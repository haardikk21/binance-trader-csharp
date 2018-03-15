# binance-trader-csharp

Inspired by [binance-trader](https://github.com/unterstein/binance-trader) - I decided to make a version of it in C#. This project uses JKorf's [Binance.Net](https://github.com/JKorf/Binance.Net) library to communicate with Binance.com 

**PLEASE NOTE:**
This bot in no way is supposed to be used with a lot of coins. It's an experimental bot with a very basic algorithm which can make you lose money as quickly as it can help you gain it. I take no responsibility for any losses caused due to this bot.

# The Bot Logic

 - The bot tries to capture price bursts and tries to buy low-sell high. 
 - Every 3 seconds, the bot will fetch binance order books and compare the asks and bids. 
 - If the bot thinks the price is going up, it will buy in.
 - If profit target is reached, bot will sell coins.
 - If bot faces loss or something unexpected happens, bot will sell all coins at market price.

# Configuration

The bot configuration is located in a file named `config.json` which has the following contents by default:

    {
    	"baseCurrency": "BTC",
    	"tradeCurrency": "XVG",
    	"key": "",
    	"secret": "",
    	"tradeDifference": 0.00000001,
    	"tradeProfit": 1,
    	"tradeAmount": 5
    }

After compiling the code, you are supposed to place this file in the same folder as the executable.

You are supposed to set up an API Key on Binance.com and place your `key` and `secret` in the file. 

 - tradeCurrency: The coin you will be trading
 - baseCurrency: The coin you will be holding
 - tradeDifference: The price you will under or overcut the current sellers or buyers after target has been reached
 - tradeProfit: In percentage, the target profit you want to reach. Default is 1%
 - tradeAmount: Number of coins the bot will attempt to buy/sell per burst
# Output Sample
![Sample Output](https://i.imgur.com/Z10KjxJ.png)

# Contributors
 - [**haardikk21**](http://github.com/haardikk21)

**How to contribute?**
Create a pull request along with your profile link in the same format as above in an edited README.md file.

# Donate :D
If this bot helped you in any way at all and you would like to thank me, I would appreciate any donations :D

 - BTC: 15iRWpBscCvDNfpsN2k3Vi8o1coVwjGhej
 - ETH: 0x86C05AAF20aD3ffcb5845527a98E7E2b17100991
 - BCH: qqr9cjx0lghk7e48vpw4q6z48a7r8k6rvch8sv0e8s
