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
    private readonly AchestService? _achest;
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string BaseUrl = "https://finnhub.io/api/v1";

    public FinnhubService(
        HttpClient httpClient,
        ILogger<FinnhubService> logger,
        IConfiguration configuration,
        AchestService? achest = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _achest = achest;
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
        // Preferred source: Arithmax Chest (Eulerpool financial metrics).
        if (_achest != null)
        {
            try
            {
                var metrics = await _achest.GetFinancialMetricsAsync(symbol);
                if (metrics.HasValue && metrics.Value.ValueKind == JsonValueKind.Object)
                {
                    return MapMetricsToFinnhub(metrics.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest metrics fallback for {Symbol}", symbol);
            }
        }

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
        // Preferred source: Arithmax Chest unified OHLCV API.
        if (_achest != null)
        {
            try
            {
                var bars = await _achest.GetBarsAsync(symbol, DateTime.UtcNow.Date.AddDays(-14), DateTime.UtcNow.Date.AddDays(1));
                if (bars.Count > 0)
                {
                    var last = bars[^1];
                    var prev = bars.Count > 1 ? bars[^2].Close : last.Open;
                    return new FinnhubQuote
                    {
                        CurrentPrice = (decimal)last.Close,
                        High = (decimal)last.High,
                        Low = (decimal)last.Low,
                        Open = (decimal)last.Open,
                        PreviousClose = (decimal)prev,
                        Timestamp = new DateTimeOffset(last.Timestamp).ToUnixTimeSeconds()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest quote fallback for {Symbol}", symbol);
            }
        }

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

    /// <summary>Maps an achest Eulerpool financial-metrics JSON object onto Finnhub metrics.</summary>
    private static FinnhubMetrics MapMetricsToFinnhub(JsonElement m)
    {
        static JsonElement? Sub(JsonElement el, string name) =>
            el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object
                ? v : (JsonElement?)null;

        static decimal? Dec(JsonElement? el, string name, decimal scale = 1m)
        {
            if (!el.HasValue || !el.Value.TryGetProperty(name, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.Number => (decimal)v.GetDouble() * scale,
                JsonValueKind.String when decimal.TryParse(v.GetString(), out var d) => d * scale,
                _ => null
            };
        }

        var valuation = Sub(m, "valuation");
        var profitability = Sub(m, "profitability");
        var perShare = Sub(m, "perShare");
        var leverage = Sub(m, "leverage");

        // FeenQR stores these ratio fields as FRACTIONS (UI multiplies by 100).
        // achest: margins & dividendYield are percentages (÷100); ROE/ROA/ROIC are fractions.
        const decimal PctToFraction = 0.01m;

        return new FinnhubMetrics
        {
            PERatio = Dec(valuation, "pe"),
            PriceToSales = Dec(valuation, "ps"),
            PriceToBook = Dec(valuation, "pb"),
            EVToEBITDA = Dec(valuation, "evEbitda"),
            MarketCap = Dec(valuation, "marketCap"),
            EnterpriseValue = Dec(valuation, "enterpriseValue"),
            ROE = Dec(profitability, "roe"),
            ROA = Dec(profitability, "roa"),
            ROIC = Dec(profitability, "roic"),
            GrossMargin = Dec(profitability, "grossMargin") * PctToFraction,
            OperatingMargin = Dec(profitability, "operatingMargin") * PctToFraction,
            NetMargin = Dec(profitability, "netMargin") * PctToFraction,
            EPS = Dec(perShare, "eps"),
            DividendPerShare = Dec(perShare, "dps"),
            DividendYield = Dec(perShare, "dividendYield") * PctToFraction,
            RevenuePerShare = Dec(perShare, "sps"),
            BookValuePerShare = Dec(perShare, "bps"),
            DebtToEquity = Dec(leverage, "debtToEquity"),
            NetDebt = Dec(leverage, "netDebt"),
            CurrentRatio = Dec(leverage, "currentRatio")
        };
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
