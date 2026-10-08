using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

/// <summary>
/// Alpha Vantage API service for free financial data and fundamental analysis
/// Provides access to stock quotes, fundamentals, technical indicators, and forex data
/// </summary>
public class AlphaVantageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlphaVantageService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AchestService? _achest;
    private readonly string _apiKey;

    public AlphaVantageService(
        HttpClient httpClient,
        ILogger<AlphaVantageService> logger,
        IConfiguration configuration,
        AchestService? achest = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _achest = achest;
        _apiKey = _configuration["AlphaVantage:ApiKey"] ?? "demo";

        // Set user agent for API requests
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FeenQR/1.0");
    }

    /// <summary>
    /// Get real-time stock quote
    /// </summary>
    public async Task<AlphaVantageQuote> GetQuoteAsync(string symbol)
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
                    var change = last.Close - prev;
                    var changePct = prev != 0 ? (change / prev) * 100 : 0;
                    return new AlphaVantageQuote
                    {
                        Symbol = symbol,
                        Price = (decimal)last.Close,
                        Change = (decimal)change,
                        ChangePercent = $"{changePct:F4}%",
                        Volume = (long)last.Volume,
                        High = (decimal)last.High,
                        Low = (decimal)last.Low
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest quote fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageQuoteResponse>(content);

            return data?.GlobalQuote;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting quote for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get company overview (fundamental data)
    /// </summary>
    public async Task<AlphaVantageCompanyOverview> GetCompanyOverviewAsync(string symbol)
    {
        // Preferred source: Arithmax Chest (Eulerpool overview + metrics).
        if (_achest != null)
        {
            try
            {
                var profile = await _achest.GetCompanyProfileAsync(symbol);
                var overview = await _achest.GetFinancialMetricsAsync(symbol);
                if (overview.HasValue && overview.Value.ValueKind == JsonValueKind.Object)
                {
                    return MapOverviewToAlphaVantage(symbol, profile, overview.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest overview fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://www.alphavantage.co/query?function=OVERVIEW&symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                throw new HttpRequestException($"Alpha Vantage API error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AlphaVantageCompanyOverview>(content) 
                ?? throw new InvalidOperationException("Failed to deserialize company overview");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting company overview for {symbol}");
            throw;
        }
    }

    /// <summary>
    /// Get income statement data
    /// </summary>
    public async Task<List<AlphaVantageIncomeStatement>> GetIncomeStatementAsync(string symbol)
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=INCOME_STATEMENT&symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageIncomeStatement>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageIncomeResponse>(content);

            return data?.AnnualReports ?? new List<AlphaVantageIncomeStatement>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting income statement for {symbol}");
            return new List<AlphaVantageIncomeStatement>();
        }
    }

    /// <summary>
    /// Get balance sheet data
    /// </summary>
    public async Task<List<AlphaVantageBalanceSheet>> GetBalanceSheetAsync(string symbol)
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=BALANCE_SHEET&symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageBalanceSheet>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageBalanceResponse>(content);

            return data?.AnnualReports ?? new List<AlphaVantageBalanceSheet>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting balance sheet for {symbol}");
            return new List<AlphaVantageBalanceSheet>();
        }
    }

    /// <summary>
    /// Get cash flow statement data
    /// </summary>
    public async Task<List<AlphaVantageCashFlow>> GetCashFlowAsync(string symbol)
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=CASH_FLOW&symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageCashFlow>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageCashFlowResponse>(content);

            return data?.AnnualReports ?? new List<AlphaVantageCashFlow>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting cash flow for {symbol}");
            return new List<AlphaVantageCashFlow>();
        }
    }

    /// <summary>
    /// Get earnings data
    /// </summary>
    public async Task<List<AlphaVantageEarnings>> GetEarningsAsync(string symbol)
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=EARNINGS&symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageEarnings>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageEarningsResponse>(content);

            return data?.AnnualEarnings ?? new List<AlphaVantageEarnings>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting earnings for {symbol}");
            return new List<AlphaVantageEarnings>();
        }
    }

    /// <summary>
    /// Get technical indicator (SMA)
    /// </summary>
    public async Task<List<AlphaVantageTechnicalData>> GetSMAAsync(string symbol, int period = 20, string interval = "daily")
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=SMA&symbol={symbol}&interval={interval}&time_period={period}&series_type=close&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageTechnicalData>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageTechnicalResponse>(content);

            return data?.TechnicalAnalysis?.Values.ToList() ?? new List<AlphaVantageTechnicalData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting SMA for {symbol}");
            return new List<AlphaVantageTechnicalData>();
        }
    }

    /// <summary>
    /// Get forex exchange rate
    /// </summary>
    public async Task<AlphaVantageForexRate> GetForexRateAsync(string fromCurrency, string toCurrency)
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=CURRENCY_EXCHANGE_RATE&from_currency={fromCurrency}&to_currency={toCurrency}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageForexResponse>(content);

            return data?.RealtimeCurrencyExchangeRate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting forex rate for {fromCurrency}/{toCurrency}");
            return null;
        }
    }

    /// <summary>
    /// Get technical indicator (EMA)
    /// </summary>
    public async Task<List<AlphaVantageTechnicalData>> GetEMAAsync(string symbol, int period = 20, string interval = "daily")
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=EMA&symbol={symbol}&interval={interval}&time_period={period}&series_type=close&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageTechnicalData>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageTechnicalResponse>(content);

            return data?.TechnicalAnalysis?.Values.ToList() ?? new List<AlphaVantageTechnicalData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting EMA for {symbol}");
            return new List<AlphaVantageTechnicalData>();
        }
    }

    /// <summary>
    /// Get technical indicator (RSI)
    /// </summary>
    public async Task<List<AlphaVantageTechnicalData>> GetRSIAsync(string symbol, int period = 14, string interval = "daily")
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=RSI&symbol={symbol}&interval={interval}&time_period={period}&series_type=close&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageTechnicalData>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageTechnicalResponse>(content);

            return data?.TechnicalAnalysis?.Values.ToList() ?? new List<AlphaVantageTechnicalData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting RSI for {symbol}");
            return new List<AlphaVantageTechnicalData>();
        }
    }

    /// <summary>
    /// Get technical indicator (MACD)
    /// </summary>
    public async Task<List<AlphaVantageTechnicalData>> GetMACDAsync(string symbol, string interval = "daily")
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=MACD&symbol={symbol}&interval={interval}&series_type=close&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageTechnicalData>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageTechnicalResponse>(content);

            return data?.TechnicalAnalysis?.Values.ToList() ?? new List<AlphaVantageTechnicalData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting MACD for {symbol}");
            return new List<AlphaVantageTechnicalData>();
        }
    }

    /// <summary>
    /// Get technical indicator (Bollinger Bands)
    /// </summary>
    public async Task<List<AlphaVantageTechnicalData>> GetBollingerBandsAsync(string symbol, int period = 20, string interval = "daily")
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=BBANDS&symbol={symbol}&interval={interval}&time_period={period}&series_type=close&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageTechnicalData>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageTechnicalResponse>(content);

            return data?.TechnicalAnalysis?.Values.ToList() ?? new List<AlphaVantageTechnicalData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting Bollinger Bands for {symbol}");
            return new List<AlphaVantageTechnicalData>();
        }
    }

    /// <summary>
    /// Get technical indicator (Stochastic)
    /// </summary>
    public async Task<List<AlphaVantageTechnicalData>> GetStochasticAsync(string symbol, string interval = "daily")
    {
        try
        {
            var url = $"https://www.alphavantage.co/query?function=STOCH&symbol={symbol}&interval={interval}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return new List<AlphaVantageTechnicalData>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AlphaVantageTechnicalResponse>(content);

            return data?.TechnicalAnalysis?.Values.ToList() ?? new List<AlphaVantageTechnicalData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting Stochastic for {symbol}");
            return new List<AlphaVantageTechnicalData>();
        }
    }

    /// <summary>Maps achest Eulerpool profile + metrics onto Alpha Vantage's overview DTO.</summary>
    private static AlphaVantageCompanyOverview MapOverviewToAlphaVantage(string symbol, JsonElement? profile, JsonElement m)
    {
        static string S(JsonElement? el, string name)
        {
            if (!el.HasValue || el.Value.ValueKind != JsonValueKind.Object) return null;
            if (!el.Value.TryGetProperty(name, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => null
            };
        }

        static JsonElement? Sub(JsonElement el, string name) =>
            el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object
                ? v : (JsonElement?)null;

        // FeenQR's UI multiplies these fields by 100 for display, so emit FRACTIONS.
        // achest: margins & dividendYield are percentages (÷100); ROE/ROA/growth are fractions.

        var valuation = Sub(m, "valuation");
        var profitability = Sub(m, "profitability");
        var perShare = Sub(m, "perShare");
        var growth = Sub(m, "growth");

        // Prefer true year-over-year growth computed from the most recent two fiscal
        // years (achest `historical` is newest-first). Fall back to the 3-year CAGR-ish
        // field only if history is unavailable. Values are fractions.
        var (revYoy, earnYoy) = ComputeYoY(m);

        return new AlphaVantageCompanyOverview
        {
            Symbol = symbol,
            Name = S(profile, "name"),
            Description = S(profile, "description"),
            Sector = S(profile, "sector"),
            Industry = S(profile, "branch") ?? S(profile, "industry"),
            Exchange = S(profile, "exchangeName"),
            Country = S(profile, "country"),
            Website = S(profile, "website"),
            MarketCapitalization = ScaleMillions(S(valuation, "marketCap")),
            PERatio = S(valuation, "pe"),
            PriceToBookRatio = S(valuation, "pb"),
            PriceToSalesRatioTTM = S(valuation, "ps"),
            EVToEBITDA = S(valuation, "evEbitda"),
            EPS = S(perShare, "eps"),
            DividendPerShare = S(perShare, "dps"),
            DividendYield = PercentToFraction(S(perShare, "dividendYield")),
            RevenuePerShareTTM = S(perShare, "sps"),
            BookValue = S(perShare, "bps"),
            ProfitMargin = PercentToFraction(S(profitability, "netMargin")),
            OperatingMarginTTM = PercentToFraction(S(profitability, "operatingMargin")),
            ReturnOnAssetsTTM = S(profitability, "roa"),
            ReturnOnEquityTTM = S(profitability, "roe"),
            QuarterlyEarningsGrowthYOY = earnYoy?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? S(growth, "earningsGrowth3Y"),
            QuarterlyRevenueGrowthYOY = revYoy?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? S(growth, "revenueGrowth3Y")
        };
    }

    /// <summary>
    /// Computes year-over-year revenue & earnings growth from achest's `historical`
    /// array (newest-first). Returns fractions (0.18 == 18%).
    /// </summary>
    private static (decimal? revenue, decimal? earnings) ComputeYoY(JsonElement m)
    {
        if (m.ValueKind != JsonValueKind.Object || !m.TryGetProperty("historical", out var hist) || hist.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        var rows = hist.EnumerateArray().ToList();
        if (rows.Count < 2) return (null, null);

        decimal? Growth(string field)
        {
            var curr = rows[0].TryGetProperty(field, out var c) && c.ValueKind == JsonValueKind.Number ? (decimal?)c.GetDouble() : null;
            var prev = rows[1].TryGetProperty(field, out var p) && p.ValueKind == JsonValueKind.Number ? (decimal?)p.GetDouble() : null;
            if (curr is null || prev is null || prev.Value == 0m) return null;
            return (curr.Value - prev.Value) / Math.Abs(prev.Value);
        }

        return (Growth("revenue"), Growth("netIncome"));
    }

    /// <summary>achest market caps are in millions; Alpha Vantage expects full dollars.</summary>
    private static string ScaleMillions(string millions)
    {
        if (string.IsNullOrWhiteSpace(millions)) return null;
        return decimal.TryParse(millions, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var m)
            ? ((long)(m * 1_000_000m)).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : millions;
    }

    /// <summary>achest returns margins/dividend yield as percentages; FeenQR stores fractions.</summary>
    private static string PercentToFraction(string percent)
    {
        if (string.IsNullOrWhiteSpace(percent)) return null;
        return decimal.TryParse(percent, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p)
            ? (p / 100m).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : percent;
    }
}

// Data models for Alpha Vantage API responses
public class AlphaVantageQuote
{
    [JsonPropertyName("01. symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("05. price")]
    public decimal Price { get; set; }

    [JsonPropertyName("09. change")]
    public decimal Change { get; set; }

    [JsonPropertyName("10. change percent")]
    public string? ChangePercent { get; set; }

    [JsonPropertyName("06. volume")]
    public long Volume { get; set; }

    [JsonPropertyName("03. high")]
    public decimal High { get; set; }

    [JsonPropertyName("04. low")]
    public decimal Low { get; set; }
}

public class AlphaVantageQuoteResponse
{
    [JsonPropertyName("Global Quote")]
    public AlphaVantageQuote? GlobalQuote { get; set; }
}

public class AlphaVantageCompanyOverview
{
    [JsonPropertyName("Symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    [JsonPropertyName("Sector")]
    public string? Sector { get; set; }

    [JsonPropertyName("Industry")]
    public string? Industry { get; set; }

    [JsonPropertyName("Exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("Country")]
    public string? Country { get; set; }

    [JsonPropertyName("Website")]
    public string? Website { get; set; }

    [JsonPropertyName("MarketCapitalization")]
    public string? MarketCapitalization { get; set; }

    [JsonPropertyName("PERatio")]
    public string? PERatio { get; set; }

    [JsonPropertyName("PEGRatio")]
    public string? PEGRatio { get; set; }

    [JsonPropertyName("BookValue")]
    public string? BookValue { get; set; }

    [JsonPropertyName("DividendPerShare")]
    public string? DividendPerShare { get; set; }

    [JsonPropertyName("DividendYield")]
    public string? DividendYield { get; set; }

    [JsonPropertyName("EPS")]
    public string? EPS { get; set; }

    [JsonPropertyName("RevenuePerShareTTM")]
    public string? RevenuePerShareTTM { get; set; }

    [JsonPropertyName("ProfitMargin")]
    public string? ProfitMargin { get; set; }

    [JsonPropertyName("OperatingMarginTTM")]
    public string? OperatingMarginTTM { get; set; }

    [JsonPropertyName("ReturnOnAssetsTTM")]
    public string? ReturnOnAssetsTTM { get; set; }

    [JsonPropertyName("ReturnOnEquityTTM")]
    public string? ReturnOnEquityTTM { get; set; }

    [JsonPropertyName("QuarterlyEarningsGrowthYOY")]
    public string? QuarterlyEarningsGrowthYOY { get; set; }

    [JsonPropertyName("QuarterlyRevenueGrowthYOY")]
    public string? QuarterlyRevenueGrowthYOY { get; set; }

    [JsonPropertyName("AnalystTargetPrice")]
    public string? AnalystTargetPrice { get; set; }

    [JsonPropertyName("TrailingPE")]
    public string? TrailingPE { get; set; }

    [JsonPropertyName("ForwardPE")]
    public string? ForwardPE { get; set; }

    [JsonPropertyName("PriceToSalesRatioTTM")]
    public string? PriceToSalesRatioTTM { get; set; }

    [JsonPropertyName("PriceToBookRatio")]
    public string? PriceToBookRatio { get; set; }

    [JsonPropertyName("EVToRevenue")]
    public string? EVToRevenue { get; set; }

    [JsonPropertyName("EVToEBITDA")]
    public string? EVToEBITDA { get; set; }

    [JsonPropertyName("Beta")]
    public string? Beta { get; set; }

    [JsonPropertyName("FiftyTwoWeekHigh")]
    public string? FiftyTwoWeekHigh { get; set; }

    [JsonPropertyName("FiftyTwoWeekLow")]
    public string? FiftyTwoWeekLow { get; set; }

    [JsonPropertyName("FiftyDayMovingAverage")]
    public string? FiftyDayMovingAverage { get; set; }

    [JsonPropertyName("TwoHundredDayMovingAverage")]
    public string? TwoHundredDayMovingAverage { get; set; }

    [JsonPropertyName("SharesOutstanding")]
    public string? SharesOutstanding { get; set; }

    [JsonPropertyName("DividendDate")]
    public string? DividendDate { get; set; }

    [JsonPropertyName("ExDividendDate")]
    public string? ExDividendDate { get; set; }

    [JsonPropertyName("LastSplitFactor")]
    public string? LastSplitFactor { get; set; }

    [JsonPropertyName("LastSplitDate")]
    public string? LastSplitDate { get; set; }
}

public class AlphaVantageIncomeStatement
{
    [JsonPropertyName("fiscalDateEnding")]
    public string? FiscalDateEnding { get; set; }

    [JsonPropertyName("totalRevenue")]
    public long TotalRevenue { get; set; }

    [JsonPropertyName("costOfRevenue")]
    public long CostOfRevenue { get; set; }

    [JsonPropertyName("grossProfit")]
    public long GrossProfit { get; set; }

    [JsonPropertyName("operatingIncome")]
    public long OperatingIncome { get; set; }

    [JsonPropertyName("netIncome")]
    public long NetIncome { get; set; }
}

public class AlphaVantageIncomeResponse
{
    [JsonPropertyName("annualReports")]
    public List<AlphaVantageIncomeStatement>? AnnualReports { get; set; }
}

public class AlphaVantageBalanceSheet
{
    [JsonPropertyName("fiscalDateEnding")]
    public string? FiscalDateEnding { get; set; }

    [JsonPropertyName("totalAssets")]
    public long TotalAssets { get; set; }

    [JsonPropertyName("totalCurrentAssets")]
    public long TotalCurrentAssets { get; set; }

    [JsonPropertyName("cashAndCashEquivalentsAtCarryingValue")]
    public long CashAndEquivalents { get; set; }

    [JsonPropertyName("totalLiabilities")]
    public long TotalLiabilities { get; set; }

    [JsonPropertyName("totalCurrentLiabilities")]
    public long TotalCurrentLiabilities { get; set; }

    [JsonPropertyName("totalShareholderEquity")]
    public long TotalShareholderEquity { get; set; }
}

public class AlphaVantageBalanceResponse
{
    [JsonPropertyName("annualReports")]
    public List<AlphaVantageBalanceSheet>? AnnualReports { get; set; }
}

public class AlphaVantageCashFlow
{
    [JsonPropertyName("fiscalDateEnding")]
    public required string FiscalDateEnding { get; set; }

    [JsonPropertyName("operatingCashflow")]
    public long OperatingCashflow { get; set; }

    [JsonPropertyName("investingCashflow")]
    public long InvestingCashflow { get; set; }

    [JsonPropertyName("financingCashflow")]
    public long FinancingCashflow { get; set; }

    [JsonPropertyName("netCashflow")]
    public long NetCashflow { get; set; }
}

public class AlphaVantageCashFlowResponse
{
    [JsonPropertyName("annualReports")]
    public required List<AlphaVantageCashFlow> AnnualReports { get; set; }
}

public class AlphaVantageEarnings
{
    [JsonPropertyName("fiscalDateEnding")]
    public required string FiscalDateEnding { get; set; }

    [JsonPropertyName("reportedEPS")]
    public decimal ReportedEPS { get; set; }
}

public class AlphaVantageEarningsResponse
{
    [JsonPropertyName("annualEarnings")]
    public required List<AlphaVantageEarnings> AnnualEarnings { get; set; }
}

public class AlphaVantageTechnicalData
{
    [JsonPropertyName("date")]
    public required string Date { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> Indicators { get; set; } = new();
}

public class AlphaVantageTechnicalResponse
{
    [JsonPropertyName("Technical Analysis: SMA")]
    public required Dictionary<string, AlphaVantageTechnicalData> TechnicalAnalysis { get; set; }
}

public class AlphaVantageForexRate
{
    [JsonPropertyName("1. From_Currency Code")]
    public required string FromCurrency { get; set; }

    [JsonPropertyName("3. To_Currency Code")]
    public required string ToCurrency { get; set; }

    [JsonPropertyName("5. Exchange Rate")]
    public decimal ExchangeRate { get; set; }

    [JsonPropertyName("6. Last Refreshed")]
    public required string LastRefreshed { get; set; }

    [JsonPropertyName("Realtime Currency Exchange Rate")]
    public required AlphaVantageForexRate RealtimeCurrencyExchangeRate { get; set; }
}

public class AlphaVantageForexResponse
{
    [JsonPropertyName("Realtime Currency Exchange Rate")]
    public required AlphaVantageForexRate RealtimeCurrencyExchangeRate { get; set; }
}
