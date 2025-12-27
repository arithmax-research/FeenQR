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
/// IEX Cloud API service for free financial data and market information
/// Provides access to stock quotes, company info, dividends, earnings, and more
/// </summary>
public class IEXCloudService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IEXCloudService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;

    public IEXCloudService(
        HttpClient httpClient,
        ILogger<IEXCloudService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _apiKey = _configuration["IEXCloud:ApiKey"] ?? "pk_test";

        // Set user agent for API requests
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FeenQR/1.0");
    }

    /// <summary>
    /// Get real-time stock quote
    /// </summary>
    public async Task<IEXQuote> GetQuoteAsync(string symbol)
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/{symbol}/quote?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEXQuote>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting quote for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get company information
    /// </summary>
    public async Task<IEXCompany> GetCompanyAsync(string symbol)
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/{symbol}/company?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEXCompany>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting company info for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get company stats
    /// </summary>
    public async Task<IEXStats> GetStatsAsync(string symbol)
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/{symbol}/stats?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEXStats>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting stats for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get latest news for a company
    /// </summary>
    public async Task<List<IEXNews>> GetNewsAsync(string symbol, int count = 5)
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/{symbol}/news/last/{count}?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return new List<IEXNews>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<IEXNews>>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting news for {symbol}");
            return new List<IEXNews>();
        }
    }

    /// <summary>
    /// Get dividend history
    /// </summary>
    public async Task<List<IEXDividend>> GetDividendsAsync(string symbol, string range = "1y")
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/{symbol}/dividends/{range}?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return new List<IEXDividend>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<IEXDividend>>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting dividends for {symbol}");
            return new List<IEXDividend>();
        }
    }

    /// <summary>
    /// Get earnings data
    /// </summary>
    public async Task<List<IEXEarnings>> GetEarningsAsync(string symbol, int last = 4)
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/{symbol}/earnings/{last}?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return new List<IEXEarnings>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IEXEarningsResponse>(content);
            return data?.Earnings ?? new List<IEXEarnings>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting earnings for {symbol}");
            return new List<IEXEarnings>();
        }
    }

    /// <summary>
    /// Get intraday prices
    /// </summary>
    public async Task<List<IEXIntradayPrice>> GetIntradayPricesAsync(string symbol)
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/{symbol}/intraday-prices?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return new List<IEXIntradayPrice>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<IEXIntradayPrice>>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting intraday prices for {symbol}");
            return new List<IEXIntradayPrice>();
        }
    }

    /// <summary>
    /// Get historical prices
    /// </summary>
    public async Task<List<IEXHistoricalPrice>> GetHistoricalPricesAsync(string symbol, string range = "1m")
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/{symbol}/chart/{range}?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return new List<IEXHistoricalPrice>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<IEXHistoricalPrice>>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting historical prices for {symbol}");
            return new List<IEXHistoricalPrice>();
        }
    }

    /// <summary>
    /// Get market volume data
    /// </summary>
    public async Task<List<IEXMarketVolume>> GetMarketVolumeAsync()
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/market?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return new List<IEXMarketVolume>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<IEXMarketVolume>>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting market volume");
            return new List<IEXMarketVolume>();
        }
    }

    /// <summary>
    /// Get sector performance
    /// </summary>
    public async Task<List<IEXSector>> GetSectorPerformanceAsync()
    {
        try
        {
            var url = $"https://cloud.iexapis.com/stable/stock/market/sector-performance?token={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"IEX Cloud API error: {response.StatusCode}");
                return new List<IEXSector>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<IEXSector>>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting sector performance");
            return new List<IEXSector>();
        }
    }
}

// Data models for IEX Cloud API responses
public class IEXQuote
{
    public string Symbol { get; set; }
    public string CompanyName { get; set; }
    public decimal LatestPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long LatestVolume { get; set; }
    public decimal MarketCap { get; set; }
    public decimal PERatio { get; set; }
    public decimal Week52High { get; set; }
    public decimal Week52Low { get; set; }
    public decimal YtdChange { get; set; }
}

