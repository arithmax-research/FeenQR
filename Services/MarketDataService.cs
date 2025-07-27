using YahooFinanceApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using RestSharp;
using System.Collections.Concurrent;
using System.Text.Json;

namespace QuantResearchAgent.Services;

public class MarketDataService
{
    private readonly ILogger<MarketDataService> _logger;
    private readonly IConfiguration _configuration;
    private readonly RestClient _binanceClient;
    private readonly RestClient _alphaVantageClient;
    private readonly AlpacaService _alpacaService;
    private readonly LeanDataService _leanDataService;
    // ...existing code...
    private readonly ConcurrentDictionary<string, MarketData> _marketDataCache = new();
    private readonly ConcurrentDictionary<string, List<MarketData>> _historicalDataCache = new();

    public MarketDataService(ILogger<MarketDataService> logger, IConfiguration configuration, AlpacaService alpacaService, LeanDataService leanDataService)
    {
        _logger = logger;
        _configuration = configuration;
        _binanceClient = new RestClient("https://api.binance.com");
        _alphaVantageClient = new RestClient("https://www.alphavantage.co");
        _alpacaService = alpacaService;
        _leanDataService = leanDataService;
    // ...existing code...
    }

    public async Task RefreshMarketDataAsync()
    {
        _logger.LogInformation("Refreshing market data...");
        
        var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "ADAUSDT", "SOLUSDT" };
        var tasks = symbols.Select(symbol => RefreshSymbolDataAsync(symbol));
        
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Market data refresh completed for {SymbolCount} symbols", symbols.Length);
    }

    public async Task<MarketData?> GetMarketDataAsync(string symbol)
    {
        // Check cache first
        if (_marketDataCache.TryGetValue(symbol, out var cachedData))
        {
            // Return cached data if it's less than 1 minute old
            if (DateTime.UtcNow - cachedData.Timestamp < TimeSpan.FromMinutes(1))
            {
                return cachedData;
            }
        }

        // Fetch fresh data
        var marketData = await FetchMarketDataAsync(symbol);
        if (marketData != null)
        {
            _marketDataCache[symbol] = marketData;
        }

        return marketData;
    }

    public async Task<List<MarketData>?> GetHistoricalDataAsync(string symbol, int limit = 100)
    {
        var cacheKey = $"{symbol}_{limit}";
        
        // Check cache first
        if (_historicalDataCache.TryGetValue(cacheKey, out var cachedData))
        {
            // Return cached data if it's less than 5 minutes old
            if (cachedData.Any() && DateTime.UtcNow - cachedData.Last().Timestamp < TimeSpan.FromMinutes(5))
            {
                return cachedData;
            }
        }

        // Fetch fresh historical data
        var historicalData = await FetchHistoricalDataAsync(symbol, limit);
        if (historicalData != null)
        {
            _historicalDataCache[cacheKey] = historicalData;
        }

        return historicalData;
    }

    private async Task RefreshSymbolDataAsync(string symbol)
    {
        try
        {
            var marketData = await FetchMarketDataAsync(symbol);
            if (marketData != null)
            {
                _marketDataCache[symbol] = marketData;
                _logger.LogDebug("Refreshed market data for {Symbol}: ${Price:F2}", symbol, marketData.Price);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh market data for symbol: {Symbol}", symbol);
        }
    }

    private async Task<MarketData?> FetchMarketDataAsync(string symbol)
    {
        return await FetchMarketDataAsync(symbol, frequency: "daily");
    }
    // Overload to allow specifying frequency ("daily" or "minute")
    public async Task<MarketData?> FetchMarketDataAsync(string symbol, string frequency)
    {
        try
        {
            // Try Binance first for crypto symbols
            if (symbol.Contains("USDT") || symbol.Contains("BTC") || symbol.Contains("ETH"))
            {
                var binanceData = await FetchBinanceDataAsync(symbol);
                if (binanceData != null) binanceData.Source = "binance";
                return binanceData;
            }
            // Try LeanDataService for local equity data first
            var leanSymbol = symbol.Trim().ToUpper();
            var hasLocalData = await _leanDataService.HasDataForSymbolAsync(leanSymbol, frequency == "minute");
            if (hasLocalData)
            {
                var bars = await _leanDataService.GetEquityBarsAsync(leanSymbol, frequency, 2);
                var latestBar = bars.LastOrDefault();
                var prevBar = bars.Count > 1 ? bars[^2] : null;
                if (latestBar != null)
                {
                    _logger.LogInformation($"Fetched market data for {symbol} from local Lean data ({frequency})");
                    double change24h = prevBar != null ? (double)(latestBar.Close - prevBar.Close) : (double)(latestBar.Close - latestBar.Open);
                    double changePercent24h = prevBar != null && prevBar.Close != 0 ? (change24h / (double)prevBar.Close) * 100 : 0;
                    return new MarketData
                    {
                        Symbol = leanSymbol,
                        Price = (double)latestBar.Close,
                        Volume = latestBar.Volume,
                        High24h = (double)latestBar.High,
                        Low24h = (double)latestBar.Low,
                        Change24h = change24h,
                        ChangePercent24h = changePercent24h,
                        Timestamp = latestBar.Time,
                        Source = "local"
                    };
                }
            }
            // Try local zip/csv files if LeanData is missing
            MarketData? localData = null;
            if (frequency == "minute")
            {
                localData = TryReadLatestMinuteData(symbol);
            }
            else
            {
                localData = TryReadLatestDailyData(symbol);
            }
            if (localData != null)
            {
                _logger.LogInformation($"Fetched market data for {symbol} from local {frequency} file");
                return localData;
            }

            // Fallback to Yahoo for US stocks
            var yahooData = await FetchYahooFinanceCurrentData(symbol);
            if (yahooData != null)
            {
                _logger.LogInformation($"Fetched market data for {symbol} from Yahoo Finance");
                return yahooData;
            }

            // TODO: Use Alpaca WebSocket for recent data if needed
            _logger.LogWarning($"No market data found for {symbol} (all sources failed)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to fetch market data for symbol: {symbol}");
            return null;
        }
    }

    // Helper to read latest daily data from zip/csv
    private MarketData? TryReadLatestDailyData(string symbol)
    {
        try
        {
            var basePath = "/Users/misango/codechest/ArithmaxResearchChest/data/equity/usa/daily/";
            var fileName = symbol.ToLower() + ".zip";
            var filePath = System.IO.Path.Combine(basePath, fileName);
            if (!System.IO.File.Exists(filePath)) return null;
            using (var archive = System.IO.Compression.ZipFile.OpenRead(filePath))
            {
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".csv"));
                if (entry == null) return null;
                using (var reader = new System.IO.StreamReader(entry.Open()))
                {
                    var validLines = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                            validLines.Add(line);
                    }
                    if (validLines.Count >= 2)
                    {
                        var prevParts = validLines[validLines.Count - 2].Split(',');
                        var lastParts = validLines[validLines.Count - 1].Split(',');
                        // Assume Lean format: date,open,high,low,close,volume,...
                        var dt = DateTime.ParseExact(lastParts[0], "yyyyMMdd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                        var open = double.Parse(lastParts[1]);
                        var high = double.Parse(lastParts[2]);
                        var low = double.Parse(lastParts[3]);
                        var close = double.Parse(lastParts[4]);
                        var volume = double.Parse(lastParts[5]);
                        var prevClose = double.Parse(prevParts[4]);
                        double change24h = close - prevClose;
                        double changePercent24h = prevClose != 0 ? (change24h / prevClose) * 100 : 0;
                        return new MarketData
                        {
                            Symbol = symbol.ToUpper(),
                            Price = close,
                            Volume = volume,
                            High24h = high,
                            Low24h = low,
                            Change24h = change24h,
                            ChangePercent24h = changePercent24h,
                            Timestamp = dt,
                            Source = "local"
                        };
                    }
                    else if (validLines.Count == 1)
                    {
                        // Fallback: only one line, can't compute change
                        var parts = validLines[0].Split(',');
                        var dt = DateTime.ParseExact(parts[0], "yyyyMMdd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                        var open = double.Parse(parts[1]);
                        var high = double.Parse(parts[2]);
                        var low = double.Parse(parts[3]);
                        var close = double.Parse(parts[4]);
                        var volume = double.Parse(parts[5]);
                        return new MarketData
                        {
                            Symbol = symbol.ToUpper(),
                            Price = close,
                            Volume = volume,
                            High24h = high,
                            Low24h = low,
                            Change24h = close - open,
                            ChangePercent24h = 0,
                            Timestamp = dt,
                            Source = "local"
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading local daily data for {symbol}");
        }
        return null;
    }

    // Helper to read latest minute data from zip/csv
    private MarketData? TryReadLatestMinuteData(string symbol)
    {
        try
        {
            var basePath = $"/Users/misango/codechest/ArithmaxResearchChest/data/equity/usa/minute/{symbol.ToLower()}";
            if (!System.IO.Directory.Exists(basePath)) return null;
            var files = System.IO.Directory.GetFiles(basePath, "*_trade.zip").OrderByDescending(f => f).ToList();
            foreach (var file in files)
            {
                using (var archive = System.IO.Compression.ZipFile.OpenRead(file))
                {
                    var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".csv"));
                    if (entry == null) continue;
                    using (var reader = new System.IO.StreamReader(entry.Open()))
                    {
                        string? line;
                        string? lastLine = null;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                                lastLine = line;
                        }
                        if (lastLine != null)
                        {
                            var parts = lastLine.Split(',');
                            // Assume Lean format: datetime,open,high,low,close,volume,...
                            var dt = DateTime.ParseExact(parts[0], "yyyyMMdd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                            var open = double.Parse(parts[1]);
                            var high = double.Parse(parts[2]);
                            var low = double.Parse(parts[3]);
                            var close = double.Parse(parts[4]);
                            var volume = double.Parse(parts[5]);
                            return new MarketData
                            {
                                Symbol = symbol.ToUpper(),
                                Price = close,
                                Volume = volume,
                                High24h = high,
                                Low24h = low,
                                Change24h = close - open,
                                Timestamp = dt
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading local minute data for {symbol}");
        }
        return null;
    }

    private async Task<MarketData?> FetchBinanceDataAsync(string symbol)
    {
        try
        {
            // Get 24hr ticker statistics
            var request = new RestRequest($"/api/v3/ticker/24hr");
            request.AddParameter("symbol", symbol);
            
            var response = await _binanceClient.ExecuteAsync(request);
            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                _logger.LogWarning("Failed to fetch Binance data for {Symbol}: {Error}", symbol, response.ErrorMessage);
                return null;
            }

            var tickerData = JsonSerializer.Deserialize<BinanceTicker>(response.Content);
            if (tickerData == null)
            {
                _logger.LogWarning("Failed to deserialize Binance ticker data for {Symbol}", symbol);
                return null;
            }

            return new MarketData
            {
                Symbol = symbol,
                Price = double.Parse(tickerData.LastPrice),
                Volume = double.Parse(tickerData.Volume),
                Change24h = double.Parse(tickerData.PriceChange),
                ChangePercent24h = double.Parse(tickerData.PriceChangePercent),
                High24h = double.Parse(tickerData.HighPrice),
                Low24h = double.Parse(tickerData.LowPrice),
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Binance data for symbol: {Symbol}", symbol);
            return null;
        }
    }


    // Fetch current data from Yahoo Finance
    private async Task<MarketData?> FetchYahooFinanceCurrentData(string symbol)
    {
        try
        {
            var securities = await Yahoo.Symbols(symbol).Fields(Field.RegularMarketPrice, Field.RegularMarketVolume, Field.RegularMarketDayHigh, Field.RegularMarketDayLow, Field.RegularMarketPreviousClose, Field.RegularMarketOpen, Field.RegularMarketTime).QueryAsync();
            if (securities.TryGetValue(symbol, out var sec))
            {
                double price = (double?)sec[Field.RegularMarketPrice] ?? 0;
                double prevClose = (double?)sec[Field.RegularMarketPreviousClose] ?? 0;
                double change24h = price - prevClose;
                double changePercent24h = prevClose != 0 ? (change24h / prevClose) * 100 : 0;
                return new MarketData
                {
                    Symbol = symbol.ToUpper(),
                    Price = price,
                    Volume = (double?)sec[Field.RegularMarketVolume] ?? 0,
                    High24h = (double?)sec[Field.RegularMarketDayHigh] ?? 0,
                    Low24h = (double?)sec[Field.RegularMarketDayLow] ?? 0,
                    Change24h = change24h,
                    ChangePercent24h = changePercent24h,
                    Timestamp = sec[Field.RegularMarketTime] is DateTime dt ? dt : DateTime.UtcNow,
                    Source = "yahoo"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching Yahoo Finance current data for {symbol}");
        }
        return null;
    }

    private async Task<List<MarketData>?> FetchHistoricalDataAsync(string symbol, int limit)
    {
        try
        {
            if (symbol.Contains("USDT") || symbol.Contains("BTC") || symbol.Contains("ETH"))
            {
                return await FetchBinanceHistoricalDataAsync(symbol, limit);
            }
            else
            {
                var data = FetchStockHistoricalData(symbol, limit);
                // Do not fallback to Yahoo. Only use local data.
                if (data != null)
                {
                    // Filter: exclude the most recent minute, and only include up to 5 years ago
                    var now = DateTime.UtcNow;
                    var fiveYearsAgo = now.AddYears(-5);
                    data = data.Where(d => d.Timestamp < now.AddMinutes(-1) && d.Timestamp >= fiveYearsAgo)
                               .OrderBy(d => d.Timestamp)
                               .TakeLast(limit)
                               .ToList();
                }
                return data;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to fetch historical data for symbol: {symbol}");
            return null;
        }
    }
    // Fallback: fetch historical data from Yahoo Finance (using YahooFinanceApi)
    private async Task<List<MarketData>?> FetchYahooFinanceHistoricalData(string symbol, int limit)
    {
        try
        {
            // Use YahooFinanceApi NuGet package (add to your project if not present)
            var start = DateTime.UtcNow.AddYears(-5);
            var end = DateTime.UtcNow.AddMinutes(-1);
            var history = await Yahoo.GetHistoricalAsync(symbol, start, end, Period.Daily);
            var data = history.Select(bar => new MarketData
            {
                Symbol = symbol.ToUpper(),
                Price = (double)bar.Close,
                Volume = (double)bar.Volume,
                High24h = (double)bar.High,
                Low24h = (double)bar.Low,
                Change24h = (double)(bar.Close - bar.Open),
                Timestamp = bar.DateTime
            })
            .OrderBy(d => d.Timestamp)
            .TakeLast(limit)
            .ToList();
            _logger.LogInformation($"Fetched {data.Count} bars from Yahoo Finance for {symbol}");
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching Yahoo Finance data for {symbol}");
            return null;
        }
    }

    private async Task<List<MarketData>?> FetchBinanceHistoricalDataAsync(string symbol, int limit)
    {
        try
        {
            var request = new RestRequest("/api/v3/klines");
            request.AddParameter("symbol", symbol);
            request.AddParameter("interval", "5m"); // 5-minute intervals
            request.AddParameter("limit", limit);

            var response = await _binanceClient.ExecuteAsync(request);
            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                _logger.LogWarning("Failed to fetch Binance historical data for {Symbol}: {Error}", symbol, response.ErrorMessage);
                return null;
            }

            var klineData = JsonSerializer.Deserialize<System.Text.Json.JsonElement[][]>(response.Content);
            if (klineData == null)
            {
                _logger.LogWarning("Failed to deserialize Binance kline data for {Symbol}", symbol);
                return null;
            }

            var historicalData = new List<MarketData>();
            foreach (var kline in klineData)
            {
                // Binance returns numbers, not strings
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(kline[0].GetInt64()).DateTime;
                var open = kline[1].GetDouble();
                var high = kline[2].GetDouble();
                var low = kline[3].GetDouble();
                var close = kline[4].GetDouble();
                var volume = kline[5].GetDouble();

                historicalData.Add(new MarketData
                {
                    Symbol = symbol,
                    Price = close,
                    Volume = volume,
                    High24h = high,
                    Low24h = low,
                    Timestamp = timestamp
                });
            }

            return historicalData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Binance historical data for symbol: {Symbol}", symbol);
            return null;
        }
    }

    private List<MarketData>? FetchStockHistoricalData(string symbol, int limit)
    {
        // Try to load from local data files if available
        try
        {
            var basePath = "/Users/misango/codechest/ArithmaxResearchChest/data/equity/usa/daily/";
            var fileName = symbol.ToLower() + ".zip";
            var filePath = System.IO.Path.Combine(basePath, fileName);
            if (System.IO.File.Exists(filePath))
            {
                _logger.LogInformation($"Found local data file for {symbol}: {filePath}");
                var historicalData = new List<MarketData>();
                using (var archive = System.IO.Compression.ZipFile.OpenRead(filePath))
                {
                    var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".csv"));
                    if (entry != null)
                    {
                        using (var stream = entry.Open())
                        using (var reader = new StreamReader(stream))
                        {
                            string? line;
                            bool isHeader = true;
                            var rows = new List<string>();
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (isHeader) { isHeader = false; continue; }
                                rows.Add(line);
                            }
                            // Take last N rows (limit)
                            foreach (var row in rows.Skip(Math.Max(0, rows.Count - limit)))
                            {
                                var parts = row.Split(',');
                                if (parts.Length < 7) continue;
                                // Lean daily: date,open,high,low,close,volume,dividends,splits
                                // Example: 20240725,200.1,202.5,199.8,201.2,123456,0,0
                                if (!DateTime.TryParseExact(parts[0], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                                    continue;
                                if (!double.TryParse(parts[1], out var open)) continue;
                                if (!double.TryParse(parts[2], out var high)) continue;
                                if (!double.TryParse(parts[3], out var low)) continue;
                                if (!double.TryParse(parts[4], out var close)) continue;
                                if (!long.TryParse(parts[5], out var volume)) continue;
                                historicalData.Add(new MarketData
                                {
                                    Symbol = symbol,
                                    Price = close,
                                    High24h = high,
                                    Low24h = low,
                                    Volume = volume,
                                    Timestamp = date,
                                    Source = "Local"
                                });
                            }
                        }
                    }
                }
                if (historicalData.Count > 0)
                    return historicalData;
                else
                    _logger.LogWarning($"No valid rows found in local data file for {symbol}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading local data file for {symbol}");
        }
        // Fallback: Generate mock data (for non-equities or if no file)
        var fallbackData = new List<MarketData>();
        var random = new Random();
        var basePrice = 100.0;
        for (int i = limit; i > 0; i--)
        {
            var timestamp = DateTime.UtcNow.AddMinutes(-i * 5);
            var price = basePrice + (random.NextDouble() - 0.5) * 20;
            fallbackData.Add(new MarketData
            {
                Symbol = symbol,
                Price = price,
                Volume = 50000 + random.Next(0, 100000),
                High24h = price + random.NextDouble() * 2,
                Low24h = price - random.NextDouble() * 2,
                Timestamp = timestamp,
                Source = "Mock"
            });
        }
        return fallbackData;
    }
}

// DTOs for Binance API responses
public class BinanceTicker
{
    public string Symbol { get; set; } = string.Empty;
    public string PriceChange { get; set; } = "0";
    public string PriceChangePercent { get; set; } = "0";
    public string WeightedAvgPrice { get; set; } = "0";
    public string PrevClosePrice { get; set; } = "0";
    public string LastPrice { get; set; } = "0";
    public string LastQty { get; set; } = "0";
    public string BidPrice { get; set; } = "0";
    public string AskPrice { get; set; } = "0";
    public string OpenPrice { get; set; } = "0";
    public string HighPrice { get; set; } = "0";
    public string LowPrice { get; set; } = "0";
    public string Volume { get; set; } = "0";
    public string QuoteVolume { get; set; } = "0";
    public long OpenTime { get; set; }
    public long CloseTime { get; set; }
    public long Count { get; set; }
}
