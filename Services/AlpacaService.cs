using Alpaca.Markets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Collections.Concurrent;

namespace QuantResearchAgent.Services;

public class AlpacaService
{
    private readonly ILogger<AlpacaService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAlpacaTradingClient? _tradingClient;
    private readonly IAlpacaDataClient? _dataClient;
    private readonly LeanDataService _leanDataService;
    private readonly ConcurrentDictionary<string, List<IBar>> _historicalDataCache = new();

    public AlpacaService(ILogger<AlpacaService> logger, IConfiguration configuration, LeanDataService leanDataService)
    {
        _logger = logger;
        _configuration = configuration;
        _leanDataService = leanDataService;

        var apiKey = configuration["Alpaca:ApiKey"];
        var secretKey = configuration["Alpaca:SecretKey"];
        var isPaperTrading = configuration.GetValue<bool>("Alpaca:IsPaperTrading", true);

        _logger.LogInformation("Initializing Alpaca service with API Key: {ApiKey}, IsPaper: {IsPaper}", 
            string.IsNullOrEmpty(apiKey) ? "NOT_SET" : $"{apiKey[..8]}...", isPaperTrading);

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey) || 
            apiKey == "YOUR_ALPACA_API_KEY" || secretKey == "YOUR_ALPACA_SECRET_KEY")
        {
            _logger.LogWarning("Alpaca API keys not configured. Using demo mode.");
            return;
        }

        try
        {
            var environment = isPaperTrading ? Alpaca.Markets.Environments.Paper : Alpaca.Markets.Environments.Live;

            _tradingClient = environment.GetAlpacaTradingClient(new SecretKey(apiKey, secretKey));
            _dataClient = environment.GetAlpacaDataClient(new SecretKey(apiKey, secretKey));

            _logger.LogInformation("Alpaca service initialized successfully in {Environment} mode", isPaperTrading ? "Paper" : "Live");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Alpaca clients");
        }
    }

    public async Task<MarketData?> GetMarketDataAsync(string symbol)
    {
        try
        {
            var cleanSymbol = symbol.Trim().ToUpper();
            if (_dataClient == null)
            {
                _logger.LogWarning("Alpaca client not initialized. Cannot fetch market data for {Symbol}", cleanSymbol);
                return await GetLeanMarketDataFallback(cleanSymbol);
            }

            _logger.LogDebug("Requesting Alpaca market data for symbol: {Symbol}", cleanSymbol);

            try
            {
                // Get latest quote for real-time price
                var quote = await _dataClient.GetLatestQuoteAsync(new LatestMarketDataRequest(cleanSymbol));
                _logger.LogDebug("Alpaca quote response: {Quote}", quote != null ? $"Bid={quote.BidPrice}, Ask={quote.AskPrice}, Timestamp={quote.TimestampUtc}" : "null");

                // Get latest trade for volume and price confirmation
                var trade = await _dataClient.GetLatestTradeAsync(new LatestMarketDataRequest(cleanSymbol));
                _logger.LogDebug("Alpaca trade response: {Trade}", trade != null ? $"Price={trade.Price}, Size={trade.Size}, Timestamp={trade.TimestampUtc}" : "null");

                // Get recent bars for daily high/low - Use proper historical data with 15+ minute delay
                var endTime = DateTime.UtcNow.AddMinutes(-20); // 20 minute delay to ensure data availability
                var startTime = endTime.AddDays(-5); // Get last 5 days to ensure we have data
                
                var barsRequest = new HistoricalBarsRequest(
                    cleanSymbol,
                    startTime,
                    endTime,
                    BarTimeFrame.Day);

                var barsResponse = await _dataClient.GetHistoricalBarsAsync(barsRequest);
                _logger.LogDebug("Alpaca bars response: {BarsCount}", barsResponse.Items.ContainsKey(cleanSymbol) ? barsResponse.Items[cleanSymbol].Count : 0);
                var bars = barsResponse.Items.ContainsKey(cleanSymbol) ? barsResponse.Items[cleanSymbol].ToList() : new List<IBar>();

                var latestBar = bars.LastOrDefault();
                var previousBar = bars.Count > 1 ? bars[bars.Count - 2] : null;

                // Prefer trade price, then quote, then bar close
                var currentPrice = trade?.Price ?? quote?.BidPrice ?? quote?.AskPrice ?? latestBar?.Close ?? 0;
                var previousClose = previousBar?.Close ?? latestBar?.Open ?? currentPrice;
                var change24h = currentPrice - previousClose;

                // Log detailed information for debugging
                _logger.LogInformation("Alpaca data for {Symbol}: Quote={QuoteAvailable}, Trade={TradeAvailable}, Bars={BarCount}, CurrentPrice={Price}", 
                    cleanSymbol, quote != null, trade != null, bars.Count, currentPrice);

                if (currentPrice == 0 && latestBar == null)
                {
                    _logger.LogWarning("Alpaca returned no usable data for {Symbol}. Quote: {Quote}, Trade: {Trade}, Bars: {BarCount}", 
                        cleanSymbol, quote != null ? "Available" : "null", trade != null ? "Available" : "null", bars.Count);
                    return await GetLeanMarketDataFallback(cleanSymbol);
                }

                // Use bar data if real-time data is not available
                if (currentPrice == 0 && latestBar != null)
                {
                    currentPrice = latestBar.Close;
                    _logger.LogInformation("Using bar close price {Price} for {Symbol} as real-time data not available", currentPrice, cleanSymbol);
                }

                var marketData = new MarketData
                {
                    Symbol = cleanSymbol,
                    Source = "Alpaca",
                    Price = (double)currentPrice,
                    Volume = (double)(latestBar?.Volume ?? trade?.Size ?? 0),
                    High24h = (double)(latestBar?.High ?? currentPrice),
                    Low24h = (double)(latestBar?.Low ?? currentPrice),
                    Change24h = (double)change24h,
                    ChangePercent24h = previousClose != 0 ? (double)((change24h / previousClose) * 100) : 0,
                    Timestamp = trade?.TimestampUtc ?? quote?.TimestampUtc ?? latestBar?.TimeUtc ?? DateTime.UtcNow
                };

                _logger.LogInformation("Successfully fetched market data for {Symbol}: Price=${Price:F2}, Change={Change:F2}%", 
                    cleanSymbol, marketData.Price, marketData.ChangePercent24h);

                return marketData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching market data for {Symbol}. Exception: {Message}", symbol, ex.Message);
                return await GetLeanMarketDataFallback(cleanSymbol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market data for {Symbol}. Exception: {Message}", symbol, ex.Message);
            return await GetLeanMarketDataFallback(symbol.Trim().ToUpper());
        }

    }

    public async Task<List<IBar>> GetHistoricalBarsAsync(string symbol, int days = 30, BarTimeFrame? timeFrame = null)
    {
        try
        {
            // First try to get data from Alpaca API if client is initialized
            if (_dataClient != null)
            {
                _logger.LogInformation("Attempting to fetch data from Alpaca API for {Symbol}", symbol);
                var alpacaBars = await GetAlpacaHistoricalBarsAsync(symbol, days, timeFrame);
                
                if (alpacaBars.Any())
                {
                    _logger.LogInformation("Successfully fetched {Count} bars from Alpaca API for {Symbol}", alpacaBars.Count, symbol);
                    return alpacaBars;
                }
                
                _logger.LogWarning("No data returned from Alpaca API for {Symbol}, trying Lean data fallback", symbol);
            }
            else
            {
                _logger.LogInformation("Alpaca client not initialized, using Lean data for {Symbol}", symbol);
            }

            // Fallback to Lean data
            return await GetLeanHistoricalBarsAsync(symbol, days);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetHistoricalBarsAsync for {Symbol}, trying Lean fallback", symbol);
            return await GetLeanHistoricalBarsAsync(symbol, days);
        }
    }

    private async Task<List<IBar>> GetAlpacaHistoricalBarsAsync(string symbol, int days = 30, BarTimeFrame? timeFrame = null)
    {
        try
        {
            var cacheKey = $"{symbol}_{days}_{timeFrame?.ToString() ?? "1D"}";
            if (_historicalDataCache.TryGetValue(cacheKey, out var cached))
            {
                // Return cached data if it's less than 5 minutes old
                var lastBar = cached.LastOrDefault();
                if (lastBar != null && DateTime.UtcNow - lastBar.TimeUtc < TimeSpan.FromMinutes(5))
                {
                    _logger.LogInformation("Returning cached Alpaca data for {Symbol}, {Count} bars", symbol, cached.Count);
                    return cached;
                }
            }

            // Extend the date range to account for weekends and holidays
            // Use endDate with 20+ minute delay to ensure data availability (Free plan requirement)
            var endDate = DateTime.UtcNow.AddMinutes(-20);
            var startDate = endDate.AddDays(-(days + 10)); // Add extra days to ensure we get enough data

            _logger.LogInformation("Fetching historical data for {Symbol} from {StartDate} to {EndDate} (20min delay for data availability)", 
                symbol, startDate, endDate);

            var request = new HistoricalBarsRequest(
                symbol,
                startDate,
                endDate,
                timeFrame ?? BarTimeFrame.Day);

            _logger.LogDebug("Making Alpaca API request for {Symbol} with timeframe {TimeFrame}", symbol, timeFrame ?? BarTimeFrame.Day);

            try
            {
                var response = await _dataClient!.GetHistoricalBarsAsync(request);
                _logger.LogDebug("Alpaca API response received. Found data for {SymbolCount} symbols: {Symbols}", 
                    response.Items.Count, string.Join(", ", response.Items.Keys));

                if (!response.Items.ContainsKey(symbol))
                {
                    _logger.LogWarning("No historical data found in response for symbol {Symbol}. Available symbols: {AvailableSymbols}", 
                        symbol, string.Join(", ", response.Items.Keys));
                    return await GetLeanHistoricalBarsFallback(symbol, days);
                }

                var allBars = response.Items[symbol].ToList();
                // Take only the requested number of most recent bars
                var bars = allBars.OrderByDescending(b => b.TimeUtc).Take(days).OrderBy(b => b.TimeUtc).ToList();

                _historicalDataCache[cacheKey] = bars;
                _logger.LogInformation("Fetched {TotalBars} total bars, returning {RequestedBars} most recent bars for {Symbol}", 
                    allBars.Count, bars.Count, symbol);

                if (bars.Count == 0)
                {
                    _logger.LogWarning("Historical data request returned 0 bars for {Symbol}. Market might be closed or symbol invalid.", symbol);
                    // Log detailed response info for debugging
                    _logger.LogDebug("Response contained {SymbolCount} symbols: {Symbols}", 
                        response.Items.Count, string.Join(", ", response.Items.Keys));
                    return await GetLeanHistoricalBarsFallback(symbol, days);
                }

                return bars;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching historical bars from Alpaca for {Symbol}: {Message}", symbol, ex.Message);
                return await GetLeanHistoricalBarsFallback(symbol, days);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching historical bars from Alpaca for {Symbol}: {Message}", symbol, ex.Message);
            return await GetLeanHistoricalBarsFallback(symbol, days);
        }

    }

    private async Task<List<IBar>> GetLeanHistoricalBarsFallback(string symbol, int days)
    {
        if (_leanDataService != null)
        {
            var hasLocalData = await _leanDataService.HasDataForSymbolAsync(symbol, false);
            if (hasLocalData)
            {
                var bars = await _leanDataService.GetEquityBarsAsync(symbol, "daily", days);
                if (bars != null && bars.Count > 0)
                {
                    _logger.LogInformation($"Fetched {bars.Count} bars for {symbol} from local Lean data (fallback)");
                    // If LeanBar implements IBar, cast directly; otherwise, map manually
                    return bars.Select(b => new AlpacaBar
                    {
                        Symbol = symbol,
                        TimeUtc = b.Time,
                        Open = (decimal)b.Open,
                        High = (decimal)b.High,
                        Low = (decimal)b.Low,
                        Close = (decimal)b.Close,
                        Volume = b.Volume
                    }).Cast<IBar>().ToList();
                }
            }
        }
        _logger.LogWarning($"No Lean data found for {symbol} (historical fallback)");
        return new List<IBar>();
    }

    private async Task<MarketData?> GetLeanMarketDataFallback(string symbol)
    {
        if (_leanDataService != null)
        {
            var hasLocalData = await _leanDataService.HasDataForSymbolAsync(symbol, false);
            if (hasLocalData)
            {
                var bars = await _leanDataService.GetEquityBarsAsync(symbol, "daily", 1);
                var latestBar = bars.LastOrDefault();
                if (latestBar != null)
                {
                    _logger.LogInformation($"Fetched market data for {symbol} from local Lean data (fallback)");
                    return new MarketData
                    {
                        Symbol = symbol,
                        Price = (double)latestBar.Close,
                        Volume = latestBar.Volume,
                        High24h = (double)latestBar.High,
                        Low24h = (double)latestBar.Low,
                        Change24h = (double)(latestBar.Close - latestBar.Open),
                        Timestamp = latestBar.Time
                    };
                }
            }
        }
        _logger.LogWarning($"No Lean data found for {symbol} (fallback)");
        return null;
    }

    // Removed duplicate GetHistoricalBarsAsync

    // Removed duplicate GetAlpacaHistoricalBarsAsync

    private async Task<List<IBar>> GetLeanHistoricalBarsAsync(string symbol, int days = 30)
    {
        try
        {
            _logger.LogInformation("Fetching data from Lean data files for {Symbol}", symbol);
            var hasEquityData = await _leanDataService.HasDataForSymbolAsync(symbol, false);
            if (hasEquityData)
            {
                var leanBars = await _leanDataService.GetEquityBarsAsync(symbol, "daily", days);
                if (leanBars != null && leanBars.Count > 0)
                {
                    var bars = leanBars.Select(lb => new AlpacaBar
                    {
                        Symbol = lb.Symbol,
                        TimeUtc = lb.Time,
                        Open = lb.Open,
                        High = lb.High,
                        Low = lb.Low,
                        Close = lb.Close,
                        Volume = lb.Volume
                    }).Cast<IBar>().ToList();
                    _logger.LogInformation("Converted {Count} Lean bars to Alpaca format for {Symbol}", bars.Count, symbol);
                    return bars;
                }
            }
            _logger.LogWarning("No Lean equity data found for {Symbol}", symbol);
            return new List<IBar>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from Lean files for {Symbol}", symbol);
            return new List<IBar>();
        }
    }

    public async Task<List<MarketData>> GetMultipleSymbolDataAsync(params string[] symbols)
    {
        var tasks = symbols.Select(GetMarketDataAsync);
        var results = await Task.WhenAll(tasks);
        
        return results.Where(r => r != null).ToList()!;
    }

    public async Task<IAccount?> GetAccountInfoAsync()
    {
        try
        {
            if (_tradingClient == null)
            {
                _logger.LogWarning("Alpaca trading client not initialized");
                return null;
            }

            return await _tradingClient.GetAccountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching account information");
            return null;
        }
    }

    public async Task<List<IPosition>> GetPositionsAsync()
    {
        try
        {
            if (_tradingClient == null)
            {
                _logger.LogWarning("Alpaca trading client not initialized");
                return new List<IPosition>();
            }

            var positions = await _tradingClient.ListPositionsAsync();
            return positions.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching positions");
            return new List<IPosition>();
        }
    }

    public async Task<IOrder?> PlaceOrderAsync(string symbol, OrderQuantity quantity, OrderSide side, OrderType orderType, TimeInForce timeInForce)
    {
        try
        {
            if (_tradingClient == null)
            {
                _logger.LogWarning("Alpaca trading client not initialized");
                return null;
            }

            var request = new NewOrderRequest(symbol, quantity, side, orderType, timeInForce);
            var order = await _tradingClient.PostOrderAsync(request);
            
            _logger.LogInformation("Order placed: {Symbol} {Side} {Quantity} at {Type}", symbol, side, quantity, orderType);
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order for {Symbol}", symbol);
            return null;
        }
    }
}

// Simple implementation of IBar for Lean data
public class AlpacaBar : IBar
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime TimeUtc { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public decimal Vwap { get; set; }
    public ulong TradeCount { get; set; }
}
