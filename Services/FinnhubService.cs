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
/// Finnhub API service for fetching company fundamentals
/// Provides access to financial metrics, ratios, and company data with high reliability
/// Free tier: 60 API calls per minute
/// </summary>
public class FinnhubService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FinnhubService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string BaseUrl = "https://finnhub.io/api/v1";

    public FinnhubService(
        HttpClient httpClient,
        ILogger<FinnhubService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _apiKey = _configuration["Finnhub:ApiKey"] ?? "";

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
    /// Get comprehensive company metrics from Finnhub
    /// Returns Forward P/E, PEG Ratio, EV/EBITDA, and other key metrics
    /// </summary>
    public async Task<FinnhubMetrics> GetCompanyMetricsAsync(string symbol)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Finnhub API key not configured");
            return null;
        }

        try
        {
            // Finnhub endpoint: /stock/metric?symbol=AAPL&metric=all
            var url = $"{BaseUrl}/stock/metric?symbol={symbol}&metric=all&token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Finnhub API error for {symbol}: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var metrics = JsonSerializer.Deserialize<FinnhubMetrics>(content, _jsonOptions);
            
            if (metrics == null)
            {
                _logger.LogWarning($"No metrics found for {symbol} from Finnhub");
                return null;
            }

            _logger.LogDebug($"Successfully fetched Finnhub metrics for {symbol}");
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting metrics for {symbol} from Finnhub");
            return null;
        }
    }

    /// <summary>
    /// Get basic company financials (quote data with key ratios)
    /// Alternative endpoint for quick fundamental data
    /// </summary>
    public async Task<FinnhubQuote> GetQuoteAsync(string symbol)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Finnhub API key not configured");
            return null;
        }

        try
        {
            // Finnhub endpoint: /quote?symbol=AAPL
            var url = $"{BaseUrl}/quote?symbol={symbol}&token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Finnhub quote API error for {symbol}: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var quote = JsonSerializer.Deserialize<FinnhubQuote>(content, _jsonOptions);
            
            if (quote == null)
            {
                _logger.LogWarning($"No quote found for {symbol} from Finnhub");
                return null;
            }

            _logger.LogDebug($"Successfully fetched Finnhub quote for {symbol}");
            return quote;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting quote for {symbol} from Finnhub");
            return null;
        }
    }
}

/// <summary>
/// Finnhub comprehensive metrics response model
/// Maps to /stock/metric?symbol=X&metric=all endpoint
/// </summary>
public class FinnhubMetrics
{
    [JsonPropertyName("10DayAverageTradingVolume")]
    public decimal? TenDayAvgVolume { get; set; }

    [JsonPropertyName("52WeekHigh")]
    public decimal? FiftyTwoWeekHigh { get; set; }

    [JsonPropertyName("52WeekLow")]
    public decimal? FiftyTwoWeekLow { get; set; }

    [JsonPropertyName("52WeekHighDate")]
    public string FiftyTwoWeekHighDate { get; set; }

    [JsonPropertyName("52WeekLowDate")]
    public string FiftyTwoWeekLowDate { get; set; }

    [JsonPropertyName("52WeekPriceReturnDaily")]
    public decimal? FiftyTwoWeekReturn { get; set; }

    [JsonPropertyName("beta")]
    public decimal? Beta { get; set; }

    [JsonPropertyName("dividendYield")]
    public decimal? DividendYield { get; set; }

    [JsonPropertyName("eps")]
    public decimal? EPS { get; set; }

    [JsonPropertyName("ev")]
    public decimal? EnterpriseValue { get; set; }

    [JsonPropertyName("evToEbitda")]
    public decimal? EVToEBITDA { get; set; }

    [JsonPropertyName("evToRevenue")]
    public decimal? EVToRevenue { get; set; }

    [JsonPropertyName("marketCapitalization")]
    public decimal? MarketCap { get; set; }

    [JsonPropertyName("netDebt")]
    public decimal? NetDebt { get; set; }

    [JsonPropertyName("pbRatio")]
    public decimal? PriceToBook { get; set; }

    [JsonPropertyName("peRatio")]
    public decimal? PERatio { get; set; }

    [JsonPropertyName("peg")]
    public decimal? PEGRatio { get; set; }

    [JsonPropertyName("psRatio")]
    public decimal? PriceToSales { get; set; }

    [JsonPropertyName("currentRatio")]
    public decimal? CurrentRatio { get; set; }

    [JsonPropertyName("debtToEquity")]
    public decimal? DebtToEquity { get; set; }

    [JsonPropertyName("grossMargin")]
    public decimal? GrossMargin { get; set; }

    [JsonPropertyName("operatingMargin")]
    public decimal? OperatingMargin { get; set; }

    [JsonPropertyName("netMargin")]
    public decimal? NetMargin { get; set; }

    [JsonPropertyName("roe")]
    public decimal? ROE { get; set; }

    [JsonPropertyName("roa")]
    public decimal? ROA { get; set; }

    [JsonPropertyName("roic")]
    public decimal? ROIC { get; set; }

    [JsonPropertyName("forwardPE")]
    public decimal? ForwardPE { get; set; }

    [JsonPropertyName("revenuePerShare")]
    public decimal? RevenuePerShare { get; set; }

    [JsonPropertyName("bookValuePerShare")]
    public decimal? BookValuePerShare { get; set; }

    [JsonPropertyName("cashFlowPerShare")]
    public decimal? CashFlowPerShare { get; set; }

    [JsonPropertyName("dividendPerShare")]
    public decimal? DividendPerShare { get; set; }
}

/// <summary>
/// Finnhub quote response model
/// Maps to /quote?symbol=X endpoint
/// </summary>
public class FinnhubQuote
{
    [JsonPropertyName("c")]
    public decimal? CurrentPrice { get; set; }

    [JsonPropertyName("h")]
    public decimal? High { get; set; }

    [JsonPropertyName("l")]
    public decimal? Low { get; set; }

    [JsonPropertyName("o")]
    public decimal? Open { get; set; }

    [JsonPropertyName("pc")]
    public decimal? PreviousClose { get; set; }

    [JsonPropertyName("t")]
    public long? Timestamp { get; set; }
}
