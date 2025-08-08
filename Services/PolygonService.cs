using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Polygon.io API service for market data, news, and fundamentals
    /// </summary>
    public class PolygonService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PolygonService> _logger;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.polygon.io";

        public PolygonService(HttpClient httpClient, ILogger<PolygonService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["Polygon:ApiKey"] ?? throw new ArgumentException("Polygon API key not configured");
        }

        /// <summary>
        /// Get previous day's market data for a stock (free tier available)
        /// </summary>
        public async Task<PolygonQuote?> GetQuoteAsync(string symbol)
        {
            try
            {
                // Use previous close endpoint which is available on free tier
                var url = $"{BaseUrl}/v2/aggs/ticker/{symbol}/prev?adjusted=true&apikey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<PolygonPrevCloseResponse>(response);
                
                if (result?.Results != null && result.Results.Count > 0)
                {
                    var data = result.Results[0];
                    return new PolygonQuote
                    {
                        Symbol = symbol,
                        Price = data.Close,
                        Size = data.Volume,
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(data.Timestamp).DateTime
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quote for {Symbol}. Note: Free tier has limited access to real-time data.", symbol);
                return null;
            }
        }

        /// <summary>
        /// Get daily market data for a stock
        /// </summary>
        public async Task<PolygonDailyBar?> GetDailyBarAsync(string symbol, DateTime date)
        {
            try
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                var url = $"{BaseUrl}/v1/open-close/{symbol}/{dateStr}?adjusted=true&apikey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<PolygonDailyBar>(response);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily bar for {Symbol} on {Date}", symbol, date);
                return null;
            }
        }

        /// <summary>
        /// Get historical bars (aggregates) for a stock
        /// </summary>
        public async Task<List<PolygonAggregateBar>> GetAggregatesAsync(
            string symbol, 
            int multiplier = 1, 
            string timespan = "day", 
            DateTime? from = null, 
            DateTime? to = null,
            int limit = 120)
        {
            try
            {
                from ??= DateTime.Now.AddDays(-365);
                to ??= DateTime.Now;
                
                var fromStr = from.Value.ToString("yyyy-MM-dd");
                var toStr = to.Value.ToString("yyyy-MM-dd");
                
                var url = $"{BaseUrl}/v2/aggs/ticker/{symbol}/range/{multiplier}/{timespan}/{fromStr}/{toStr}?adjusted=true&sort=asc&limit={limit}&apikey={_apiKey}";
                
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<PolygonAggregatesResponse>(response);
                
                return result?.Results ?? new List<PolygonAggregateBar>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting aggregates for {Symbol}", symbol);
                return new List<PolygonAggregateBar>();
            }
        }

        /// <summary>
        /// Get news articles
        /// </summary>
        public async Task<List<PolygonNewsArticle>> GetNewsAsync(
            string? ticker = null, 
            int limit = 10, 
            DateTime? publishedUtcGte = null)
        {
            try
            {
                var queryParams = new List<string> { $"limit={limit}", $"apikey={_apiKey}" };
                
                if (!string.IsNullOrEmpty(ticker))
                {
                    queryParams.Add($"ticker={ticker}");
                }
                
                if (publishedUtcGte.HasValue)
                {
                    queryParams.Add($"published_utc.gte={publishedUtcGte.Value:yyyy-MM-dd}");
                }
                
                var url = $"{BaseUrl}/v2/reference/news?{string.Join("&", queryParams)}";
                
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<PolygonNewsResponse>(response);
                
                return result?.Results ?? new List<PolygonNewsArticle>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting news for ticker {Ticker}", ticker);
                return new List<PolygonNewsArticle>();
            }
        }

        /// <summary>
        /// Get company financials
        /// </summary>
        public async Task<PolygonFinancials?> GetFinancialsAsync(string symbol, int limit = 4)
        {
            try
            {
                var url = $"{BaseUrl}/vX/reference/financials?ticker={symbol}&limit={limit}&apikey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<PolygonFinancialsResponse>(response);
                
                return result?.Results?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting financials for {Symbol}", symbol);
                return null;
            }
        }

        /// <summary>
        /// Get market status
        /// </summary>
        public async Task<PolygonMarketStatus?> GetMarketStatusAsync()
        {
            try
            {
                var url = $"{BaseUrl}/v1/marketstatus/now?apikey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<PolygonMarketStatus>(response);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting market status");
                return null;
            }
        }
    }

    // Data models for Polygon.io responses
    public class PolygonQuote
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public long Size { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class PolygonQuoteResponse
    {
        [JsonPropertyName("results")]
        public PolygonQuoteResult? Results { get; set; }
    }

    public class PolygonQuoteResult
    {
        [JsonPropertyName("p")]
        public decimal Price { get; set; }
        
        [JsonPropertyName("s")]
        public long Size { get; set; }
        
        [JsonPropertyName("t")]
        public long Timestamp { get; set; }
    }

    public class PolygonDailyBar
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;
        
        [JsonPropertyName("open")]
        public decimal Open { get; set; }
        
        [JsonPropertyName("high")]
        public decimal High { get; set; }
        
        [JsonPropertyName("low")]
        public decimal Low { get; set; }
        
        [JsonPropertyName("close")]
        public decimal Close { get; set; }
        
        [JsonPropertyName("volume")]
        public long Volume { get; set; }
        
        [JsonPropertyName("from")]
        public string Date { get; set; } = string.Empty;
    }

    public class PolygonAggregateBar
    {
        [JsonPropertyName("o")]
        public decimal Open { get; set; }
        
        [JsonPropertyName("h")]
        public decimal High { get; set; }
        
        [JsonPropertyName("l")]
        public decimal Low { get; set; }
        
        [JsonPropertyName("c")]
        public decimal Close { get; set; }
        
        [JsonPropertyName("v")]
        public long Volume { get; set; }
        
        [JsonPropertyName("t")]
        public long Timestamp { get; set; }
        
        [JsonPropertyName("n")]
        public int Transactions { get; set; }
    }

    public class PolygonAggregatesResponse
    {
        [JsonPropertyName("results")]
        public List<PolygonAggregateBar>? Results { get; set; }
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class PolygonNewsArticle
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("published_utc")]
        public DateTime PublishedUtc { get; set; }
        
        [JsonPropertyName("article_url")]
        public string ArticleUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;
        
        [JsonPropertyName("publisher")]
        public PolygonPublisher? Publisher { get; set; }
        
        [JsonPropertyName("tickers")]
        public List<string>? Tickers { get; set; }
    }

    public class PolygonPublisher
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("homepage_url")]
        public string HomepageUrl { get; set; } = string.Empty;
    }

    public class PolygonNewsResponse
    {
        [JsonPropertyName("results")]
        public List<PolygonNewsArticle>? Results { get; set; }
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class PolygonFinancials
    {
        [JsonPropertyName("ticker")]
        public string Ticker { get; set; } = string.Empty;
        
        [JsonPropertyName("period_of_report_date")]
        public DateTime PeriodOfReportDate { get; set; }
        
        [JsonPropertyName("financials")]
        public PolygonFinancialData? Financials { get; set; }
    }

    public class PolygonFinancialData
    {
        [JsonPropertyName("income_statement")]
        public PolygonIncomeStatement? IncomeStatement { get; set; }
        
        [JsonPropertyName("balance_sheet")]
        public PolygonBalanceSheet? BalanceSheet { get; set; }
        
        [JsonPropertyName("cash_flow_statement")]
        public PolygonCashFlowStatement? CashFlowStatement { get; set; }
    }

    public class PolygonIncomeStatement
    {
        [JsonPropertyName("revenues")]
        public PolygonFinancialValue? Revenues { get; set; }
        
        [JsonPropertyName("net_income_loss")]
        public PolygonFinancialValue? NetIncomeLoss { get; set; }
        
        [JsonPropertyName("basic_earnings_per_share")]
        public PolygonFinancialValue? BasicEarningsPerShare { get; set; }
    }

    public class PolygonBalanceSheet
    {
        [JsonPropertyName("equity")]
        public PolygonFinancialValue? Equity { get; set; }
        
        [JsonPropertyName("assets")]
        public PolygonFinancialValue? Assets { get; set; }
        
        [JsonPropertyName("liabilities")]
        public PolygonFinancialValue? Liabilities { get; set; }
    }

    public class PolygonCashFlowStatement
    {
        [JsonPropertyName("net_cash_flow_from_operating_activities")]
        public PolygonFinancialValue? OperatingCashFlow { get; set; }
        
        [JsonPropertyName("net_cash_flow_from_investing_activities")]
        public PolygonFinancialValue? InvestingCashFlow { get; set; }
        
        [JsonPropertyName("net_cash_flow_from_financing_activities")]
        public PolygonFinancialValue? FinancingCashFlow { get; set; }
    }

    public class PolygonFinancialValue
    {
        [JsonPropertyName("value")]
        public decimal Value { get; set; }
    }

    public class PolygonFinancialsResponse
    {
        [JsonPropertyName("results")]
        public List<PolygonFinancials>? Results { get; set; }
    }

    public class PolygonMarketStatus
    {
        [JsonPropertyName("market")]
        public string Market { get; set; } = string.Empty;
        
        [JsonPropertyName("serverTime")]
        public DateTime ServerTime { get; set; }
        
        [JsonPropertyName("exchanges")]
        public PolygonExchangeStatus? Exchanges { get; set; }
    }

    public class PolygonExchangeStatus
    {
        [JsonPropertyName("nasdaq")]
        public string Nasdaq { get; set; } = string.Empty;
        
        [JsonPropertyName("nyse")]
        public string Nyse { get; set; } = string.Empty;
        
        [JsonPropertyName("otc")]
        public string Otc { get; set; } = string.Empty;
    }

    public class PolygonPrevCloseResponse
    {
        [JsonPropertyName("ticker")]
        public string Ticker { get; set; } = string.Empty;
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonPropertyName("results")]
        public List<PolygonPrevCloseResult>? Results { get; set; }
    }

    public class PolygonPrevCloseResult
    {
        [JsonPropertyName("T")]
        public string Ticker { get; set; } = string.Empty;
        
        [JsonPropertyName("v")]
        public long Volume { get; set; }
        
        [JsonPropertyName("vw")]
        public decimal VolumeWeightedPrice { get; set; }
        
        [JsonPropertyName("o")]
        public decimal Open { get; set; }
        
        [JsonPropertyName("c")]
        public decimal Close { get; set; }
        
        [JsonPropertyName("h")]
        public decimal High { get; set; }
        
        [JsonPropertyName("l")]
        public decimal Low { get; set; }
        
        [JsonPropertyName("t")]
        public long Timestamp { get; set; }
        
        [JsonPropertyName("n")]
        public int NumberOfTransactions { get; set; }
    }
}
