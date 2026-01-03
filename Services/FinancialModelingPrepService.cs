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
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;

    public FinancialModelingPrepService(
        HttpClient httpClient,
        ILogger<FinancialModelingPrepService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _apiKey = _configuration["FMP:ApiKey"] ?? "demo";

        // Configure JSON deserializer to be case-insensitive
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Set user agent for API requests
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FeenQR/1.0");
    }

    /// <summary>
    /// Get company profile
    /// </summary>
    public async Task<FMPCompanyProfile> GetCompanyProfileAsync(string symbol)
    {
        try
        {
            var url = $"https://financialmodelingprep.com/stable/profile?symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
        try
        {
            var url = $"https://financialmodelingprep.com/stable/quote?symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
        try
        {
            var url = $"https://financialmodelingprep.com/stable/income-statement?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
        try
        {
            var url = $"https://financialmodelingprep.com/stable/balance-sheet-statement?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
        try
        {
            var url = $"https://financialmodelingprep.com/stable/cash-flow-statement?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
        try
        {
            var url = $"https://financialmodelingprep.com/stable/key-metrics?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
        try
        {
            var url = $"https://financialmodelingprep.com/stable/ratios?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
        try
        {
            var url = $"https://financialmodelingprep.com/stable/historical-price-full/{symbol}?from={from}&to={to}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
            var url = $"https://financialmodelingprep.com/stable/analyst-estimates?symbol={symbol}&limit={limit}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
                _logger.LogWarning($"FMP API error: {response.StatusCode}");
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
    public long Revenue { get; set; }
    public long CostOfRevenue { get; set; }
    public long GrossProfit { get; set; }
    public long GrossProfitRatio { get; set; }
    public long ResearchAndDevelopmentExpenses { get; set; }
    public long GeneralAndAdministrativeExpenses { get; set; }
    public long SellingAndMarketingExpenses { get; set; }
    public long OtherExpenses { get; set; }
    public long OperatingExpenses { get; set; }
    public long CostAndExpenses { get; set; }
    public long InterestExpense { get; set; }
    public long DepreciationAndAmortization { get; set; }
    public long Ebitda { get; set; }
    public long Ebitdaratio { get; set; }
    public long OperatingIncome { get; set; }
    public long OperatingIncomeRatio { get; set; }
    public long TotalOtherIncomeExpensesNet { get; set; }
    public long IncomeBeforeTax { get; set; }
    public long IncomeBeforeTaxRatio { get; set; }
    public long IncomeTaxExpense { get; set; }
    public long NetIncome { get; set; }
    public long NetIncomeRatio { get; set; }
    public long Eps { get; set; }
    public long Epsdiluted { get; set; }
    public long WeightedAverageShsOut { get; set; }
    public long WeightedAverageShsOutDil { get; set; }
}

public class FMPBalanceSheet
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public long CashAndCashEquivalents { get; set; }
    public long ShortTermInvestments { get; set; }
    public long CashAndShortTermInvestments { get; set; }
    public long NetReceivables { get; set; }
    public long Inventory { get; set; }
    public long OtherCurrentAssets { get; set; }
    public long TotalCurrentAssets { get; set; }
    public long PropertyPlantEquipmentNet { get; set; }
    public long Goodwill { get; set; }
    public long IntangibleAssets { get; set; }
    public long GoodwillAndIntangibleAssets { get; set; }
    public long LongTermInvestments { get; set; }
    public long TaxAssets { get; set; }
    public long OtherNonCurrentAssets { get; set; }
    public long TotalNonCurrentAssets { get; set; }
    public long OtherAssets { get; set; }
    public long TotalAssets { get; set; }
    public long AccountPayables { get; set; }
    public long ShortTermDebt { get; set; }
    public long TaxPayables { get; set; }
    public long DeferredRevenue { get; set; }
    public long OtherCurrentLiabilities { get; set; }
    public long TotalCurrentLiabilities { get; set; }
    public long LongTermDebt { get; set; }
    public long DeferredRevenueNonCurrent { get; set; }
    public long DeferredTaxLiabilitiesNonCurrent { get; set; }
    public long OtherNonCurrentLiabilities { get; set; }
    public long TotalNonCurrentLiabilities { get; set; }
    public long OtherLiabilities { get; set; }
    public long TotalLiabilities { get; set; }
    public long CommonStock { get; set; }
    public long RetainedEarnings { get; set; }
    public long AccumulatedOtherComprehensiveIncomeLoss { get; set; }
    public long OthertotalStockholdersEquity { get; set; }
    public long TotalStockholdersEquity { get; set; }
    public long TotalLiabilitiesAndStockholdersEquity { get; set; }
    public long MinorityInterest { get; set; }
    public long TotalEquity { get; set; }
    public long TotalLiabilitiesAndTotalEquity { get; set; }
    public long TotalInvestments { get; set; }
    public long TotalDebt { get; set; }
    public long NetDebt { get; set; }
}

public class FMPCashFlow
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public long NetIncome { get; set; }
    public long DepreciationAndAmortization { get; set; }
    public long DeferredIncomeTax { get; set; }
    public long StockBasedCompensation { get; set; }
    public long ChangeInWorkingCapital { get; set; }
    public long AccountsReceivables { get; set; }
    public long Inventory { get; set; }
    public long AccountsPayables { get; set; }
    public long OtherWorkingCapital { get; set; }
    public long OtherNonCashItems { get; set; }
    public long NetCashProvidedByOperatingActivities { get; set; }
    public long InvestmentsInPropertyPlantAndEquipment { get; set; }
    public long AcquisitionsNet { get; set; }
    public long PurchasesOfInvestments { get; set; }
    public long SalesMaturitiesOfInvestments { get; set; }
    public long OtherInvestingActivites { get; set; }
    public long NetCashUsedForInvestingActivites { get; set; }
    public long DebtRepayment { get; set; }
    public long CommonStockIssued { get; set; }
    public long CommonStockRepurchased { get; set; }
    public long DividendsPaid { get; set; }
    public long OtherFinancingActivites { get; set; }
    public long NetCashUsedProvidedByFinancingActivities { get; set; }
    public long EffectOfForexChangesOnCash { get; set; }
    public long NetChangeInCash { get; set; }
    public long CashAtEndOfPeriod { get; set; }
    public long CashAtBeginningOfPeriod { get; set; }
    public long OperatingCashFlow { get; set; }
    public long CapitalExpenditure { get; set; }
    public long FreeCashFlow { get; set; }
}

public class FMPKeyMetrics
{
    public required string Date { get; set; }
    public required string Symbol { get; set; }
    public required string Period { get; set; }
    public decimal RevenuePerShare { get; set; }
    public decimal NetIncomePerShare { get; set; }
    public decimal OperatingCashFlowPerShare { get; set; }
    public decimal FreeCashFlowPerShare { get; set; }
    public decimal CashPerShare { get; set; }
    public decimal BookValuePerShare { get; set; }
    public decimal TangibleBookValuePerShare { get; set; }
    public decimal ShareholdersEquityPerShare { get; set; }
    public decimal InterestDebtPerShare { get; set; }
    public decimal MarketCap { get; set; }
    public decimal EnterpriseValue { get; set; }
    public decimal PeRatio { get; set; }
    public decimal PriceToSalesRatio { get; set; }
    public decimal Pocfratio { get; set; }
    public decimal PfcfRatio { get; set; }
    public decimal PbRatio { get; set; }
    public decimal Ptbratio { get; set; }
    public decimal EvToSales { get; set; }
    public decimal EnterpriseValueOverEBITDA { get; set; }
    public decimal EvToOperatingCashFlow { get; set; }
    public decimal EvToFreeCashFlow { get; set; }
    public decimal EarningsYield { get; set; }
    public decimal FreeCashFlowYield { get; set; }
    public decimal DebtToEquity { get; set; }
    public decimal DebtToAssets { get; set; }
    public decimal NetDebtToEBITDA { get; set; }
    public decimal CurrentRatio { get; set; }
    public decimal InterestCoverage { get; set; }
    public decimal IncomeQuality { get; set; }
    public decimal DividendYield { get; set; }
    public decimal PayoutRatio { get; set; }
    public decimal SalesGeneralAndAdministrativeToRevenue { get; set; }
    public decimal ResearchAndDdevelopementToRevenue { get; set; }
    public decimal IntangiblesToTotalAssets { get; set; }
    public decimal CapexToOperatingCashFlow { get; set; }
    public decimal CapexToRevenue { get; set; }
    public decimal CapexToDepreciation { get; set; }
    public decimal StockBasedCompensationToRevenue { get; set; }
    public decimal GrahamNumber { get; set; }
    public decimal Roic { get; set; }
    public decimal ReturnOnTangibleAssets { get; set; }
    public decimal GrahamNetNet { get; set; }
    public decimal WorkingCapital { get; set; }
    public decimal TangibleAssetValue { get; set; }
    public decimal NetCurrentAssetValue { get; set; }
    public decimal InvestedCapital { get; set; }
    public decimal AverageReceivables { get; set; }
    public decimal AveragePayables { get; set; }
    public decimal AverageInventory { get; set; }
    public decimal DaysSalesOutstanding { get; set; }
    public decimal DaysPayablesOutstanding { get; set; }
    public decimal DaysOfInventoryOnHand { get; set; }
    public decimal ReceivablesTurnover { get; set; }
    public decimal PayablesTurnover { get; set; }
    public decimal InventoryTurnover { get; set; }
    public decimal Roe { get; set; }
    public decimal CapexPerShare { get; set; }
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
    public decimal UnadjustedVolume { get; set; }
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
    public decimal EstimatedRevenueLow { get; set; }
    public decimal EstimatedRevenueHigh { get; set; }
    public decimal EstimatedRevenueAvg { get; set; }
    public decimal EstimatedEbitdaLow { get; set; }
    public decimal EstimatedEbitdaHigh { get; set; }
    public decimal EstimatedEbitdaAvg { get; set; }
    public decimal EstimatedEbitLow { get; set; }
    public decimal EstimatedEbitHigh { get; set; }
    public decimal EstimatedEbitAvg { get; set; }
    public decimal EstimatedNetIncomeLow { get; set; }
    public decimal EstimatedNetIncomeHigh { get; set; }
    public decimal EstimatedNetIncomeAvg { get; set; }
    public decimal EstimatedEpsAvg { get; set; }
    public decimal EstimatedEpsHigh { get; set; }
    public decimal EstimatedEpsLow { get; set; }
    public long NumberAnalystEstimatedRevenue { get; set; }
    public long NumberAnalystsEstimatedEps { get; set; }
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
    public decimal MarketCap { get; set; }
    public decimal PriceAvg50 { get; set; }
    public decimal PriceAvg200 { get; set; }
    public long Volume { get; set; }
    public long AvgVolume { get; set; }
    public required string Exchange { get; set; }
    public decimal Open { get; set; }
    public decimal PreviousClose { get; set; }
    public long Timestamp { get; set; }
}
