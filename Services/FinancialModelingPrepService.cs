using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

/// <summary>
/// Financial Modeling Prep API service for free financial data and analysis
/// Provides access to company profiles, financial statements, stock prices, and more
/// </summary>
public class FinancialModelingPrepService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FinancialModelingPrepService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AchestService? _achest;
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;

    public FinancialModelingPrepService(
        HttpClient httpClient,
        ILogger<FinancialModelingPrepService> logger,
        IConfiguration configuration,
        AchestService? achest = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _achest = achest;
        _apiKey = _configuration["FMP:ApiKey"] ?? "demo";

        // Configure JSON deserializer to be case-insensitive and use custom converters
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new FlexibleDecimalConverter());
        _jsonOptions.Converters.Add(new FlexibleNullableDecimalConverter());
        _jsonOptions.Converters.Add(new FlexibleNullableLongConverter());

        // Set user agent for API requests
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FeenQR/1.0");
    }

    /// <summary>
    /// Get company profile
    /// </summary>
    public async Task<FMPCompanyProfile> GetCompanyProfileAsync(string symbol)
    {
        // Preferred source: Arithmax Chest (Eulerpool profile).
        if (_achest != null)
        {
            try
            {
                var profile = await _achest.GetCompanyProfileAsync(symbol);
                if (profile.HasValue && profile.Value.ValueKind == JsonValueKind.Object)
                {
                    static string S(System.Text.Json.JsonElement el, string name) =>
                        el.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
                    static long? L(System.Text.Json.JsonElement el, string name)
                    {
                        if (!el.TryGetProperty(name, out var v)) return null;
                        return v.ValueKind switch
                        {
                            System.Text.Json.JsonValueKind.Number => (long)v.GetDouble(),
                            System.Text.Json.JsonValueKind.String when long.TryParse(v.GetString(), out var l) => l,
                            _ => null
                        };
                    }
                    var p = profile.Value;
                    return new FMPCompanyProfile
                    {
                        Symbol = S(p, "ticker") ?? symbol,
                        CompanyName = S(p, "name"),
                        Industry = S(p, "branch") ?? S(p, "industry"),
                        Sector = S(p, "sector"),
                        Website = S(p, "website"),
                        Description = S(p, "description"),
                        Country = S(p, "country"),
                        Image = S(p, "logo"),
                        FullTimeEmployees = L(p, "employees")?.ToString(),
                        IsActivelyTrading = true
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest profile fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://financialmodelingprep.com/stable/profile?symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var profiles = JsonSerializer.Deserialize<List<FMPCompanyProfile>>(content, _jsonOptions);
            return profiles?.Count > 0 ? profiles[0] : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting company profile for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get real-time stock quote
    /// </summary>
    public async Task<FMPQuote> GetQuoteAsync(string symbol)
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

                    // Enrich with achest fundamentals so downstream valuation logic
                    // (fair value via EPS, 52w range, market cap) has what it needs.
                    var metrics = await _achest.GetFinancialMetricsAsync(symbol);
                    var m = metrics.HasValue && metrics.Value.ValueKind == JsonValueKind.Object ? metrics.Value : default;
                    var perShare = Sub(m, "perShare");
                    var valuation = Sub(m, "valuation");
                    var other = Sub(m, "other");
                    var eps = Dec(perShare, "eps") ?? 0m;
                    var mcapMillions = Dec(valuation, "marketCap");
                    var sharesMillions = Dec(other, "shares");

                    return new FMPQuote
                    {
                        Symbol = symbol,
                        Price = (decimal)last.Close,
                        Change = (decimal)change,
                        ChangesPercentage = prev != 0 ? (decimal)((change / prev) * 100) : 0,
                        DayLow = (decimal)last.Low,
                        DayHigh = (decimal)last.High,
                        Open = (decimal)last.Open,
                        PreviousClose = (decimal)prev,
                        Volume = (long)last.Volume,
                        Eps = eps,
                        Pe = eps > 0 ? (decimal)last.Close / eps : 0m,
                        MarketCap = (decimal)((mcapMillions ?? 0m) * 1_000_000m),
                        SharesOutstanding = (long)((sharesMillions ?? 0m) * 1_000_000m),
                        YearHigh = (decimal)bars.Max(b => b.High),
                        YearLow = (decimal)bars.Min(b => b.Low),
                        Timestamp = new DateTimeOffset(last.Timestamp).ToUnixTimeSeconds()
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
            var url = $"https://financialmodelingprep.com/stable/quote?symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var quotes = JsonSerializer.Deserialize<List<FMPQuote>>(content, _jsonOptions);
            return quotes?.Count > 0 ? quotes[0] : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting quote for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get income statement
    /// </summary>
    public async Task<List<FMPIncomeStatement>> GetIncomeStatementAsync(string symbol, int limit = 5)
    {
        // Preferred source: Arithmax Chest (Eulerpool annual income statements).
        if (_achest != null)
        {
            try
            {
                var statements = await MapAchestIncomeStatements(symbol, limit);
                if (statements.Count > 0)
                {
                    return statements;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest income statement fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://financialmodelingprep.com/stable/income-statement?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPIncomeStatement>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FMPIncomeStatement>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting income statement for {symbol}");
            return new List<FMPIncomeStatement>();
        }
    }

    /// <summary>
    /// Get balance sheet
    /// </summary>
    public async Task<List<FMPBalanceSheet>> GetBalanceSheetAsync(string symbol, int limit = 5)
    {
        // Preferred source: Arithmax Chest (Eulerpool annual balance sheets).
        if (_achest != null)
        {
            try
            {
                var statements = await MapAchestBalanceSheets(symbol, limit);
                if (statements.Count > 0)
                {
                    return statements;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest balance sheet fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://financialmodelingprep.com/stable/balance-sheet-statement?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPBalanceSheet>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FMPBalanceSheet>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting balance sheet for {symbol}");
            return new List<FMPBalanceSheet>();
        }
    }

    /// <summary>
    /// Get cash flow statement
    /// </summary>
    public async Task<List<FMPCashFlow>> GetCashFlowAsync(string symbol, int limit = 5)
    {
        // Preferred source: Arithmax Chest (Eulerpool annual cash-flow statements).
        if (_achest != null)
        {
            try
            {
                var statements = await MapAchestCashFlows(symbol, limit);
                if (statements.Count > 0)
                {
                    return statements;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest cash flow fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://financialmodelingprep.com/stable/cash-flow-statement?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPCashFlow>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FMPCashFlow>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting cash flow for {symbol}");
            return new List<FMPCashFlow>();
        }
    }

    /// <summary>
    /// Get key metrics
    /// </summary>
    public async Task<List<FMPKeyMetrics>> GetKeyMetricsAsync(string symbol, int limit = 5)
    {
        // Preferred source: Arithmax Chest (Eulerpool financial metrics).
        if (_achest != null)
        {
            try
            {
                var m = await _achest.GetFinancialMetricsAsync(symbol);
                if (m.HasValue && m.Value.ValueKind == JsonValueKind.Object)
                {
                    var km = MapMetricsToFmpKeyMetrics(symbol, m.Value);
                    if (km != null)
                    {
                        await EnrichDerivedValuationAsync(symbol, km);
                        return new List<FMPKeyMetrics> { km };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest key metrics fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://financialmodelingprep.com/stable/key-metrics?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPKeyMetrics>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FMPKeyMetrics>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting key metrics for {symbol}");
            return new List<FMPKeyMetrics>();
        }
    }

    /// <summary>
    /// Get financial ratios
    /// </summary>
    public async Task<List<FMPFinancialRatios>> GetFinancialRatiosAsync(string symbol, int limit = 5)
    {
        // Preferred source: Arithmax Chest (Eulerpool financial metrics).
        if (_achest != null)
        {
            try
            {
                var m = await _achest.GetFinancialMetricsAsync(symbol);
                if (m.HasValue && m.Value.ValueKind == JsonValueKind.Object)
                {
                    var ratios = MapMetricsToFmpRatios(symbol, m.Value);
                    await EnrichDerivedRatiosAsync(symbol, ratios);
                    return new List<FMPFinancialRatios> { ratios };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest ratios fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://financialmodelingprep.com/stable/ratios?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPFinancialRatios>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FMPFinancialRatios>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting financial ratios for {symbol}");
            return new List<FMPFinancialRatios>();
        }
    }

    /// <summary>
    /// Get historical daily prices
    /// </summary>
    public async Task<List<FMPHistoricalPrice>> GetHistoricalPricesAsync(string symbol, string from, string to)
    {
        // Preferred source: Arithmax Chest unified OHLCV API.
        if (_achest != null)
        {
            try
            {
                DateTime.TryParse(from, out var fromDate);
                DateTime.TryParse(to, out var toDate);
                if (fromDate == default) fromDate = DateTime.UtcNow.AddYears(-1);
                if (toDate == default) toDate = DateTime.UtcNow;
                var bars = await _achest.GetBarsAsync(symbol, fromDate.Date, toDate.Date.AddDays(1));
                if (bars.Count > 0)
                {
                    return bars.Select(b => new FMPHistoricalPrice
                    {
                        Date = b.Timestamp.ToString("yyyy-MM-dd"),
                        Open = (decimal)b.Open,
                        High = (decimal)b.High,
                        Low = (decimal)b.Low,
                        Close = (decimal)b.Close,
                        AdjClose = (decimal)b.Close,
                        Volume = (long)b.Volume,
                        UnadjustedVolume = (long)b.Volume,
                        Label = b.Timestamp.ToString("yyyy-MM-dd")
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "achest historical fallback for {Symbol}", symbol);
            }
        }

        try
        {
            var url = $"https://financialmodelingprep.com/stable/historical-price-full/{symbol}?from={from}&to={to}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPHistoricalPrice>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<FMPHistoricalResponse>(content, _jsonOptions);
            return data?.Historical ?? new List<FMPHistoricalPrice>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting historical prices for {symbol}");
            return new List<FMPHistoricalPrice>();
        }
    }

    /// <summary>
    /// Get analyst estimates
    /// </summary>
    public async Task<List<FMPAnalystEstimates>> GetAnalystEstimatesAsync(string symbol, int limit = 5)
    {
        try
        {
            var url = $"https://financialmodelingprep.com/api/v3/analyst-estimates/{symbol}?limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Analyst estimates require a premium subscription.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("FMP API access forbidden (403). Analyst estimates endpoint may require a premium subscription or your API key may be invalid.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPAnalystEstimates>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FMPAnalystEstimates>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting analyst estimates for {symbol}");
            return new List<FMPAnalystEstimates>();
        }
    }

    /// <summary>
    /// Get stock screener results
    /// </summary>
    public async Task<List<FMPStockScreener>> GetStockScreenerAsync(FMPStockScreenerCriteria criteria)
    {
        try
        {
            var queryParams = BuildScreenerQueryString(criteria);
            var url = $"https://financialmodelingprep.com/stable/stock-screener?{queryParams}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPStockScreener>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FMPStockScreener>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting stock screener results");
            return new List<FMPStockScreener>();
        }
    }

    /// <summary>
    /// Get market indices
    /// </summary>
    public async Task<List<FMPMarketIndex>> GetMarketIndicesAsync()
    {
        try
        {
            var url = $"https://financialmodelingprep.com/stable/quotes/index?apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    _logger.LogWarning("FMP API free tier limit exceeded (402 Payment Required). Falling back to Alpha Vantage.");
                }
                else
                {
                    _logger.LogWarning($"FMP API error: {response.StatusCode}");
                }
                return new List<FMPMarketIndex>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FMPMarketIndex>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting market indices");
            return new List<FMPMarketIndex>();
        }
    }

    private string BuildScreenerQueryString(FMPStockScreenerCriteria criteria)
    {
        var parameters = new List<string>();

        if (criteria.MarketCapMin > 0) parameters.Add($"marketCapMin={criteria.MarketCapMin}");
        if (criteria.MarketCapMax > 0) parameters.Add($"marketCapMax={criteria.MarketCapMax}");
        if (criteria.VolumeMin > 0) parameters.Add($"volumeMin={criteria.VolumeMin}");
        if (criteria.VolumeMax > 0) parameters.Add($"volumeMax={criteria.VolumeMax}");
        if (criteria.PERatioMin > 0) parameters.Add($"peRatioMin={criteria.PERatioMin}");
        if (criteria.PERatioMax > 0) parameters.Add($"peRatioMax={criteria.PERatioMax}");
        if (!string.IsNullOrEmpty(criteria.Sector)) parameters.Add($"sector={criteria.Sector}");
        if (!string.IsNullOrEmpty(criteria.Industry)) parameters.Add($"industry={criteria.Industry}");
        if (!string.IsNullOrEmpty(criteria.Country)) parameters.Add($"country={criteria.Country}");

        return string.Join("&", parameters);
    }

    // ── achest (Eulerpool) → FMP metric mappers ────────────────────────
    // Unit conventions: achest returns ROE/ROA/growth as fractions (0.31 == 31%)
    // and margins/dividendYield as percentages; marketCap is in millions.
    private static JsonElement? Sub(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object
            ? v : (JsonElement?)null;

    private static decimal? Dec(JsonElement? el, string name, decimal scale = 1m)
    {
        if (!el.HasValue || !el.Value.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => (decimal)v.GetDouble() * scale,
            JsonValueKind.String when decimal.TryParse(v.GetString(), out var d) => d * scale,
            _ => null
        };
    }

    /// <summary>Maps achest Eulerpool metrics onto FMP key metrics (primary cascade slot).</summary>
    private static FMPKeyMetrics MapMetricsToFmpKeyMetrics(string symbol, JsonElement m)
    {
        var valuation = Sub(m, "valuation");
        var perShare = Sub(m, "perShare");
        var leverage = Sub(m, "leverage");
        var other = Sub(m, "other");

        var eps = Dec(perShare, "eps") ?? 0m;
        // achest gives no P/E directly (often 0); derive from marketCap/shares when possible.
        var peRatio = Dec(valuation, "pe");
        if (peRatio is null || peRatio == 0m)
        {
            var shares = Dec(other, "shares");
            var marketCapMillions = Dec(valuation, "marketCap");
            if (shares is > 0 && marketCapMillions is > 0 && eps != 0m)
            {
                // marketCap(millions) / shares(millions) = price; price/eps = P/E
                peRatio = (marketCapMillions.Value / shares.Value) / eps;
            }
        }

        // Graham Number = sqrt(22.5 * EPS * BookValuePerShare)
        var bvps = Dec(perShare, "bps") ?? 0m;
        decimal? graham = eps > 0m && bvps > 0m ? (decimal)Math.Sqrt((double)(22.5m * eps * bvps)) : null;

        return new FMPKeyMetrics
        {
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Symbol = symbol,
            Period = "FY",
            RevenuePerShare = Dec(perShare, "sps"),
            NetIncomePerShare = Dec(perShare, "eps"),
            BookValuePerShare = Dec(perShare, "bps"),
            MarketCap = (long?)(Dec(valuation, "marketCap") * 1_000_000m),
            EnterpriseValue = (long?)(Dec(valuation, "enterpriseValue") * 1_000_000m),
            PeRatio = peRatio,
            PriceToSalesRatio = Dec(valuation, "ps"),
            PbRatio = Dec(valuation, "pb"),
            EvToSales = Dec(valuation, "evRevenue"),
            EnterpriseValueOverEBITDA = Dec(valuation, "evEbitda"),
            GrahamNumber = graham,
            DebtToEquity = Dec(leverage, "debtToEquity"),
            CurrentRatio = Dec(leverage, "currentRatio"),
            DividendYield = Dec(perShare, "dividendYield"),          // already a percentage (0.32 == 0.32%)
            Roe = Dec(Sub(m, "profitability"), "roe", 100m)          // fraction -> %
        };
    }

    /// <summary>
    /// Maps achest Eulerpool metrics onto FMP financial ratios (primary cascade slot).
    /// NOTE: FeenQR's UI multiplies these ratio fields by 100 before display, so the
    /// values emitted here must be FRACTIONS (0.2692 == 26.92%), matching FMP's convention.
    /// achest returns margins/dividendYield as percentages (÷100) and ROA/ROE as fractions.
    /// </summary>
    private static FMPFinancialRatios MapMetricsToFmpRatios(string symbol, JsonElement m)
    {
        var valuation = Sub(m, "valuation");
        var profitability = Sub(m, "profitability");
        var leverage = Sub(m, "leverage");
        var perShare = Sub(m, "perShare");

        const decimal PctToFraction = 0.01m;

        // achest profitability roe/roa are already fractions.
        var roe = Dec(profitability, "roe") ?? 0m;
        var roa = Dec(profitability, "roa") ?? 0m;

        // P/E: derive if needed.
        var pe = Dec(valuation, "pe") ?? 0m;
        if (pe == 0m)
        {
            var eps = Dec(perShare, "eps") ?? 0m;
            var shares = Dec(Sub(m, "other"), "shares") ?? 0m;
            var mc = Dec(valuation, "marketCap") ?? 0m;
            if (eps != 0m && shares > 0 && mc > 0)
            {
                pe = (mc / shares) / eps;
            }
        }

        // PEG = P/E / earnings growth (%). Growth is a fraction -> convert to % for the ratio math.
        var earningsGrowthPct = (Dec(Sub(m, "growth"), "earningsGrowth3Y") ?? 0m) * 100m;
        var peg = pe > 0m && earningsGrowthPct > 0m ? pe / earningsGrowthPct : 0m;

        return new FMPFinancialRatios
        {
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Symbol = symbol,
            Period = "FY",
            GrossMargin = (Dec(profitability, "grossMargin") ?? 0m) * PctToFraction,
            OperatingMargin = (Dec(profitability, "operatingMargin") ?? 0m) * PctToFraction,
            NetProfitMargin = (Dec(profitability, "netMargin") ?? 0m) * PctToFraction,
            ReturnOnAssets = roa,
            ReturnOnEquity = roe,
            ReturnOnCapitalEmployed = Dec(profitability, "roce") ?? 0m,
            DebtEquityRatio = Dec(leverage, "debtToEquity") ?? 0m,
            CurrentRatio = Dec(leverage, "currentRatio") ?? 0m,
            PriceEarningsRatio = pe,
            PriceEarningsToGrowthRatio = peg,
            PriceToBookRatio = Dec(valuation, "pb") ?? 0m,
            PriceToSalesRatio = Dec(valuation, "ps") ?? 0m,
            EnterpriseValueMultiple = Dec(valuation, "evEbitda") ?? 0m,
            DividendYield = (Dec(perShare, "dividendYield") ?? 0m) * PctToFraction
        };
    }

    // ── achest (Eulerpool) → FMP financial statement mappers ───────────

    /// <summary>
    /// Fills valuation multiples that achest's metrics endpoint leaves at 0/null
    /// (P/S, EV/Revenue, EV/EBITDA) by combining market cap with revenue/EBITDA and
    /// net debt from the income and balance statement endpoints.
    /// </summary>
    private async Task EnrichDerivedValuationAsync(string symbol, FMPKeyMetrics km)
    {
        try
        {
            if (_achest == null) return;

            var needsPs = (km.PriceToSalesRatio ?? 0m) == 0m;
            var needsEvRev = (km.EvToSales ?? 0m) == 0m;
            var needsEvEbitda = (km.EnterpriseValueOverEBITDA ?? 0m) == 0m;
            if (!needsPs && !needsEvRev && !needsEvEbitda) return;

            // Revenue + EBITDA from the most recent income statement (achest is
            // oldest-first & in millions; use the last row).
            var income = await _achest.TryEulerpoolAsync($"fundamentals/income/{Uri.EscapeDataString(symbol)}");
            decimal? revenue = null, ebitda = null;
            if (income.HasValue && income.Value.ValueKind == JsonValueKind.Array)
            {
                var rows = ActualRowsNewestFirst(income.Value);
                if (rows.Count > 0)
                {
                    var r = rows[0];
                    revenue = Lng(r, "revenue") * 1_000_000L;
                    var ebit = Lng(r, "ebit");
                    var dep = Lng(r, "depreciationAmortization");
                    if (ebit.HasValue) ebitda = (ebit.Value + (dep ?? 0)) * 1_000_000m;
                }
            }

            // Net debt from the most recent balance sheet (last row, millions).
            decimal? netDebt = null;
            var balance = await _achest.TryEulerpoolAsync($"fundamentals/balance/{Uri.EscapeDataString(symbol)}");
            if (balance.HasValue && balance.Value.ValueKind == JsonValueKind.Array)
            {
                var rows = ActualRowsNewestFirst(balance.Value);
                if (rows.Count > 0)
                {
                    var r = rows[0];
                    var shortDebt = Lng(r, "shortTermDebt") ?? 0;
                    var longDebt = Lng(r, "longTermDebt") ?? 0;
                    var cash = Lng(r, "cashShortTermInvestments") ?? 0;
                    netDebt = (shortDebt + longDebt - cash) * 1_000_000m;
                }
            }

            var marketCap = (decimal)(km.MarketCap ?? 0L);
            if (marketCap <= 0 || revenue == null || revenue.Value <= 0) return;

            if (needsPs)
            {
                km.PriceToSalesRatio = marketCap / revenue.Value;
            }

            var enterpriseValue = marketCap + (netDebt ?? 0m);
            km.EnterpriseValue = (long)enterpriseValue;

            if (needsEvRev)
            {
                km.EvToSales = enterpriseValue / revenue.Value;
            }
            if (needsEvEbitda && ebitda is > 0)
            {
                km.EnterpriseValueOverEBITDA = enterpriseValue / ebitda.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Derived valuation enrichment failed for {Symbol}", symbol);
        }
    }

    /// <summary>Fills P/S and EV/EBITDA on the ratios object when achest leaves them at 0.</summary>
    private async Task EnrichDerivedRatiosAsync(string symbol, FMPFinancialRatios ratios)
    {
        try
        {
            if (_achest == null) return;
            if (ratios.PriceToSalesRatio != 0m && ratios.EnterpriseValueMultiple != 0m) return;

            var income = await _achest.TryEulerpoolAsync($"fundamentals/income/{Uri.EscapeDataString(symbol)}");
            decimal? revenue = null, ebitda = null;
            if (income.HasValue && income.Value.ValueKind == JsonValueKind.Array)
            {
                var rows = ActualRowsNewestFirst(income.Value);
                if (rows.Count > 0)
                {
                    var r = rows[0]; // most recent actual (achest is oldest-first)
                    revenue = Lng(r, "revenue") * 1_000_000L;
                    var ebit = Lng(r, "ebit");
                    var dep = Lng(r, "depreciationAmortization");
                    if (ebit.HasValue) ebitda = (ebit.Value + (dep ?? 0)) * 1_000_000m;
                }
            }
            if (revenue == null || revenue.Value <= 0) return;

            var metrics = await _achest.GetFinancialMetricsAsync(symbol);
            var m = metrics.HasValue ? metrics.Value : default;
            var marketCapMillions = Dec(Sub(m, "valuation"), "marketCap") ?? 0m;
            var marketCap = marketCapMillions * 1_000_000m;
            if (marketCap <= 0) return;

            if (ratios.PriceToSalesRatio == 0m)
            {
                ratios.PriceToSalesRatio = marketCap / revenue.Value;
            }

            if (ratios.EnterpriseValueMultiple == 0m && ebitda is > 0)
            {
                decimal netDebt = 0m;
                var balance = await _achest.TryEulerpoolAsync($"fundamentals/balance/{Uri.EscapeDataString(symbol)}");
                if (balance.HasValue && balance.Value.ValueKind == JsonValueKind.Array)
                {
                    var rows = ActualRowsNewestFirst(balance.Value);
                    if (rows.Count > 0)
                    {
                        var r = rows[0];
                        netDebt = ((Lng(r, "shortTermDebt") ?? 0) + (Lng(r, "longTermDebt") ?? 0) - (Lng(r, "cashShortTermInvestments") ?? 0)) * 1_000_000m;
                    }
                }
                var enterpriseValue = marketCap + netDebt;
                ratios.EnterpriseValueMultiple = enterpriseValue / ebitda.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Derived ratios enrichment failed for {Symbol}", symbol);
        }
    }

    private static long? Lng(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => (long)v.GetDouble(),
            JsonValueKind.String when long.TryParse(v.GetString(), out var l) => l,
            _ => null
        };
    }

    /// <summary>Reads an achest statement figure (in millions) and returns dollars.</summary>
    private static long? Millions(JsonElement el, string name)
    {
        var raw = Lng(el, name);
        return raw.HasValue ? raw.Value * 1_000_000L : (long?)null;
    }

    /// <summary>True when an achest statement period is a forward estimate (e.g. "2031-06-30e").</summary>
    private static bool IsEstimate(JsonElement row) =>
        (Str(row, "period") ?? string.Empty).TrimEnd().EndsWith("e", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns achest statement rows ordered newest-first, excluding forward estimates.
    /// achest lists rows oldest-first; the tail may contain `...e` estimate periods.
    /// </summary>
    private static List<JsonElement> ActualRowsNewestFirst(JsonElement array)
    {
        var rows = array.EnumerateArray().Where(r => !IsEstimate(r)).ToList();
        rows.Reverse(); // oldest-first -> newest-first
        return rows;
    }

    private static decimal? DecVal(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => (decimal)v.GetDouble(),
            JsonValueKind.String when decimal.TryParse(v.GetString(), out var d) => d,
            _ => null
        };
    }

    private static string Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) ? v.ToString() : null;

    private async Task<List<FMPIncomeStatement>> MapAchestIncomeStatements(string symbol, int limit)
    {
        var result = new List<FMPIncomeStatement>();
        var data = await _achest!.TryEulerpoolAsync($"fundamentals/income/{Uri.EscapeDataString(symbol)}");
        if (!data.HasValue || data.Value.ValueKind != JsonValueKind.Array) return result;

        // achest returns rows oldest-first and values in MILLIONS. Take the most
        // recent `limit` rows and present them newest-first, scaled to dollars.
        var rows = ActualRowsNewestFirst(data.Value);
        for (var i = 0; i < rows.Count && result.Count < limit; i++)
        {
            var r = rows[i];
            var revenue = Millions(r, "revenue");
            var netIncome = Millions(r, "netIncome");
            result.Add(new FMPIncomeStatement
            {
                Date = Str(r, "period") ?? string.Empty,
                Symbol = symbol,
                Period = "FY",
                Revenue = revenue,
                CostOfRevenue = Millions(r, "costOfGoodsSold"),
                GrossProfit = Millions(r, "grossIncome"),
                ResearchAndDevelopmentExpenses = Millions(r, "researchDevelopment"),
                GeneralAndAdministrativeExpenses = Millions(r, "sgaExpense"),
                OtherExpenses = Millions(r, "otherOperatingExpensesTotal"),
                OperatingExpenses = Millions(r, "totalOperatingExpense"),
                DepreciationAndAmortization = Millions(r, "depreciationAmortization"),
                OperatingIncome = Millions(r, "ebit"),
                TotalOtherIncomeExpensesNet = Millions(r, "totalOtherIncomeExpenseNet"),
                IncomeBeforeTax = Millions(r, "pretaxIncome"),
                IncomeTaxExpense = Millions(r, "provisionforIncomeTaxes"),
                NetIncome = netIncome,
                Eps = DecVal(r, "diluted_eps"),
                Epsdiluted = DecVal(r, "diluted_eps"),
                WeightedAverageShsOut = Millions(r, "shares"),
                WeightedAverageShsOutDil = Millions(r, "shares")
            });
        }
        return result;
    }

    private async Task<List<FMPBalanceSheet>> MapAchestBalanceSheets(string symbol, int limit)
    {
        var result = new List<FMPBalanceSheet>();
        var data = await _achest!.TryEulerpoolAsync($"fundamentals/balance/{Uri.EscapeDataString(symbol)}");
        if (!data.HasValue || data.Value.ValueKind != JsonValueKind.Array) return result;

        var rows = ActualRowsNewestFirst(data.Value);
        for (var i = 0; i < rows.Count && result.Count < limit; i++)
        {
            var r = rows[i];
            result.Add(new FMPBalanceSheet
            {
                Date = Str(r, "period") ?? string.Empty,
                Symbol = symbol,
                Period = "FY",
                CashAndCashEquivalents = Millions(r, "cashShortTermInvestments"),
                CashAndShortTermInvestments = Millions(r, "cashShortTermInvestments"),
                NetReceivables = Millions(r, "accountsReceivables"),
                Inventory = Millions(r, "inventory"),
                OtherCurrentAssets = Millions(r, "otherCurrentAssets"),
                TotalCurrentAssets = Millions(r, "currentAssets"),
                PropertyPlantEquipmentNet = Millions(r, "propertyPlantEquipment"),
                Goodwill = Millions(r, "goodwill"),
                IntangibleAssets = Millions(r, "intangiblesAssets"),
                LongTermInvestments = Millions(r, "longTermInvestments"),
                TotalAssets = Millions(r, "allAssets") ?? Millions(r, "assets"),
                AccountPayables = Millions(r, "accountsPayable"),
                ShortTermDebt = Millions(r, "shortTermDebt"),
                OtherCurrentLiabilities = Millions(r, "otherCurrentliabilities"),
                TotalCurrentLiabilities = Millions(r, "currentLiabilities"),
                LongTermDebt = Millions(r, "longTermDebt"),
                TotalLiabilities = Millions(r, "liabilities"),
                CommonStock = Millions(r, "commonStock"),
                RetainedEarnings = Millions(r, "retainedEarnings"),
                TotalStockholdersEquity = Millions(r, "equity"),
                TotalEquity = Millions(r, "equity"),
                TotalDebt = (Millions(r, "shortTermDebt") ?? 0) + (Millions(r, "longTermDebt") ?? 0)
            });
        }
        return result;
    }

    private async Task<List<FMPCashFlow>> MapAchestCashFlows(string symbol, int limit)
    {
        var result = new List<FMPCashFlow>();
        var data = await _achest!.TryEulerpoolAsync($"fundamentals/cashflow/{Uri.EscapeDataString(symbol)}");
        if (!data.HasValue || data.Value.ValueKind != JsonValueKind.Array) return result;

        var rows = ActualRowsNewestFirst(data.Value);
        for (var i = 0; i < rows.Count && result.Count < limit; i++)
        {
            var r = rows[i];
            result.Add(new FMPCashFlow
            {
                Date = Str(r, "period") ?? string.Empty,
                Symbol = symbol,
                Period = "FY",
                NetIncome = Millions(r, "netIncomeStartingLine"),
                DepreciationAndAmortization = Millions(r, "amortization"),
                DeferredIncomeTax = Millions(r, "deferredTaxesInvestmentTaxCredit"),
                ChangeInWorkingCapital = Millions(r, "changesinWorkingCapital"),
                OtherNonCashItems = Millions(r, "nonCashItems"),
                NetCashProvidedByOperatingActivities = Millions(r, "netOperatingCashFlow"),
                InvestmentsInPropertyPlantAndEquipment = Millions(r, "capex"),
                NetCashProvidedByInvestingActivities = Millions(r, "netInvestingCashFlow"),
                OtherInvestingActivites = Millions(r, "otherInvestingCashFlowItemsTotal"),
                OtherFinancingActivites = Millions(r, "otherFundsFinancingItems"),
                DividendsPaid = Millions(r, "cashDividendsPaid"),
                NetCashProvidedByFinancingActivities = Millions(r, "netCashFinancingActivities"),
                EffectOfForexChangesOnCash = Millions(r, "foreignExchangeEffects"),
                NetChangeInCash = Millions(r, "cashNet"),
                OperatingCashFlow = Millions(r, "netOperatingCashFlow"),
                CapitalExpenditure = Millions(r, "capex"),
                FreeCashFlow = Millions(r, "fcf")
            });
        }
        return result;
    }
}

// Data models for Financial Modeling Prep API responses
public class FMPCompanyProfile
{
    public string Symbol { get; set; }
    public string CompanyName { get; set; }
    public string Industry { get; set; }
    public string Sector { get; set; }
    public string Website { get; set; }
    public string Description { get; set; }
    public string CEO { get; set; }
    public string FullTimeEmployees { get; set; }
    public string Phone { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
    public string Country { get; set; }
    public string Image { get; set; }
    public string IpoDate { get; set; }
    public bool IsActivelyTrading { get; set; }
}

public class FMPQuote
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal ChangesPercentage { get; set; }
    public decimal Change { get; set; }
    public decimal DayLow { get; set; }
    public decimal DayHigh { get; set; }
    public decimal YearLow { get; set; }
    public decimal YearHigh { get; set; }
    public decimal MarketCap { get; set; }
    public decimal PriceAvg50 { get; set; }
    public decimal PriceAvg200 { get; set; }
    public long Volume { get; set; }
    public long AvgVolume { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public decimal Open { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal Eps { get; set; }
    public decimal Pe { get; set; }
    public string EarningsAnnouncement { get; set; } = string.Empty;
    public long SharesOutstanding { get; set; }
    public long Timestamp { get; set; }
}

public class FMPIncomeStatement
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public long? Revenue { get; set; }
    public long? CostOfRevenue { get; set; }
    public long? GrossProfit { get; set; }
    public decimal? GrossProfitRatio { get; set; }
    public long? ResearchAndDevelopmentExpenses { get; set; }
    public long? GeneralAndAdministrativeExpenses { get; set; }
    public long? SellingAndMarketingExpenses { get; set; }
    public long? OtherExpenses { get; set; }
    public long? OperatingExpenses { get; set; }
    public long? CostAndExpenses { get; set; }
    public long? InterestExpense { get; set; }
    public long? DepreciationAndAmortization { get; set; }
    public long? Ebitda { get; set; }
    public decimal? Ebitdaratio { get; set; }
    public long? OperatingIncome { get; set; }
    public decimal? OperatingIncomeRatio { get; set; }
    public long? TotalOtherIncomeExpensesNet { get; set; }
    public long? IncomeBeforeTax { get; set; }
    public decimal? IncomeBeforeTaxRatio { get; set; }
    public long? IncomeTaxExpense { get; set; }
    public long? NetIncome { get; set; }
    public decimal? NetIncomeRatio { get; set; }
    public decimal? Eps { get; set; }
    public decimal? Epsdiluted { get; set; }
    public long? WeightedAverageShsOut { get; set; }
    public long? WeightedAverageShsOutDil { get; set; }
}

public class FMPBalanceSheet
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public long? CashAndCashEquivalents { get; set; }
    public long? ShortTermInvestments { get; set; }
    public long? CashAndShortTermInvestments { get; set; }
    public long? NetReceivables { get; set; }
    public long? Inventory { get; set; }
    public long? OtherCurrentAssets { get; set; }
    public long? TotalCurrentAssets { get; set; }
    public long? PropertyPlantEquipmentNet { get; set; }
    public long? Goodwill { get; set; }
    public long? IntangibleAssets { get; set; }
    public long? GoodwillAndIntangibleAssets { get; set; }
    public long? LongTermInvestments { get; set; }
    public long? TaxAssets { get; set; }
    public long? OtherNonCurrentAssets { get; set; }
    public long? TotalNonCurrentAssets { get; set; }
    public long? OtherAssets { get; set; }
    public long? TotalAssets { get; set; }
    public long? AccountPayables { get; set; }
    public long? ShortTermDebt { get; set; }
    public long? TaxPayables { get; set; }
    public long? DeferredRevenue { get; set; }
    public long? OtherCurrentLiabilities { get; set; }
    public long? TotalCurrentLiabilities { get; set; }
    public long? LongTermDebt { get; set; }
    public long? DeferredRevenueNonCurrent { get; set; }
    public long? DeferredTaxLiabilitiesNonCurrent { get; set; }
    public long? OtherNonCurrentLiabilities { get; set; }
    public long? TotalNonCurrentLiabilities { get; set; }
    public long? OtherLiabilities { get; set; }
    public long? TotalLiabilities { get; set; }
    public long? CommonStock { get; set; }
    public long? RetainedEarnings { get; set; }
    public long? AccumulatedOtherComprehensiveIncomeLoss { get; set; }
    public long? OthertotalStockholdersEquity { get; set; }
    public long? TotalStockholdersEquity { get; set; }
    public long? TotalLiabilitiesAndStockholdersEquity { get; set; }
    public long? MinorityInterest { get; set; }
    public long? TotalEquity { get; set; }
    public long? TotalLiabilitiesAndTotalEquity { get; set; }
    public long? TotalInvestments { get; set; }
    public long? TotalDebt { get; set; }
    public long? NetDebt { get; set; }
}

public class FMPCashFlow
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public long? NetIncome { get; set; }
    public long? DepreciationAndAmortization { get; set; }
    public long? DeferredIncomeTax { get; set; }
    public long? StockBasedCompensation { get; set; }
    public long? ChangeInWorkingCapital { get; set; }
    public long? AccountsReceivables { get; set; }
    public long? Inventory { get; set; }
    public long? AccountsPayables { get; set; }
    public long? OtherWorkingCapital { get; set; }
    public long? OtherNonCashItems { get; set; }
    public long? NetCashProvidedByOperatingActivities { get; set; }
    public long? InvestmentsInPropertyPlantAndEquipment { get; set; }
    public long? AcquisitionsNet { get; set; }
    public long? PurchasesOfInvestments { get; set; }
    public long? SalesMaturitiesOfInvestments { get; set; }
    public long? OtherInvestingActivites { get; set; }
    public long? NetCashProvidedByInvestingActivities { get; set; }
    public long? DebtRepayment { get; set; }
    public long? CommonStockIssued { get; set; }
    public long? CommonStockRepurchased { get; set; }
    public long? DividendsPaid { get; set; }
    public long? OtherFinancingActivites { get; set; }
    public long? NetCashProvidedByFinancingActivities { get; set; }
    public long? EffectOfForexChangesOnCash { get; set; }
    public long? NetChangeInCash { get; set; }
    public long? CashAtEndOfPeriod { get; set; }
    public long? CashAtBeginningOfPeriod { get; set; }
    public long? OperatingCashFlow { get; set; }
    public long? CapitalExpenditure { get; set; }
    public long? FreeCashFlow { get; set; }
}

public class FMPKeyMetrics
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public decimal? RevenuePerShare { get; set; }
    public decimal? NetIncomePerShare { get; set; }
    public decimal? OperatingCashFlowPerShare { get; set; }
    public decimal? FreeCashFlowPerShare { get; set; }
    public decimal? CashPerShare { get; set; }
    public decimal? BookValuePerShare { get; set; }
    public decimal? TangibleBookValuePerShare { get; set; }
    public decimal? ShareholdersEquityPerShare { get; set; }
    public decimal? InterestDebtPerShare { get; set; }
    public long? MarketCap { get; set; }
    public long? EnterpriseValue { get; set; }
    public decimal? PeRatio { get; set; }
    public decimal? PriceToSalesRatio { get; set; }
    public decimal? Pocfratio { get; set; }
    public decimal? PfcfRatio { get; set; }
    public decimal? PbRatio { get; set; }
    public decimal? Ptbratio { get; set; }
    public decimal? EvToSales { get; set; }
    public decimal? EnterpriseValueOverEBITDA { get; set; }
    public decimal? EvToOperatingCashFlow { get; set; }
    public decimal? EvToFreeCashFlow { get; set; }
    public decimal? EarningsYield { get; set; }
    public decimal? FreeCashFlowYield { get; set; }
    public decimal? DebtToEquity { get; set; }
    public decimal? DebtToAssets { get; set; }
    public decimal? NetDebtToEBITDA { get; set; }
    public decimal? CurrentRatio { get; set; }
    public decimal? InterestCoverage { get; set; }
    public decimal? IncomeQuality { get; set; }
    public decimal? DividendYield { get; set; }
    public decimal? PayoutRatio { get; set; }
    public decimal? SalesGeneralAndAdministrativeToRevenue { get; set; }
    public decimal? ResearchAndDdevelopementToRevenue { get; set; }
    public decimal? IntangiblesToTotalAssets { get; set; }
    public decimal? CapexToOperatingCashFlow { get; set; }
    public decimal? CapexToRevenue { get; set; }
    public decimal? CapexToDepreciation { get; set; }
    public decimal? StockBasedCompensationToRevenue { get; set; }
    public decimal? GrahamNumber { get; set; }
    public decimal? Roic { get; set; }
    public decimal? ReturnOnTangibleAssets { get; set; }
    public decimal? GrahamNetNet { get; set; }
    public long? WorkingCapital { get; set; }
    public long? TangibleAssetValue { get; set; }
    public long? NetCurrentAssetValue { get; set; }
    public long? InvestedCapital { get; set; }
    public long? AverageReceivables { get; set; }
    public long? AveragePayables { get; set; }
    public long? AverageInventory { get; set; }
    public decimal? DaysSalesOutstanding { get; set; }
    public decimal? DaysPayablesOutstanding { get; set; }
    public decimal? DaysOfInventoryOnHand { get; set; }
    public decimal? ReceivablesTurnover { get; set; }
    public decimal? PayablesTurnover { get; set; }
    public decimal? InventoryTurnover { get; set; }
    public decimal? Roe { get; set; }
    public decimal? CapexPerShare { get; set; }
}

public class FMPFinancialRatios
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public decimal CurrentRatio { get; set; }
    public decimal QuickRatio { get; set; }
    public decimal CashRatio { get; set; }
    public decimal DaysOfSalesOutstanding { get; set; }
    public decimal DaysOfInventoryOutstanding { get; set; }
    public decimal OperatingCycle { get; set; }
    public decimal DaysOfPayablesOutstanding { get; set; }
    public decimal CashConversionCycle { get; set; }
    public decimal GrossMargin { get; set; }
    public decimal OperatingMargin { get; set; }
    public decimal PretaxProfitMargin { get; set; }
    public decimal NetProfitMargin { get; set; }
    public decimal EffectiveTaxRate { get; set; }
    public decimal ReturnOnAssets { get; set; }
    public decimal ReturnOnEquity { get; set; }
    public decimal ReturnOnCapitalEmployed { get; set; }
    public decimal NetIncomePerEBT { get; set; }
    public decimal EbtPerEbit { get; set; }
    public decimal EbitPerRevenue { get; set; }
    public decimal DebtRatio { get; set; }
    public decimal DebtEquityRatio { get; set; }
    public decimal LongTermDebtToCapitalization { get; set; }
    public decimal TotalDebtToCapitalization { get; set; }
    public decimal InterestCoverage { get; set; }
    public decimal CashFlowToDebtRatio { get; set; }
    public decimal CompanyEquityMultiplier { get; set; }
    public decimal ReceivablesTurnover { get; set; }
    public decimal PayablesTurnover { get; set; }
    public decimal InventoryTurnover { get; set; }
    public decimal FixedAssetTurnover { get; set; }
    public decimal AssetTurnover { get; set; }
    public decimal OperatingCashFlowPerShare { get; set; }
    public decimal FreeCashFlowPerShare { get; set; }
    public decimal CashPerShare { get; set; }
    public decimal PayoutRatio { get; set; }
    public decimal OperatingCashFlowSalesRatio { get; set; }
    public decimal FreeCashFlowOperatingCashFlowRatio { get; set; }
    public decimal CashFlowCoverageRatios { get; set; }
    public decimal ShortTermCoverageRatios { get; set; }
    public decimal CapitalExpenditureCoverageRatios { get; set; }
    public decimal DividendPaidAndCapexCoverageRatios { get; set; }
    public decimal DividendPayoutRatio { get; set; }
    public decimal PriceBookValueRatio { get; set; }
    public decimal PriceToBookRatio { get; set; }
    public decimal PriceToSalesRatio { get; set; }
    public decimal PriceEarningsRatio { get; set; }
    public decimal PriceToFreeCashFlowsRatio { get; set; }
    public decimal PriceToOperatingCashFlowsRatio { get; set; }
    public decimal PriceCashFlowRatio { get; set; }
    public decimal PriceEarningsToGrowthRatio { get; set; }
    public decimal PriceSalesRatio { get; set; }
    public decimal DividendYield { get; set; }
    public decimal EnterpriseValueMultiple { get; set; }
    public decimal PriceFairValue { get; set; }
}

public class FMPHistoricalPrice
{
    public required string Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal AdjClose { get; set; }
    public long Volume { get; set; }
    public long UnadjustedVolume { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public required string Label { get; set; }
    public decimal ChangeOverTime { get; set; }
}

public class FMPHistoricalResponse
{
    public required string Symbol { get; set; }
    public required List<FMPHistoricalPrice> Historical { get; set; }
}

public class FMPAnalystEstimates
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public long EstimatedRevenueLow { get; set; }
    public long EstimatedRevenueHigh { get; set; }
    public long EstimatedRevenueAvg { get; set; }
    public long EstimatedEbitdaLow { get; set; }
    public long EstimatedEbitdaHigh { get; set; }
    public long EstimatedEbitdaAvg { get; set; }
    public long EstimatedEbitLow { get; set; }
    public long EstimatedEbitHigh { get; set; }
    public long EstimatedEbitAvg { get; set; }
    public long EstimatedNetIncomeLow { get; set; }
    public long EstimatedNetIncomeHigh { get; set; }
    public long EstimatedNetIncomeAvg { get; set; }
    public decimal EstimatedEpsAvg { get; set; }
    public decimal EstimatedEpsHigh { get; set; }
    public decimal EstimatedEpsLow { get; set; }
    public int NumberAnalystEstimatedRevenue { get; set; }
    public int NumberAnalystsEstimatedEps { get; set; }
}

public class FMPStockScreenerCriteria
{
    public long MarketCapMin { get; set; }
    public long MarketCapMax { get; set; }
    public long VolumeMin { get; set; }
    public long VolumeMax { get; set; }
    public decimal PERatioMin { get; set; }
    public decimal PERatioMax { get; set; }
    public required string Sector { get; set; }
    public required string Industry { get; set; }
    public required string Country { get; set; }
}

public class FMPStockScreener
{
    public required string Symbol { get; set; }
    public required string CompanyName { get; set; }
    public required string MarketCap { get; set; }
    public required string Sector { get; set; }
    public required string Industry { get; set; }
    public required string Beta { get; set; }
    public required string Price { get; set; }
    public required string LastAnnualDividend { get; set; }
    public required string Volume { get; set; }
    public required string Exchange { get; set; }
    public required string ExchangeShortName { get; set; }
    public required string Country { get; set; }
    public bool IsEtf { get; set; }
    public bool IsActivelyTrading { get; set; }
}

public class FMPMarketIndex
{
    public required string Symbol { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public decimal ChangesPercentage { get; set; }
    public decimal Change { get; set; }
    public decimal DayLow { get; set; }
    public decimal DayHigh { get; set; }
    public decimal YearLow { get; set; }
    public decimal YearHigh { get; set; }
    public long MarketCap { get; set; }
    public decimal PriceAvg50 { get; set; }
    public decimal PriceAvg200 { get; set; }
    public long Volume { get; set; }
    public long AvgVolume { get; set; }
    public required string Exchange { get; set; }
    public decimal Open { get; set; }
    public decimal PreviousClose { get; set; }
    public long Timestamp { get; set; }
}

/// <summary>
/// Custom JSON converter that can handle both string and number values for decimal types
/// </summary>
public class FlexibleDecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal>
{
    public override decimal Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            return string.IsNullOrEmpty(stringValue) ? 0m : decimal.TryParse(stringValue, out var result) ? result : 0m;
        }
        else if (reader.TokenType == System.Text.Json.JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }
        return 0m;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, decimal value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// Custom JSON converter that can handle both string and number values for long types
/// </summary>
public class FlexibleNullableLongConverter : System.Text.Json.Serialization.JsonConverter<long?>
{
    public override long? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null)
        {
            return null;
        }
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            return string.IsNullOrEmpty(stringValue) ? null : long.TryParse(stringValue, out var result) ? result : null;
        }
        else if (reader.TokenType == System.Text.Json.JsonTokenType.Number)
        {
            return reader.GetInt64();
        }
        return null;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, long? value, System.Text.Json.JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

public class FlexibleNullableDecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal?>
{
    public override decimal? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null)
        {
            return null;
        }
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            return string.IsNullOrEmpty(stringValue) ? null : decimal.TryParse(stringValue, out var result) ? result : null;
        }
        else if (reader.TokenType == System.Text.Json.JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }
        return null;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, decimal? value, System.Text.Json.JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
