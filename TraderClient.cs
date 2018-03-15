using System;
using Binance.Net;
using System.Linq;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Logging;
using Newtonsoft.Json.Linq;

namespace Binance_Trader
{
    class TraderClient
    {
        static string baseCurrency, tradeCurrency, key, secret, symbol;
        static decimal tradeDifference, tradeProfit, lastProfitablePrice;
        static decimal? lastPrice = null;
        static int tradeAmount, panicBuyCounter, panicSellCounter;
        static long? orderId = null;
        static void Main()
        {
            SuccessLog("binance-trader-csharp has been started");
            var config = JObject.Parse(System.IO.File.ReadAllText("config.json"));
            /* Binance Configuration */
            baseCurrency = (string)config["baseCurrency"];
            tradeCurrency = (string)config["tradeCurrency"];
            key = (string)config["key"];
            secret = (string)config["secret"];
            symbol = tradeCurrency + baseCurrency;
            InfoLog("Base Currency: " + baseCurrency + " | Trading Currency: " + tradeCurrency + " | Symbol: " + symbol);

            /* Bot Trade Configuration */
            tradeDifference = Convert.ToDecimal(config["tradeDifference"]);
            tradeProfit = Convert.ToDecimal(config["tradeProfit"]);
            tradeAmount = (int)config["tradeAmount"];
            InfoLog("Trade Difference: " + tradeDifference + " | Trade Profit: " + tradeProfit + "% | Trade Amount: " + tradeAmount);

            BinanceClient.SetDefaultOptions(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(key, secret),
                LogVerbosity = LogVerbosity.None,
                LogWriter = null
            });

            var tick = new System.Timers.Timer();
            tick.Elapsed += Tick_Elapsed;
            tick.Interval = 3000;
            tick.Enabled = true;
            Console.ReadLine();
        }

        private static void Tick_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            InfoLog("Connecting to Binance");
            using (var client = new BinanceClient())
            {
                var orderBook = client.GetOrderBook(symbol, 5);
                decimal? tradeCurrencyPrice = client.Get24HPrice(symbol).Data.LastPrice;
                var tradingBalance = client.GetAccountInfo().Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == tradeCurrency.ToLower());
                decimal totalTradingBalance = tradingBalance.Total;
                decimal lastBid = orderBook.Data.Bids[0].Price;
                decimal lastAsk = orderBook.Data.Asks[0].Price;
                decimal buyPrice = lastBid + tradeDifference;
                decimal sellPrice = lastAsk - tradeDifference;
                decimal profitablePrice = buyPrice + (buyPrice * tradeProfit / 100);
                InfoLog("buyPrice: " + buyPrice + " | sellPrice " + sellPrice + " | bid " + lastBid + " | ask " + lastAsk + " | price " + profitablePrice + " | diff " + (lastAsk - profitablePrice).ToString());

