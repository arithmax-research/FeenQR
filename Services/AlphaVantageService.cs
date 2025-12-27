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
    private readonly string _apiKey;

    public AlphaVantageService(
        HttpClient httpClient,
        ILogger<AlphaVantageService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _apiKey = _configuration["AlphaVantage:ApiKey"] ?? "demo";

        // Set user agent for API requests
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FeenQR/1.0");
    }

    /// <summary>
    /// Get real-time stock quote
    /// </summary>
    public async Task<AlphaVantageQuote> GetQuoteAsync(string symbol)
    {
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
        try
        {
            var url = $"https://www.alphavantage.co/query?function=OVERVIEW&symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Alpha Vantage API error: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AlphaVantageCompanyOverview>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting company overview for {symbol}");
            return null;
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
    public long MarketCapitalization { get; set; }

    [JsonPropertyName("PERatio")]
    public decimal PERatio { get; set; }

    [JsonPropertyName("PEGRatio")]
    public decimal PEGRatio { get; set; }

    [JsonPropertyName("BookValue")]
    public decimal BookValue { get; set; }

    [JsonPropertyName("DividendPerShare")]
    public decimal DividendPerShare { get; set; }

    [JsonPropertyName("DividendYield")]
    public decimal DividendYield { get; set; }

    [JsonPropertyName("EPS")]
    public decimal EPS { get; set; }

    [JsonPropertyName("RevenuePerShareTTM")]
    public decimal RevenuePerShareTTM { get; set; }

    [JsonPropertyName("ProfitMargin")]
    public decimal ProfitMargin { get; set; }

    [JsonPropertyName("OperatingMarginTTM")]
    public decimal OperatingMarginTTM { get; set; }

    [JsonPropertyName("ReturnOnAssetsTTM")]
    public decimal ReturnOnAssetsTTM { get; set; }

    [JsonPropertyName("ReturnOnEquityTTM")]
    public decimal ReturnOnEquityTTM { get; set; }

    [JsonPropertyName("QuarterlyEarningsGrowthYOY")]
    public decimal QuarterlyEarningsGrowthYOY { get; set; }

    [JsonPropertyName("QuarterlyRevenueGrowthYOY")]
    public decimal QuarterlyRevenueGrowthYOY { get; set; }

    [JsonPropertyName("AnalystTargetPrice")]
    public decimal AnalystTargetPrice { get; set; }

    [JsonPropertyName("TrailingPE")]
    public decimal TrailingPE { get; set; }

    [JsonPropertyName("ForwardPE")]
    public decimal ForwardPE { get; set; }

    [JsonPropertyName("PriceToSalesRatioTTM")]
    public decimal PriceToSalesRatioTTM { get; set; }

    [JsonPropertyName("PriceToBookRatio")]
    public decimal PriceToBookRatio { get; set; }

    [JsonPropertyName("EVToRevenue")]
    public decimal EVToRevenue { get; set; }

    [JsonPropertyName("EVToEBITDA")]
    public decimal EVToEBITDA { get; set; }

    [JsonPropertyName("Beta")]
    public decimal Beta { get; set; }

    [JsonPropertyName("FiftyTwoWeekHigh")]
    public decimal FiftyTwoWeekHigh { get; set; }

    [JsonPropertyName("FiftyTwoWeekLow")]
    public decimal FiftyTwoWeekLow { get; set; }

    [JsonPropertyName("FiftyDayMovingAverage")]
    public decimal FiftyDayMovingAverage { get; set; }

    [JsonPropertyName("TwoHundredDayMovingAverage")]
    public decimal TwoHundredDayMovingAverage { get; set; }

    [JsonPropertyName("SharesOutstanding")]
    public long SharesOutstanding { get; set; }

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
