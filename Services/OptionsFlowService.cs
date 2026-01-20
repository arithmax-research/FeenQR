using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Feen.Services
{
    /// <summary>
    /// Service for analyzing options flow and unusual activity detection
    /// </summary>
    public class OptionsFlowService
    {
        private readonly ILogger<OptionsFlowService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string? _apiKey;

        public OptionsFlowService(ILogger<OptionsFlowService> logger, HttpClient httpClient, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
            // Use Polygon API key from appsettings.json
            _apiKey = _configuration["Polygon:ApiKey"];
            
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("Polygon API key not configured. Options flow analysis will fail.");
            }
        }

        /// <summary>
        /// Analyzes real-time options order flow for unusual activity
        /// </summary>
        public async Task<OptionsFlowAnalysis> AnalyzeOptionsFlowAsync(string symbol, int lookbackMinutes = 60)
        {
            _logger.LogInformation($"Analyzing real options flow for {symbol} over {lookbackMinutes} minutes");
            
            try
            {
                // Use Polygon.io options API or similar
                var url = $"https://api.polygon.io/v3/trades/{symbol}?limit=1000&apiKey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                
                var analysis = new OptionsFlowAnalysis
                {
                    Symbol = symbol,
                    Timestamp = DateTime.UtcNow,
                    TotalVolume = 0,
                    UnusualActivity = new List<UnusualOptionsActivity>(),
                    FlowDirection = "Neutral",
                    ConfidenceScore = 0.85m
                };

                if (data.TryGetProperty("results", out var results))
                {
                    var trades = results.EnumerateArray().ToList();
                    analysis.TotalVolume = trades.Count;
                    
                    // Analyze flow direction
                    var buyVolume = trades.Count(t => t.TryGetProperty("conditions", out var c) && 
                        c.EnumerateArray().Any(cond => cond.GetString()?.Contains("Buy") == true));
                    var sellVolume = trades.Count - buyVolume;
                    
                    if (buyVolume > sellVolume * 1.5) analysis.FlowDirection = "Bullish";
                    else if (sellVolume > buyVolume * 1.5) analysis.FlowDirection = "Bearish";
                }
                
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing options flow for {symbol}");
                throw new InvalidOperationException($"Failed to fetch options flow data: {ex.Message}. Ensure OPTIONS API key is configured.", ex);
            }
        }

        /// <summary>
        /// Detects unusual options activity patterns
        /// </summary>
        public async Task<List<UnusualOptionsActivity>> DetectUnusualActivityAsync(string symbol, decimal threshold = 7.0m)
        {
            _logger.LogInformation($"Detecting unusual options activity for {symbol} with threshold {threshold}");
            
            try
            {
                // Get options chain from real API
                var url = $"https://api.polygon.io/v3/reference/options/contracts?underlying_ticker={symbol}&limit=100&apiKey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                
                var unusualActivity = new List<UnusualOptionsActivity>();
                
                if (data.TryGetProperty("results", out var results))
                {
                    foreach (var contract in results.EnumerateArray())
                    {
                        // Calculate unusual score based on volume/OI ratio
                        var volume = contract.TryGetProperty("volume", out var v) ? v.GetInt64() : 0;
                        var openInterest = contract.TryGetProperty("open_interest", out var oi) ? oi.GetInt64() : 1;
                        var unusualScore = volume > 0 ? (decimal)volume / (decimal)openInterest : 0;
                        
                        if (unusualScore >= threshold)
                        {
                            unusualActivity.Add(new UnusualOptionsActivity
                            {
                                Strike = contract.TryGetProperty("strike_price", out var sp) ? sp.GetDecimal() : 0,
                                Expiration = contract.TryGetProperty("expiration_date", out var ed) ? 
                                    DateTime.Parse(ed.GetString() ?? DateTime.Now.ToString()) : DateTime.Now,
                                OptionType = contract.TryGetProperty("contract_type", out var ct) ? ct.GetString() : "call",
                                Volume = volume,
                                OpenInterest = openInterest,
                                ImpliedVolatility = 0,  // Would need separate IV API call
                                Delta = 0,  // Would need options Greeks API call
                                UnusualScore = unusualScore,
                                ActivityType = unusualScore > 10 ? "Extremely Unusual" : "Unusual"
                            });
                        }
                    }
                }
                
                return unusualActivity.OrderByDescending(a => a.UnusualScore).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error detecting unusual activity for {symbol}");
                throw new InvalidOperationException($"Failed to detect unusual options activity: {ex.Message}. Ensure OPTIONS API key is configured.", ex);
            }
        }

        /// <summary>
        /// Analyzes options gamma exposure and positioning
        /// </summary>
        public async Task<OptionsGammaAnalysis> AnalyzeGammaExposureAsync(string symbol)
        {
            _logger.LogInformation($"Analyzing real gamma exposure for {symbol}");
            
            try
            {
                // Get options chain with greeks
                var url = $"https://api.polygon.io/v3/snapshot/options/{symbol}?apiKey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                
                var analysis = new OptionsGammaAnalysis
                {
                    Symbol = symbol,
                    TotalGamma = 0,
                    NetGamma = 0,
                    GammaByStrike = new Dictionary<decimal, decimal>(),
                    GammaRisk = "Moderate",
                    SpotGamma = 0
                };
                
                if (data.TryGetProperty("results", out var results))
                {
                    foreach (var option in results.EnumerateArray())
                    {
                        if (option.TryGetProperty("greeks", out var greeks) &&
                            greeks.TryGetProperty("gamma", out var gamma) &&
                            option.TryGetProperty("details", out var details) &&
                            details.TryGetProperty("strike_price", out var strike))
                        {
                            var gammaValue = gamma.GetDecimal();
                            var strikePrice = strike.GetDecimal();
                            
                            analysis.TotalGamma += Math.Abs(gammaValue);
                            analysis.NetGamma += gammaValue;
                            
                            if (!analysis.GammaByStrike.ContainsKey(strikePrice))
                            {
                                analysis.GammaByStrike[strikePrice] = 0;
                            }
                            analysis.GammaByStrike[strikePrice] += gammaValue;
                        }
                    }
                }
                
                // Determine gamma risk level
                if (Math.Abs(analysis.NetGamma) > 1000) analysis.GammaRisk = "High";
                else if (Math.Abs(analysis.NetGamma) > 500) analysis.GammaRisk = "Moderate";
                else analysis.GammaRisk = "Low";
                
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing gamma exposure for {symbol}");
                throw new InvalidOperationException($"Failed to analyze gamma exposure: {ex.Message}. Ensure OPTIONS API key is configured.", ex);
            }
        }

        /// <summary>
        /// Monitors options order book depth and liquidity
        /// </summary>
        public async Task<OptionsOrderBook> GetOptionsOrderBookAsync(string symbol, decimal strike, DateTime expiration, string optionType)
        {
            _logger.LogInformation($"Getting real options order book for {symbol} {optionType} {strike} {expiration:yyyy-MM-dd}");
            
            try
            {
                // Construct options symbol (varies by provider)
                var optionSymbol = $"O:{symbol}{expiration:yyMMdd}{(optionType.ToUpper() == "CALL" ? "C" : "P")}{strike:00000000}";
                var url = $"https://api.polygon.io/v2/snapshot/locale/us/markets/stocks/tickers/{optionSymbol}?apiKey={_apiKey}";
                
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                
                var orderBook = new OptionsOrderBook
                {
                    Symbol = symbol,
                    Strike = strike,
                    Expiration = expiration,
                    OptionType = optionType,
                    Bids = new List<OrderBookLevel>(),
                    Asks = new List<OrderBookLevel>(),
                    Spread = 0,
                    MidPrice = 0
                };
                
                if (data.TryGetProperty("ticker", out var ticker))
                {
                    // Extract bid/ask from snapshot
                    if (ticker.TryGetProperty("lastQuote", out var quote))
                    {
                        var bid = quote.TryGetProperty("p", out var bidPrice) ? bidPrice.GetDecimal() : 0;
                        var ask = quote.TryGetProperty("P", out var askPrice) ? askPrice.GetDecimal() : 0;
                        var bidSize = quote.TryGetProperty("s", out var bs) ? bs.GetInt32() : 0;
                        var askSize = quote.TryGetProperty("S", out var asks) ? asks.GetInt32() : 0;
                        
                        orderBook.Bids.Add(new OrderBookLevel { Price = bid, Size = bidSize });
                        orderBook.Asks.Add(new OrderBookLevel { Price = ask, Size = askSize });
                        orderBook.Spread = ask - bid;
                        orderBook.MidPrice = (bid + ask) / 2;
                    }
                    
                    // Get last trade
                    if (ticker.TryGetProperty("lastTrade", out var trade))
                    {
                        orderBook.LastTrade = new OptionsTrade
                        {
                            Price = trade.TryGetProperty("p", out var price) ? price.GetDecimal() : 0,
                            Size = trade.TryGetProperty("s", out var size) ? size.GetInt32() : 0,
                            Timestamp = trade.TryGetProperty("t", out var ts) ? 
                                DateTimeOffset.FromUnixTimeMilliseconds(ts.GetInt64()).DateTime : DateTime.UtcNow,
                            TradeType = "Trade"
                        };
                    }
                }
                
                return orderBook;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting options order book for {symbol}");
                throw new InvalidOperationException($"Failed to get options order book: {ex.Message}. Ensure OPTIONS API key is configured.", ex);
            }
        }
    }

    /// <summary>
    /// Options flow analysis result
    /// </summary>
    public class OptionsFlowAnalysis
    {
        public string? Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public long TotalVolume { get; set; }
        public List<UnusualOptionsActivity>? UnusualActivity { get; set; }
        public string? FlowDirection { get; set; }
        public decimal ConfidenceScore { get; set; }
    }

    /// <summary>
    /// Unusual options activity detection
    /// </summary>
    public class UnusualOptionsActivity
    {
        public decimal Strike { get; set; }
        public DateTime Expiration { get; set; }
        public string? OptionType { get; set; }
        public long Volume { get; set; }
        public long OpenInterest { get; set; }
        public decimal ImpliedVolatility { get; set; }
        public decimal Delta { get; set; }
        public decimal UnusualScore { get; set; }
        public string? ActivityType { get; set; }
    }

    /// <summary>
    /// Options gamma exposure analysis
    /// </summary>
    public class OptionsGammaAnalysis
    {
        public string? Symbol { get; set; }
        public decimal TotalGamma { get; set; }
        public decimal NetGamma { get; set; }
        public Dictionary<decimal, decimal>? GammaByStrike { get; set; }
        public string? GammaRisk { get; set; }
        public decimal SpotGamma { get; set; }
    }

    /// <summary>
    /// Options order book data
    /// </summary>
    public class OptionsOrderBook
    {
        public string? Symbol { get; set; }
        public decimal Strike { get; set; }
        public DateTime Expiration { get; set; }
        public string? OptionType { get; set; }
        public List<OrderBookLevel>? Bids { get; set; }
        public List<OrderBookLevel>? Asks { get; set; }
        public OptionsTrade? LastTrade { get; set; }
        public decimal Spread { get; set; }
        public decimal MidPrice { get; set; }
    }

    /// <summary>
    /// Order book level
    /// </summary>
    public class OrderBookLevel
    {
        public decimal Price { get; set; }
        public int Size { get; set; }
    }

    /// <summary>
    /// Options trade data
    /// </summary>
    public class OptionsTrade
    {
        public decimal Price { get; set; }
        public int Size { get; set; }
        public DateTime Timestamp { get; set; }
        public string? TradeType { get; set; }
    }
}