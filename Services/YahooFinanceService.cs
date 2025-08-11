using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Yahoo Finance data service that integrates with the kernel plugins
    /// to provide market data as a fallback when other sources fail
    /// </summary>
    public class YahooFinanceService
    {
        private readonly Kernel _kernel;
        private readonly ILogger<YahooFinanceService> _logger;

        public YahooFinanceService(Kernel kernel, ILogger<YahooFinanceService> logger)
        {
            _kernel = kernel;
            _logger = logger;
        }

        /// <summary>
        /// Get market data for a symbol using the Yahoo Finance plugin
        /// </summary>
        public async Task<YahooMarketData?> GetMarketDataAsync(string symbol)
        {
            try
            {
                // Check if MarketDataPlugin is available
                if (!_kernel.Plugins.Any(p => p.Name.Contains("MarketData")))
                {
                    _logger.LogWarning("MarketData plugin not available");
                    return null;
                }

                var function = _kernel.Plugins["MarketDataPlugin"]["GetYahooMarketData"];
                var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
                
                var resultString = result.ToString();
                if (string.IsNullOrEmpty(resultString))
                {
                    return null;
                }

                // Parse the Yahoo Finance result
                return ParseYahooFinanceResult(resultString, symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get Yahoo Finance data for {symbol}");
                return null;
            }
        }

        private YahooMarketData? ParseYahooFinanceResult(string result, string symbol)
        {
            try
            {
                var data = new YahooMarketData { Symbol = symbol };
                
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Current Price:"))
                    {
                        var priceStr = ExtractValue(line, "Current Price:");
                        if (decimal.TryParse(priceStr.Replace("$", "").Replace(",", ""), out var price))
                        {
                            data.CurrentPrice = price;
                        }
                    }
                    else if (line.Contains("Change (24h):"))
                    {
                        var changeStr = ExtractValue(line, "Change (24h):");
                        if (decimal.TryParse(changeStr.Replace("$", "").Replace(",", ""), out var change))
                        {
                            data.Change24h = change;
                        }
                    }
                    else if (line.Contains("High (24h):"))
                    {
                        var highStr = ExtractValue(line, "High (24h):");
                        if (decimal.TryParse(highStr.Replace("$", "").Replace(",", ""), out var high))
                        {
                            data.High24h = high;
                        }
                    }
                    else if (line.Contains("Low (24h):"))
                    {
                        var lowStr = ExtractValue(line, "Low (24h):");
                        if (decimal.TryParse(lowStr.Replace("$", "").Replace(",", ""), out var low))
                        {
                            data.Low24h = low;
                        }
                    }
                    else if (line.Contains("Volume:"))
                    {
                        var volumeStr = ExtractValue(line, "Volume:");
                        if (long.TryParse(volumeStr.Replace(",", ""), out var volume))
                        {
                            data.Volume = volume;
                        }
                    }
                    else if (line.Contains("Last Updated:"))
                    {
                        var timeStr = ExtractValue(line, "Last Updated:");
                        if (DateTime.TryParse(timeStr.Replace(" UTC", ""), out var timestamp))
                        {
                            data.LastUpdated = timestamp;
                        }
                    }
                }

                // Calculate percentage change
                if (data.CurrentPrice > 0 && data.Change24h != 0)
                {
                    data.ChangePercent24h = (double)(data.Change24h / data.CurrentPrice) * 100;
                }

                return data.CurrentPrice > 0 ? data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to parse Yahoo Finance result for {symbol}: {result}");
                return null;
            }
        }

        private string ExtractValue(string line, string prefix)
        {
            var index = line.IndexOf(prefix);
            if (index >= 0)
            {
                return line.Substring(index + prefix.Length).Trim();
            }
            return "";
        }

        /// <summary>
        /// Get formatted market data summary
        /// </summary>
        public async Task<string> GetFormattedMarketDataAsync(string symbol)
        {
            var data = await GetMarketDataAsync(symbol);
            if (data == null)
            {
                return $"No Yahoo Finance data available for {symbol}";
            }

            return $@"Yahoo Finance Data for {symbol.ToUpper()}:
Current Price: ${data.CurrentPrice:F2}
Daily Change: ${data.Change24h:F2} ({data.ChangePercent24h:+0.00;-0.00;0.00}%)
High 24h: ${data.High24h:F2}
Low 24h: ${data.Low24h:F2}
Volume: {data.Volume:N0}
Last Updated: {data.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC
Data Source: Yahoo Finance";
        }
    }

    public class YahooMarketData
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal Change24h { get; set; }
        public double ChangePercent24h { get; set; }
        public decimal High24h { get; set; }
        public decimal Low24h { get; set; }
        public long Volume { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
