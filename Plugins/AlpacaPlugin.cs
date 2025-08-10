using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using Alpaca.Markets;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for Alpaca market data and trading operations
/// </summary>
public class AlpacaPlugin
{
    private readonly AlpacaService _alpacaService;

    public AlpacaPlugin(AlpacaService alpacaService)
    {
        _alpacaService = alpacaService;
    }

    [KernelFunction, Description("Get real-time market data for a stock symbol from Alpaca")]
    public async Task<string> GetMarketDataAsync(
        [Description("The stock symbol to get data for (e.g., AAPL, TSLA, SPY)")] string symbol)
    {
        try
        {
            var marketData = await _alpacaService.GetMarketDataAsync(symbol.ToUpper());
            
            if (marketData == null)
            {
                return $"ERROR: No market data available for {symbol}. Please check the symbol or try again later.";
            }

            return $"ANALYSIS: Market Data for {symbol}\n" +
                   $"Price: ${marketData.Price:F2}\n" +
                   $"Volume: {marketData.Volume:F0}\n" +
                   $"24h High: ${marketData.High24h:F2}\n" +
                   $"24h Low: ${marketData.Low24h:F2}\n" +
                   $"24h Change: {marketData.Change24h:F2}%\n" +
                   $"Updated: {marketData.Timestamp:HH:mm:ss UTC}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Error fetching market data for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get historical price data for a stock symbol")]
    public async Task<string> GetHistoricalDataAsync(
        [Description("The stock symbol")] string symbol,
        [Description("Number of days of historical data (default: 30)")] int days = 30,
        [Description("Timeframe: minute, hour, day (default: day)")] string timeframe = "day")
    {
        try
        {
            BarTimeFrame barTimeFrame = timeframe.ToLower() switch
            {
                "minute" => BarTimeFrame.Minute,
                "hour" => BarTimeFrame.Hour,
                _ => BarTimeFrame.Day
            };

            var bars = await _alpacaService.GetHistoricalBarsAsync(symbol.ToUpper(), days, barTimeFrame);
            
            if (!bars.Any())
            {
                return $"ERROR: No historical data available for {symbol}";
            }

            var result = $" Historical Data for {symbol} ({days} days, {timeframe})\n";
            result += new string('=', 50) + "\n";
            result += "Date".PadRight(12) + " | " + "Open".PadRight(8) + " | " + "High".PadRight(8) + " | " + 
                     "Low".PadRight(8) + " | " + "Close".PadRight(8) + " | Volume\n";
            result += new string('-', 65) + "\n";

            foreach (var bar in bars.TakeLast(10)) // Show last 10 bars
            {
                result += $"{bar.TimeUtc:MM/dd/yyyy}".PadRight(12) + " | " +
                         $"{bar.Open:F2}".PadRight(8) + " | " +
                         $"{bar.High:F2}".PadRight(8) + " | " +
                         $"{bar.Low:F2}".PadRight(8) + " | " +
                         $"{bar.Close:F2}".PadRight(8) + " | " +
                         $"{bar.Volume:F0}\n";
            }

            // Calculate basic statistics
            var prices = bars.Select(b => (double)b.Close).ToList();
            var avgPrice = prices.Average();
            var minPrice = prices.Min();
            var maxPrice = prices.Max();
            var priceChange = prices.Last() - prices.First();
            var priceChangePercent = (priceChange / prices.First()) * 100;

            result += $"\nANALYSIS: Summary:\n";
            result += $"• Average Price: ${avgPrice:F2}\n";
            result += $"• Period High: ${maxPrice:F2}\n";
            result += $"• Period Low: ${minPrice:F2}\n";
            result += $"• Total Change: ${priceChange:F2} ({priceChangePercent:F2}%)\n";
            result += $"• Total Bars: {bars.Count}";

            return result;
        }
        catch (Exception ex)
        {
            return $"ERROR: Error fetching historical data for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get account information from Alpaca")]
    public async Task<string> GetAccountInfoAsync()
    {
        try
        {
            var account = await _alpacaService.GetAccountInfoAsync();
            
            if (account == null)
            {
                return "ERROR: Unable to retrieve account information. Please check your Alpaca API credentials.";
            }

            return $"BUSINESS: Alpaca Account Information\n" +
                   $"Account Number: {account.AccountNumber}\n" +
                   $"Status: {account.Status}\n" +
                   $"Trading Blocked: {account.IsTradingBlocked}\n" +
                   $"Transfers Blocked: {account.IsTransfersBlocked}\n" +
                   $"Account Blocked: {account.IsAccountBlocked}\n" +
                   $"Buying Power: ${account.BuyingPower:F2}\n" +
                   $"Cash: ${account.TradableCash:F2}\n" +
                   $"Equity: ${account.Equity:F2}\n" +
                   $"Initial Margin: ${account.InitialMargin:F2}\n" +
                   $"Maintenance Margin: ${account.MaintenanceMargin:F2}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Error retrieving account information: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get current positions from Alpaca")]
    public async Task<string> GetPositionsAsync()
    {
        try
        {
            var positions = await _alpacaService.GetPositionsAsync();
            
            if (!positions.Any())
            {
                return "ANALYSIS: No open positions found.";
            }

            var result = $"ANALYSIS: Current Positions ({positions.Count})\n";
            result += new string('=', 60) + "\n";
            result += "Symbol".PadRight(8) + " | " + "Qty".PadRight(8) + " | " + "Avg Price".PadRight(10) + " | " + 
                     "Current".PadRight(10) + " | " + "P&L".PadRight(10) + " | Side\n";
            result += new string('-', 65) + "\n";

            decimal totalPnL = 0;
            
            foreach (var position in positions)
            {
                var pnl = position.UnrealizedProfitLoss ?? 0;
                totalPnL += pnl;
                
                result += $"{position.Symbol}".PadRight(8) + " | " +
                         $"{position.Quantity}".PadRight(8) + " | " +
                         $"${position.AverageEntryPrice:F2}".PadRight(10) + " | " +
                         $"${position.MarketValue / position.Quantity:F2}".PadRight(10) + " | " +
                         $"${pnl:F2}".PadRight(10) + " | " +
                         $"{position.Side}\n";
            }

            result += new string('-', 65) + "\n";
            result += $"Total Unrealized P&L: ${totalPnL:F2}";

            return result;
        }
        catch (Exception ex)
        {
            return $"ERROR: Error retrieving positions: {ex.Message}";
        }
    }