                if (orderId == null)
                {
                    InfoLog("No open orders. Let's check the Order Book :D");
                    if (lastAsk >= profitablePrice)
                    {
                        if (tradeCurrencyPrice > lastPrice)
                        {
                            SuccessLog("Detected a buy burst! Placing an order for " + tradeAmount + tradeCurrency + " at " + buyPrice + baseCurrency);
                            lastProfitablePrice = profitablePrice;
                            orderId = client.PlaceOrder(symbol, Binance.Net.Objects.OrderSide.Buy, Binance.Net.Objects.OrderType.Limit, tradeAmount, null, buyPrice, Binance.Net.Objects.TimeInForce.GoodTillCancel).Data.OrderId;
                            panicBuyCounter = 0; panicSellCounter = 0;
                        }
                        else
                        {
                            WarnLog("Price seems to be falling. Initiating a Panic Sell");
                            var currentTradingAsset = client.GetAccountInfo().Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == tradeCurrency.ToLower());
                            decimal currentTradingBalance = currentTradingAsset.Free + currentTradingAsset.Locked;
                            if (currentTradingBalance > 1)
                            {
                                WarnLog("Last known trading balance: " + tradingBalance.Total + " - Attempt to sell at market (Bought: " + tradeCurrencyPrice + tradeCurrency + ")");
                                var orderResult = client.PlaceOrder(symbol, Binance.Net.Objects.OrderSide.Sell, Binance.Net.Objects.OrderType.Market, Convert.ToInt32(currentTradingAsset.Free));
                                InfoLog("Placed sell order at market price: " + orderResult.Data.Price + baseCurrency + " | Diff: " + (tradeCurrencyPrice - orderResult.Data.Price).ToString());
                            }
                        }
                    }
                    else
                    {
                        InfoLog("No profit detected. Diff: " + (lastAsk - profitablePrice).ToString() + baseCurrency);
                        var currentTradingAsset = client.GetAccountInfo().Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == tradeCurrency.ToLower());
                        decimal currentTradingBalance = currentTradingAsset.Free + currentTradingAsset.Locked;
                        if (currentTradingBalance > 1)
                        {
                            WarnLog("Last known trading balance: " + tradingBalance.Total + " - Attempt to sell at market (Bought: " + tradeCurrencyPrice + tradeCurrency + ")");
                            var orderResult = client.PlaceOrder(symbol, Binance.Net.Objects.OrderSide.Sell, Binance.Net.Objects.OrderType.Market, Convert.ToInt32(currentTradingAsset.Free));
                            InfoLog("Placed sell order at market price: " + orderResult.Data.Price + baseCurrency + " | Diff: " + (tradeCurrencyPrice - orderResult.Data.Price).ToString());
                        }
                        tradeCurrencyPrice = null;
                    }
                }
                else
                {
                    var order = client.QueryOrder(symbol, orderId);
                    var status = order.Data.Status;
                    if (status != Binance.Net.Objects.OrderStatus.Canceled)
                    {
                        if("0".Equals(tradingBalance.Locked.ToString()[0]) && lastAsk >= tradeCurrencyPrice)
                        {
                            if (status == Binance.Net.Objects.OrderStatus.New)
                            {
                                panicBuyCounter++;
                                WarnLog("Order is still NEW. Panic Buy Counter: " + panicBuyCounter);
                                if (panicBuyCounter > 4)
                                {
                                    var cancelResult = client.CancelOrder(symbol, orderId);
                                    panicBuyCounter = 0;
                                    panicSellCounter = 0;
                                    orderId = null;
                                    tradeCurrencyPrice = 0;
                                }
                            }
                            else
                            {
                                if ("0".Equals(tradingBalance.Free.ToString()[0]))
                                {
                                    WarnLog("No balance left in trading money. Proceeding to next tick...");
                                    panicBuyCounter = 0;
                                    panicSellCounter = 0;
                                    orderId = null;
                                    tradeCurrencyPrice = 0;
                                }
                                else if (status == Binance.Net.Objects.OrderStatus.PartiallyFilled || status == Binance.Net.Objects.OrderStatus.Filled)
                                {
                                    InfoLog("Order fulfilled with status " + status);
                                    if (lastAsk >= profitablePrice)
                                    {
                                        SuccessLog("Still gaining profits :D HODL!");
                                    }
                                    else
                                    {
                                        WarnLog("Not making profits anymore. Time to sell");
                                        var orderResult = client.PlaceOrder(symbol, Binance.Net.Objects.OrderSide.Sell, Binance.Net.Objects.OrderType.Limit, tradeAmount, null, sellPrice, Binance.Net.Objects.TimeInForce.GoodTillCancel);
                                        SuccessLog("Placed sell order. Bought " + tradeAmount + " for " + tradeCurrencyPrice + baseCurrency + " - selling for " + sellPrice + baseCurrency + " => Profit: " + (tradeCurrencyPrice - sellPrice).ToString());
                                    }
                                }
                                else
                                {
                                    ErrorLog("Unexpected order status. Initiating panic sell");
                                    var currentTradingAsset = client.GetAccountInfo().Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == tradeCurrency.ToLower());
                                    decimal currentTradingBalance = currentTradingAsset.Free + currentTradingAsset.Locked;
                                    WarnLog("Last known trading balance: " + tradingBalance.Total + " - Attempt to sell at market (Bought: " + tradeCurrencyPrice + tradeCurrency + ")");
                                    var orders = client.GetOpenOrders().Data;
                                    foreach (var o in orders)
                                    {
                                        var cancelResult = client.CancelOrder(symbol, o.OrderId);
                                    }
                                    var orderResult = client.PlaceOrder(symbol, Binance.Net.Objects.OrderSide.Sell, Binance.Net.Objects.OrderType.Market, Convert.ToInt32(currentTradingAsset.Free));
                                    InfoLog("Placed sell order at market price: " + orderResult.Data.Price + baseCurrency + " | Diff: " + (tradeCurrencyPrice - orderResult.Data.Price).ToString());
                                    panicBuyCounter = 0;
                                    panicSellCounter = 0;
                                    orderId = null;
                                    tradeCurrencyPrice = 0;
                                }
                            }
                        }
                        else
                        {
                            panicSellCounter++;
                            InfoLog("Sell request unsuccessful. Panic sell counter: " + panicSellCounter);
                            if (panicSellCounter > 3)
                            {
                                var currentTradingAsset = client.GetAccountInfo().Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == tradeCurrency.ToLower());
                                decimal currentTradingBalance = currentTradingAsset.Free + currentTradingAsset.Locked;
                                WarnLog("Last known trading balance: " + tradingBalance.Total + " - Attempt to sell at market (Bought: " + tradeCurrencyPrice + tradeCurrency + ")");
                                var orders = client.GetOpenOrders().Data;
                                foreach (var o in orders)
                                {
                                    var cancelResult = client.CancelOrder(symbol, o.OrderId);
                                }
                                var orderResult = client.PlaceOrder(symbol, Binance.Net.Objects.OrderSide.Sell, Binance.Net.Objects.OrderType.Market, Convert.ToInt32(currentTradingAsset.Free));
                                InfoLog("Placed sell order at market price: " + orderResult.Data.Price + baseCurrency + " | Diff: " + (tradeCurrencyPrice - orderResult.Data.Price).ToString());
                                panicBuyCounter = 0;
                                panicSellCounter = 0;
                                orderId = null;
                                tradeCurrencyPrice = 0;
                            }
                        }
                    }
                    else
                    {
                        WarnLog("Order was canceled. Proceeding to next tick...");
                        panicBuyCounter = 0;
                        panicSellCounter = 0;
                        orderId = null;
                        tradeCurrencyPrice = 0;
                    }
                }
                lastPrice = tradeCurrencyPrice;
            }
        }

        static void ErrorLog(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(DateTime.Now.ToString() + " | ERROR: " + msg);
            Console.ResetColor();
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        static void WarnLog(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(DateTime.Now.ToString() + " | WARN: " + msg);
            Console.ResetColor();
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        static void SuccessLog(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(DateTime.Now.ToString() + " | SUCCESS: " + msg);
            Console.ResetColor();
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        static void InfoLog(string msg)
        {
            Console.WriteLine(DateTime.Now.ToString() + " | INFO: " + msg);
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }
    }
}
