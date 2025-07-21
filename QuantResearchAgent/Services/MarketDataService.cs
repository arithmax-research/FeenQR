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
    private readonly ConcurrentDictionary<string, MarketData> _marketDataCache = new();
    private readonly ConcurrentDictionary<string, List<MarketData>> _historicalDataCache = new();

    public MarketDataService(ILogger<MarketDataService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // Initialize REST clients
        _binanceClient = new RestClient("https://api.binance.com");
        _alphaVantageClient = new RestClient("https://www.alphavantage.co");
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
        try
        {
            // Try Binance first for crypto symbols
            if (symbol.Contains("USDT") || symbol.Contains("BTC") || symbol.Contains("ETH"))
            {
                return await FetchBinanceDataAsync(symbol);
            }
            else
            {
                // For traditional stocks, you could use Alpha Vantage or other APIs
                return await FetchStockDataAsync(symbol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch market data for symbol: {Symbol}", symbol);
            return null;
        }
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

    private async Task<MarketData?> FetchStockDataAsync(string symbol)
    {
        try
        {
            // Placeholder for stock data fetching
            // In a real implementation, you'd use Alpha Vantage, Yahoo Finance, or similar
            _logger.LogInformation("Stock data fetching not implemented for symbol: {Symbol}", symbol);
            
            // Return mock data for now
            return new MarketData
            {
                Symbol = symbol,
                Price = 100.0 + new Random().NextDouble() * 50, // Mock price
                Volume = 1000000 + new Random().Next(0, 5000000),
                Change24h = (new Random().NextDouble() - 0.5) * 10,
                ChangePercent24h = (new Random().NextDouble() - 0.5) * 5,
                High24h = 105.0 + new Random().NextDouble() * 45,
                Low24h = 95.0 + new Random().NextDouble() * 45,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock data for symbol: {Symbol}", symbol);
            return null;
        }
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
                return await FetchStockHistoricalDataAsync(symbol, limit);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch historical data for symbol: {Symbol}", symbol);
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

            var klineData = JsonSerializer.Deserialize<decimal[][]>(response.Content);
            if (klineData == null)
            {
                _logger.LogWarning("Failed to deserialize Binance kline data for {Symbol}", symbol);
                return null;
            }

            var historicalData = new List<MarketData>();
            foreach (var kline in klineData)
            {
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)kline[0]).DateTime;
                var open = (double)kline[1];
                var high = (double)kline[2];
                var low = (double)kline[3];
                var close = (double)kline[4];
                var volume = (double)kline[5];

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

    private async Task<List<MarketData>?> FetchStockHistoricalDataAsync(string symbol, int limit)
    {
        // Placeholder for stock historical data
        // Generate mock historical data
        var historicalData = new List<MarketData>();
        var random = new Random();
        var basePrice = 100.0;

        for (int i = limit; i > 0; i--)
        {
            var timestamp = DateTime.UtcNow.AddMinutes(-i * 5);
            var price = basePrice + (random.NextDouble() - 0.5) * 20;
            
            historicalData.Add(new MarketData
            {
                Symbol = symbol,
                Price = price,
                Volume = 50000 + random.Next(0, 100000),
                High24h = price + random.NextDouble() * 2,
                Low24h = price - random.NextDouble() * 2,
                Timestamp = timestamp
            });
        }

        return historicalData;
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