    [KernelFunction, Description("Place a market order through Alpaca (Paper Trading)")]
    public async Task<string> PlaceMarketOrderAsync(
        [Description("The stock symbol")] string symbol,
        [Description("Number of shares")] int quantity,
        [Description("Order side: buy or sell")] string side)
    {
        try
        {
            if (quantity <= 0)
            {
                return "ERROR: Quantity must be greater than 0";
            }

            var orderSide = side.ToLower() switch
            {
                "buy" => OrderSide.Buy,
                "sell" => OrderSide.Sell,
                _ => throw new ArgumentException("Side must be 'buy' or 'sell'")
            };

            var order = await _alpacaService.PlaceOrderAsync(
                symbol.ToUpper(),
                OrderQuantity.Fractional(quantity),
                orderSide,
                OrderType.Market,
                TimeInForce.Day);

            if (order == null)
            {
                return $"ERROR: Failed to place order for {symbol}";
            }

            return $"SUCCESS: Market Order Placed\n" +
                   $"Order ID: {order.OrderId}\n" +
                   $"Symbol: {order.Symbol}\n" +
                   $"Side: {order.OrderSide}\n" +
                   $"Quantity: {order.Quantity}\n" +
                   $"Type: {order.OrderType}\n" +
                   $"Status: {order.OrderStatus}\n" +
                   $"Submitted: {order.SubmittedAtUtc:yyyy-MM-dd HH:mm:ss UTC}\n" +
                   $"WARNING: Note: This is a paper trading order";
        }
        catch (Exception ex)
        {
            return $"ERROR: Error placing order: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get multiple stock quotes at once")]
    public async Task<string> GetMultipleQuotesAsync(
        [Description("Comma-separated list of symbols (e.g., AAPL,MSFT,GOOGL)")] string symbols)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim().ToUpper()).ToArray();
            var marketDataList = await _alpacaService.GetMultipleSymbolDataAsync(symbolList);
            
            if (!marketDataList.Any())
            {
                return $"ERROR: No market data available for the provided symbols";
            }

            var result = $"ANALYSIS: Market Data for Multiple Symbols\n";
            result += new string('=', 50) + "\n";
            result += "Symbol".PadRight(8) + " | " + "Price".PadRight(10) + " | " + "Volume".PadRight(12) + " | Change\n";
            result += new string('-', 45) + "\n";

            foreach (var data in marketDataList)
            {
                var changeEmoji = data.Change24h >= 0 ? "" : "TREND:";
                result += $"{data.Symbol}".PadRight(8) + " | " +
                         $"${data.Price:F2}".PadRight(10) + " | " +
                         $"{data.Volume:F0}".PadRight(12) + " | " +
                         $"{changeEmoji} {data.Change24h:F2}%\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"ERROR: Error fetching multiple quotes: {ex.Message}";
        }
    }
}
