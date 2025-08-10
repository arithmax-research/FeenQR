using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;
public class MarketDataPlugin
{
    private readonly MarketDataService _marketDataService;

    public MarketDataPlugin(MarketDataService marketDataService)
    {
        _marketDataService = marketDataService;
    }

    [KernelFunction, Description("Get current Yahoo Finance data for a specific symbol (US stocks)")]
    public async Task<string> GetYahooMarketData(
        [Description("The stock symbol (e.g., AAPL, MSFT)")] string symbol)
    {
        try
        {
            // Use reflection or make FetchYahooFinanceCurrentData public, or call via GetMarketDataAsync if it falls back to Yahoo
            var method = typeof(MarketDataService).GetMethod("FetchYahooFinanceCurrentData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null)
                return $"Yahoo Finance data fetch not available for {symbol}.";
            var invokeResult = method.Invoke(_marketDataService, new object[] { symbol });
            if (invokeResult is not Task taskObj)
                return $"Yahoo Finance data fetch failed for {symbol}.";
            await taskObj.ConfigureAwait(false);
            var resultProperty = taskObj.GetType().GetProperty("Result");
            var result = resultProperty?.GetValue(taskObj);
            if (result == null)
                return $"No Yahoo Finance data available for {symbol}.";

            dynamic dyn = result;
            double price = dyn.Price;
            double change = dyn.Change24h;
            double high = dyn.High24h;
            double low = dyn.Low24h;
            double volume = dyn.Volume;
            DateTime timestamp = dyn.Timestamp;

            return $" Yahoo Finance Data for {symbol.ToUpper()}\n" +
                   $"Current Price: ${price:F2}\n" +
                   $"Change (24h): ${change:F2}\n" +
                   $"High (24h): ${high:F2}\n" +
                   $"Low (24h): ${low:F2}\n" +
                   $"Volume: {volume:N0}\n" +
                   $"Last Updated: {timestamp:yyyy-MM-dd HH:mm:ss UTC}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving Yahoo Finance data for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get current market data for a specific symbol")]
    public async Task<string> GetMarketDataAsync(
        [Description("The trading symbol (e.g., BTCUSDT, ETHUSDT, AAPL)")] string symbol)
    {
        try
        {
            var marketData = await _marketDataService.GetMarketDataAsync(symbol.ToUpper());
            if (marketData == null)
            {
                return $"No market data available for {symbol}. Please check the symbol or try again later.";
            }
            var changeIndicator = marketData.ChangePercent24h >= 0 ? "" : "TREND:";
            string source = string.IsNullOrEmpty(marketData.Source) ? "" : $" (Source: {marketData.Source})";
            return $"{changeIndicator} Market Data for {marketData.Symbol}{source}:\n\n" +
                   $"Current Price: ${marketData.Price:F2}\n" +
                   $"24h Change: ${marketData.Change24h:F2} ({marketData.ChangePercent24h:F2}%)\n" +
                   $"24h High: ${marketData.High24h:F2}\n" +
                   $"24h Low: ${marketData.Low24h:F2}\n" +
                   $"24h Volume: {marketData.Volume:N0}\n" +
                   $"Last Updated: {marketData.Timestamp:yyyy-MM-dd HH:mm:ss UTC}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving market data for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get historical price data for technical analysis")]
    public async Task<string> GetHistoricalDataAsync(
        [Description("The trading symbol")] string symbol,
        [Description("Number of data points to retrieve (default: 50)")] int limit = 50)
    {
        try
        {
            var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol.ToUpper(), limit);
            
            if (historicalData == null || !historicalData.Any())
            {
                return $"No historical data available for {symbol}.";
            }
            
            var latestData = historicalData.Last();
            var oldestData = historicalData.First();
            var totalReturn = ((latestData.Price - oldestData.Price) / oldestData.Price) * 100;
            
            // Calculate basic statistics
            var prices = historicalData.Select(d => d.Price).ToList();
            var avgPrice = prices.Average();
            var maxPrice = prices.Max();
            var minPrice = prices.Min();
            var volatility = CalculateVolatility(prices);
            
            return $"ANALYSIS: Historical Data for {symbol} (Last {historicalData.Count} periods):\n\n" +
                   $"Period: {oldestData.Timestamp:yyyy-MM-dd HH:mm} to {latestData.Timestamp:yyyy-MM-dd HH:mm}\n" +
                   $"Total Return: {totalReturn:F2}%\n\n" +
                   $"Price Statistics:\n" +
                   $"• Current: ${latestData.Price:F2}\n" +
                   $"• Average: ${avgPrice:F2}\n" +
                   $"• High: ${maxPrice:F2}\n" +
                   $"• Low: ${minPrice:F2}\n" +
                   $"• Volatility: {volatility:F2}%\n\n" +
                   $"Recent Price Action:\n" +
                   string.Join("\n", historicalData.TakeLast(5).Select(d => 
                       $"• {d.Timestamp:MM-dd HH:mm}: ${d.Price:F2}"));
        }
        catch (Exception ex)
        {
            return $"Error retrieving historical data for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Refresh market data for all tracked symbols")]
    public async Task<string> RefreshAllMarketDataAsync()
    {
        try
        {
            await _marketDataService.RefreshMarketDataAsync();
            return "SUCCESS: Market data refreshed successfully for all tracked symbols.";
        }
        catch (Exception ex)
        {
            return $"ERROR: Error refreshing market data: {ex.Message}";
        }
    }

