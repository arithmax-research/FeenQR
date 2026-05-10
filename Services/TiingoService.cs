using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services;

/// <summary>
/// Tiingo API service for fetching company fundamentals and historical data
/// Provides access to 20+ years of daily stock data and fundamental metrics
/// Free tier: 50 unique symbols per hour
/// </summary>
public class TiingoService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TiingoService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string BaseUrl = "https://api.tiingo.com";

    public TiingoService(
        HttpClient httpClient,
        ILogger<TiingoService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _apiKey = _configuration["Tiingo:ApiKey"] ?? "";

        // Configure JSON deserializer
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _jsonOptions.Converters.Add(new FlexibleDecimalConverter());
        _jsonOptions.Converters.Add(new FlexibleNullableDecimalConverter());

        // Set user agent for API requests
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FeenQR/1.0");
    }

    /// <summary>
    /// Get company daily data with fundamental metrics from Tiingo
    /// Includes market data and valuation metrics
    /// </summary>
    public async Task<TiingoDailyData> GetDailyMetadataAsync(string symbol)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Tiingo API key not configured");
            return null;
        }

        try
        {
            // Tiingo endpoint: /tiingo/daily/{ticker}?token={token}
            var url = $"{BaseUrl}/tiingo/daily/{symbol.ToUpper()}?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Tiingo API error for {symbol}: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var metadata = JsonSerializer.Deserialize<TiingoDailyData>(content, _jsonOptions);
            
            if (metadata == null)
            {
                _logger.LogWarning($"No metadata found for {symbol} from Tiingo");
                return null;
            }

            _logger.LogDebug($"Successfully fetched Tiingo metadata for {symbol}");
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting daily metadata for {symbol} from Tiingo");
            return null;
        }
    }

    /// <summary>
    /// Get historical daily price data for a symbol
    /// Provides 20+ years of OHLCV data
    /// </summary>
    public async Task<List<TiingoDailyPrice>> GetHistoricalDataAsync(string symbol, int days = 60)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Tiingo API key not configured");
            return null;
        }

        try
        {
            var startDate = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd");
            var endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            
            // Tiingo endpoint: /tiingo/daily/{ticker}/prices?startDate={start}&endDate={end}&token={token}
            var url = $"{BaseUrl}/tiingo/daily/{symbol.ToUpper()}/prices?startDate={startDate}&endDate={endDate}&token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Tiingo historical API error for {symbol}: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var prices = JsonSerializer.Deserialize<List<TiingoDailyPrice>>(content, _jsonOptions);
            
            if (prices == null || prices.Count == 0)
            {
                _logger.LogWarning($"No historical data found for {symbol} from Tiingo");
                return new List<TiingoDailyPrice>();
            }

            _logger.LogDebug($"Successfully fetched {prices.Count} days of data for {symbol} from Tiingo");
            return prices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting historical data for {symbol} from Tiingo");
            return null;
        }
    }
}

/// <summary>
/// Tiingo daily metadata response
/// Maps to /tiingo/daily/{ticker} endpoint
/// </summary>
public class TiingoDailyData
{
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("updated")]
    public string Updated { get; set; }

    [JsonPropertyName("exchangeCode")]
    public string ExchangeCode { get; set; }

    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; }

    [JsonPropertyName("sector")]
    public string Sector { get; set; }

    [JsonPropertyName("industrySector")]
    public string IndustrySector { get; set; }

    [JsonPropertyName("industry")]
    public string Industry { get; set; }

    [JsonPropertyName("website")]
    public string Website { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("marketCap")]
    public decimal? MarketCap { get; set; }

    [JsonPropertyName("enterpriseValue")]
    public decimal? EnterpriseValue { get; set; }

    [JsonPropertyName("peRatio")]
    public decimal? PERatio { get; set; }

    [JsonPropertyName("pbRatio")]
    public decimal? PriceToBook { get; set; }

    [JsonPropertyName("trailingDividendYield")]
    public decimal? DividendYield { get; set; }

    [JsonPropertyName("forwardDividendYield")]
    public decimal? ForwardDividendYield { get; set; }

    [JsonPropertyName("dividendPerShare")]
    public decimal? DividendPerShare { get; set; }

    [JsonPropertyName("beta")]
    public decimal? Beta { get; set; }

    [JsonPropertyName("priceToSalesTrailing12Months")]
    public decimal? PriceToSales { get; set; }

    [JsonPropertyName("enterpriseValueOverEBITDA")]
    public decimal? EVToEBITDA { get; set; }

    [JsonPropertyName("enterpriseValueOverRevenue")]
    public decimal? EVToRevenue { get; set; }

    [JsonPropertyName("lastPrice")]
    public decimal? LastPrice { get; set; }

    [JsonPropertyName("fiftyTwoWeekHigh")]
    public decimal? FiftyTwoWeekHigh { get; set; }

    [JsonPropertyName("fiftyTwoWeekLow")]
    public decimal? FiftyTwoWeekLow { get; set; }

    [JsonPropertyName("fiftyTwoWeekPriceReturnDaily")]
    public decimal? FiftyTwoWeekReturn { get; set; }

    [JsonPropertyName("pegRatio")]
    public decimal? PEGRatio { get; set; }

    [JsonPropertyName("eps")]
    public decimal? EPS { get; set; }

    [JsonPropertyName("epsGrowthTTM")]
    public decimal? EPSGrowth { get; set; }

    [JsonPropertyName("sharesOutstanding")]
    public long? SharesOutstanding { get; set; }

    [JsonPropertyName("floatShares")]
    public long? FloatShares { get; set; }

    [JsonPropertyName("shortInterest")]
    public long? ShortInterest { get; set; }

    [JsonPropertyName("shortRatio")]
    public decimal? ShortRatio { get; set; }

    [JsonPropertyName("profitMargin")]
    public decimal? ProfitMargin { get; set; }

    [JsonPropertyName("operatingMarginTTM")]
    public decimal? OperatingMargin { get; set; }

    [JsonPropertyName("returnOnAssetsTTM")]
    public decimal? ROA { get; set; }

    [JsonPropertyName("returnOnEquityTTM")]
    public decimal? ROE { get; set; }

    [JsonPropertyName("debtToEquity")]
    public decimal? DebtToEquity { get; set; }

    [JsonPropertyName("currentRatio")]
    public decimal? CurrentRatio { get; set; }
}

/// <summary>
/// Tiingo daily price data
/// Maps to /tiingo/daily/{ticker}/prices endpoint
/// </summary>
public class TiingoDailyPrice
{
    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("close")]
    public decimal? Close { get; set; }

    [JsonPropertyName("high")]
    public decimal? High { get; set; }

    [JsonPropertyName("low")]
    public decimal? Low { get; set; }

    [JsonPropertyName("open")]
    public decimal? Open { get; set; }

    [JsonPropertyName("volume")]
    public long? Volume { get; set; }

    [JsonPropertyName("adjClose")]
    public decimal? AdjClose { get; set; }

    [JsonPropertyName("adjHigh")]
    public decimal? AdjHigh { get; set; }

    [JsonPropertyName("adjLow")]
    public decimal? AdjLow { get; set; }

    [JsonPropertyName("adjOpen")]
    public decimal? AdjOpen { get; set; }

    [JsonPropertyName("adjVolume")]
    public long? AdjVolume { get; set; }

    [JsonPropertyName("divCash")]
    public decimal? DividendCash { get; set; }

    [JsonPropertyName("splitFactor")]
    public decimal? SplitFactor { get; set; }
}