public class IEXCompany
{
    public string Symbol { get; set; }
    public string CompanyName { get; set; }
    public string Industry { get; set; }
    public string Sector { get; set; }
    public string Website { get; set; }
    public string Description { get; set; }
    public string CEO { get; set; }
    public string Employees { get; set; }
    public string Address { get; set; }
    public string State { get; set; }
    public string City { get; set; }
    public string Zip { get; set; }
    public string Country { get; set; }
}

public class IEXStats
{
    public string CompanyName { get; set; }
    public decimal Marketcap { get; set; }
    public decimal Week52high { get; set; }
    public decimal Week52low { get; set; }
    public decimal Week52change { get; set; }
    public long SharesOutstanding { get; set; }
    public decimal Float { get; set; }
    public decimal Avg10Volume { get; set; }
    public decimal Avg30Volume { get; set; }
    public decimal Day200MovingAvg { get; set; }
    public decimal Day50MovingAvg { get; set; }
    public decimal Employees { get; set; }
    public decimal TtmEPS { get; set; }
    public decimal TtmDividendRate { get; set; }
    public decimal DividendYield { get; set; }
    public string NextDividendDate { get; set; }
    public string ExDividendDate { get; set; }
    public string NextEarningsDate { get; set; }
    public decimal PeRatio { get; set; }
    public decimal Beta { get; set; }
}

public class IEXNews
{
    public string Headline { get; set; }
    public string Source { get; set; }
    public string Url { get; set; }
    public string Summary { get; set; }
    public string Related { get; set; }
    public long Datetime { get; set; }
    public string Image { get; set; }
    public bool HasPaywall { get; set; }
}

public class IEXDividend
{
    public string ExDate { get; set; }
    public string PaymentDate { get; set; }
    public string RecordDate { get; set; }
    public string DeclaredDate { get; set; }
    public string Amount { get; set; }
    public string Flag { get; set; }
    public string Type { get; set; }
    public string Qualified { get; set; }
    public string Indicated { get; set; }
}

public class IEXEarnings
{
    public string ActualEPS { get; set; }
    public string ConsensusEPS { get; set; }
    public string EstimatedEPS { get; set; }
    public string AnnounceTime { get; set; }
    public long NumberOfEstimates { get; set; }
    public long EPSSurpriseDollar { get; set; }
    public string EPSReportDate { get; set; }
    public string FiscalPeriod { get; set; }
    public string FiscalEndDate { get; set; }
    public string YearAgo { get; set; }
    public string YearAgoChangePercent { get; set; }
}

public class IEXEarningsResponse
{
    public string Symbol { get; set; }
    public List<IEXEarnings> Earnings { get; set; }
}

public class IEXIntradayPrice
{
    public string Date { get; set; }
    public string Minute { get; set; }
    public string Label { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Open { get; set; }
    public decimal Close { get; set; }
    public decimal Average { get; set; }
    public long Volume { get; set; }
    public decimal Notional { get; set; }
    public long NumberOfTrades { get; set; }
    public decimal MarketHigh { get; set; }
    public decimal MarketLow { get; set; }
    public decimal MarketAverage { get; set; }
    public long MarketVolume { get; set; }
    public decimal MarketNotional { get; set; }
    public long MarketNumberOfTrades { get; set; }
    public decimal MarketOpen { get; set; }
    public decimal MarketClose { get; set; }
    public decimal ChangeOverTime { get; set; }
    public decimal MarketChangeOverTime { get; set; }
}

public class IEXHistoricalPrice
{
    public string Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public string UOpen { get; set; }
    public string UHigh { get; set; }
    public string ULow { get; set; }
    public string UClose { get; set; }
    public string UVolume { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public string Label { get; set; }
    public decimal ChangeOverTime { get; set; }
}

public class IEXMarketVolume
{
    public string Mic { get; set; }
    public string TapeId { get; set; }
    public string VenueName { get; set; }
    public long Volume { get; set; }
    public long TapeA { get; set; }
    public long TapeB { get; set; }
    public long TapeC { get; set; }
    public long MarketPercent { get; set; }
    public string LastUpdated { get; set; }
}

public class IEXSector
{
    public string Type { get; set; }
    public string Name { get; set; }
    public decimal Performance { get; set; }
    public string LastUpdated { get; set; }
}