    [KernelFunction, Description("Compare multiple symbols performance")]
    public async Task<string> CompareSymbolsAsync(
        [Description("Comma-separated list of symbols to compare (e.g., 'BTCUSDT,ETHUSDT,AAPL')")] string symbols)
    {
        try
        {
            var symbolList = symbols.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim().ToUpper())
                                   .ToList();
            
            if (symbolList.Count < 2)
            {
                return "Please provide at least 2 symbols to compare.";
            }
            
            var comparisonData = new List<(string Symbol, double Price, double Change24h, double ChangePercent24h)>();
            
            foreach (var symbol in symbolList)
            {
                var marketData = await _marketDataService.GetMarketDataAsync(symbol);
                if (marketData != null)
                {
                    comparisonData.Add((symbol, marketData.Price, marketData.Change24h, marketData.ChangePercent24h));
                }
            }
            
            if (!comparisonData.Any())
            {
                return "No valid market data found for the provided symbols.";
            }
            
            // Sort by performance
            var sortedData = comparisonData.OrderByDescending(d => d.ChangePercent24h).ToList();
            
            var result = $"ANALYSIS: Symbol Performance Comparison (24h):\n\n";
            
            for (int i = 0; i < sortedData.Count; i++)
            {
                var data = sortedData[i];
                var indicator = data.ChangePercent24h >= 0 ? "" : "TREND:";
                var position = i == 0 ? "🥇" : i == 1 ? "🥈" : i == 2 ? "🥉" : $"{i + 1}.";
                
                result += $"{position} {indicator} {data.Symbol}\n" +
                         $"   Price: ${data.Price:F2}\n" +
                         $"   Change: ${data.Change24h:F2} ({data.ChangePercent24h:F2}%)\n\n";
            }
            
            // Add summary
            var bestPerformer = sortedData.First();
            var worstPerformer = sortedData.Last();
            
            result += $"BEST: Best Performer: {bestPerformer.Symbol} (+{bestPerformer.ChangePercent24h:F2}%)\n" +
                     $"WORST: Worst Performer: {worstPerformer.Symbol} ({worstPerformer.ChangePercent24h:F2}%)";
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error comparing symbols: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get market overview with top gainers and losers")]
    public async Task<string> GetMarketOverviewAsync()
    {
        try
        {
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "ADAUSDT", "SOLUSDT", "DOGEUSDT" };
            var marketOverview = new List<(string Symbol, double Price, double ChangePercent24h, double Volume)>();
            
            foreach (var symbol in symbols)
            {
                var marketData = await _marketDataService.GetMarketDataAsync(symbol);
                if (marketData != null)
                {
                    marketOverview.Add((symbol, marketData.Price, marketData.ChangePercent24h, marketData.Volume));
                }
            }
            
            if (!marketOverview.Any())
            {
                return "Unable to retrieve market overview data.";
            }
            
            var gainers = marketOverview.Where(m => m.ChangePercent24h > 0)
                                      .OrderByDescending(m => m.ChangePercent24h)
                                      .Take(3)
                                      .ToList();
            
            var losers = marketOverview.Where(m => m.ChangePercent24h < 0)
                                     .OrderBy(m => m.ChangePercent24h)
                                     .Take(3)
                                     .ToList();
            
            var totalVolume = marketOverview.Sum(m => m.Volume);
            var avgChange = marketOverview.Average(m => m.ChangePercent24h);
            
            var result = $"CRYPTO: Crypto Market Overview:\n\n";
            
            result += $"ANALYSIS: Market Summary:\n" +
                     $"• Average Change: {avgChange:F2}%\n" +
                     $"• Total Volume: {totalVolume:N0}\n" +
                     $"• Symbols Tracked: {marketOverview.Count}\n\n";
            
            if (gainers.Any())
            {
                result += $" Top Gainers:\n";
                foreach (var gainer in gainers)
                {
                    result += $"• {gainer.Symbol}: +{gainer.ChangePercent24h:F2}%\n";
                }
                result += "\n";
            }
            
            if (losers.Any())
            {
                result += $"TREND: Top Losers:\n";
                foreach (var loser in losers)
                {
                    result += $"• {loser.Symbol}: {loser.ChangePercent24h:F2}%\n";
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error retrieving market overview: {ex.Message}";
        }
    }

    private double CalculateVolatility(List<double> prices)
    {
        if (prices.Count < 2) return 0;
        
        var returns = new List<double>();
        for (int i = 1; i < prices.Count; i++)
        {
            if (prices[i - 1] > 0)
            {
                var returnValue = (prices[i] - prices[i - 1]) / prices[i - 1];
                returns.Add(returnValue);
            }
        }
        
        if (!returns.Any()) return 0;
        
        var meanReturn = returns.Average();
        var variance = returns.Sum(r => Math.Pow(r - meanReturn, 2)) / returns.Count;
        var volatility = Math.Sqrt(variance);
        
        return volatility * 100; // Convert to percentage
    }
}
