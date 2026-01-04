using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services;

/// <summary>
/// Enhanced Fundamental Analysis service that combines data from multiple free sources
/// Provides comprehensive fundamental analysis with integrated data sources
/// Fallback order: FMP -> Alpha Vantage -> Yahoo Finance -> Alpaca -> DataBento
/// </summary>
public class EnhancedFundamentalAnalysisService
{
    private readonly AlphaVantageService _alphaVantageService;
    private readonly FinancialModelingPrepService _fmpService;
    private readonly YFinanceApiService _yfinanceService;
    private readonly AlpacaService _alpacaService;
    private readonly DataBentoService _databentoService;
    private readonly ILogger<EnhancedFundamentalAnalysisService> _logger;
    private readonly LLMRouterService _llmService;

    public EnhancedFundamentalAnalysisService(
        AlphaVantageService alphaVantageService,
        FinancialModelingPrepService fmpService,
        YFinanceApiService yfinanceService,
        AlpacaService alpacaService,
        DataBentoService databentoService,
        ILogger<EnhancedFundamentalAnalysisService> logger,
        LLMRouterService llmService)
    {
        _alphaVantageService = alphaVantageService;
        _fmpService = fmpService;
        _yfinanceService = yfinanceService;
        _alpacaService = alpacaService;
        _databentoService = databentoService;
        _logger = logger;
        _llmService = llmService;
    }

    private decimal ParseDecimal(object value)
    {
        if (value is string s)
            return decimal.TryParse(s, out var result) ? result : 0m;
        if (value is decimal d)
            return d;
        return 0m;
    }

    /// <summary>
    /// Get comprehensive company overview combining multiple data sources
    /// Fallback order: FMP -> Alpha Vantage -> Yahoo Finance -> Alpaca -> DataBento
    /// </summary>
    public async Task<EnhancedCompanyOverview> GetComprehensiveCompanyOverviewAsync(string symbol)
    {
        try
        {
            // Get data from multiple sources in parallel
            var fmpProfileTask = _fmpService.GetCompanyProfileAsync(symbol);
            var fmpKeyMetricsTask = _fmpService.GetKeyMetricsAsync(symbol, 1); // Get latest metrics
            var fmpRatiosTask = _fmpService.GetFinancialRatiosAsync(symbol, 1); // Get latest ratios
            var fmpQuoteTask = _fmpService.GetQuoteAsync(symbol);
            var alphaVantageOverviewTask = _alphaVantageService.GetCompanyOverviewAsync(symbol);
            var yfinanceTask = _yfinanceService.GetFundamentalsAsync(symbol);
            var alpacaMarketDataTask = _alpacaService.GetMarketDataAsync(symbol);

            await Task.WhenAll(fmpProfileTask, fmpKeyMetricsTask, fmpRatiosTask, fmpQuoteTask, alphaVantageOverviewTask, yfinanceTask, alpacaMarketDataTask);

            var fmpProfile = await fmpProfileTask;
            var fmpKeyMetrics = await fmpKeyMetricsTask;
            var fmpRatios = await fmpRatiosTask;
            var fmpQuote = await fmpQuoteTask;
            var alphaVantageOverview = await alphaVantageOverviewTask;
            var yfinance = await yfinanceTask;
            var alpacaData = await alpacaMarketDataTask;

            // Get the first key metrics result
            var keyMetrics = fmpKeyMetrics?.FirstOrDefault();
            var ratios = fmpRatios?.FirstOrDefault();

            _logger.LogInformation($"Data sources for {symbol} - FMP Profile: {fmpProfile != null}, FMP Metrics: {keyMetrics != null}, FMP Ratios: {ratios != null}, AlphaVantage: {alphaVantageOverview != null}, YFinance: {yfinance != null}, Alpaca: {alpacaData != null}");

            // Combine data from all sources with cascading fallback logic: FMP -> Alpha Vantage -> YFinance -> Alpaca
            var overview = new EnhancedCompanyOverview
            {
                Symbol = symbol,
                CompanyName = fmpProfile?.CompanyName ?? alphaVantageOverview?.Name,
                Description = fmpProfile?.Description ?? alphaVantageOverview?.Description ?? yfinance?.LongBusinessSummary,
                Industry = fmpProfile?.Industry ?? alphaVantageOverview?.Industry ?? yfinance?.Industry,
                Sector = fmpProfile?.Sector ?? alphaVantageOverview?.Sector ?? yfinance?.Sector,
                Exchange = fmpQuote?.Exchange ?? alphaVantageOverview?.Exchange,
                Country = fmpProfile?.Country ?? alphaVantageOverview?.Country,
                Website = fmpProfile?.Website ?? alphaVantageOverview?.Website,
                CEO = fmpProfile?.CEO,
                Employees = int.TryParse(fmpProfile?.FullTimeEmployees, out var emp) ? emp : 0,
                MarketCap = fmpQuote?.MarketCap > 0.0m ? (long)fmpQuote.MarketCap : (long.TryParse(alphaVantageOverview?.MarketCapitalization, out var mc) ? mc : (yfinance?.MarketCap ?? 0L)),
                
                // P/E Ratio - triple fallback: FMP -> Alpha Vantage -> YFinance
                PERatio = (keyMetrics?.PeRatio ?? 0m) > 0 ? keyMetrics.PeRatio.Value : (ParseDecimal(alphaVantageOverview?.PERatio) > 0 ? ParseDecimal(alphaVantageOverview?.PERatio) : (yfinance?.TrailingPE ?? 0m)),
                
                // PEG Ratio - triple fallback
                PEGRatio = (ratios?.PriceEarningsToGrowthRatio ?? 0m) > 0 ? ratios.PriceEarningsToGrowthRatio : (ParseDecimal(alphaVantageOverview?.PEGRatio) > 0 ? ParseDecimal(alphaVantageOverview?.PEGRatio) : (yfinance?.PegRatio ?? 0m)),
                
                BookValue = keyMetrics?.BookValuePerShare ?? (ParseDecimal(alphaVantageOverview?.BookValue) > 0 ? ParseDecimal(alphaVantageOverview?.BookValue) : (yfinance?.PriceToBook ?? 0m)),
                DividendPerShare = keyMetrics?.DividendYield ?? (ParseDecimal(alphaVantageOverview?.DividendPerShare) > 0 ? ParseDecimal(alphaVantageOverview?.DividendPerShare) : 0m),
                DividendYield = ratios?.DividendYield ?? (ParseDecimal(alphaVantageOverview?.DividendYield) > 0 ? ParseDecimal(alphaVantageOverview?.DividendYield) : (yfinance?.DividendYield ?? 0m)),
                
                // EPS - triple fallback
                EPS = keyMetrics?.NetIncomePerShare ?? (ParseDecimal(alphaVantageOverview?.EPS) > 0 ? ParseDecimal(alphaVantageOverview?.EPS) : (yfinance?.TrailingPE > 0 && yfinance?.CurrentPrice > 0 ? (yfinance.CurrentPrice.Value / yfinance.TrailingPE.Value) : 0m)),
                
                RevenuePerShareTTM = keyMetrics?.RevenuePerShare ?? (ParseDecimal(alphaVantageOverview?.RevenuePerShareTTM) > 0 ? ParseDecimal(alphaVantageOverview?.RevenuePerShareTTM) : 0m),
                
                // Profitability Metrics - triple fallback
                ProfitMargin = (ratios?.NetProfitMargin ?? 0m) > 0 ? ratios.NetProfitMargin : (ParseDecimal(alphaVantageOverview?.ProfitMargin) > 0 ? ParseDecimal(alphaVantageOverview?.ProfitMargin) : 0m),
                OperatingMarginTTM = (ratios?.OperatingMargin ?? 0m) > 0 ? ratios.OperatingMargin : (ParseDecimal(alphaVantageOverview?.OperatingMarginTTM) > 0 ? ParseDecimal(alphaVantageOverview?.OperatingMarginTTM) : 0m),
                ReturnOnAssetsTTM = (ratios?.ReturnOnAssets ?? 0m) > 0 ? ratios.ReturnOnAssets : (ParseDecimal(alphaVantageOverview?.ReturnOnAssetsTTM) > 0 ? ParseDecimal(alphaVantageOverview?.ReturnOnAssetsTTM) : 0m),
                ReturnOnEquityTTM = (ratios?.ReturnOnEquity ?? 0m) > 0 ? ratios.ReturnOnEquity : (ParseDecimal(alphaVantageOverview?.ReturnOnEquityTTM) > 0 ? ParseDecimal(alphaVantageOverview?.ReturnOnEquityTTM) : (yfinance?.ReturnOnEquity ?? 0m)),
                
                QuarterlyEarningsGrowthYOY = (ParseDecimal(alphaVantageOverview?.QuarterlyEarningsGrowthYOY) > 0 ? ParseDecimal(alphaVantageOverview?.QuarterlyEarningsGrowthYOY) : (yfinance?.EarningsGrowth ?? 0m)),
                QuarterlyRevenueGrowthYOY = (ParseDecimal(alphaVantageOverview?.QuarterlyRevenueGrowthYOY) > 0 ? ParseDecimal(alphaVantageOverview?.QuarterlyRevenueGrowthYOY) : (yfinance?.RevenueGrowth ?? 0m)),
                AnalystTargetPrice = ParseDecimal(alphaVantageOverview?.AnalystTargetPrice),
                TrailingPE = (keyMetrics?.PeRatio ?? 0m) > 0 ? keyMetrics.PeRatio.Value : (ParseDecimal(alphaVantageOverview?.PERatio) > 0 ? ParseDecimal(alphaVantageOverview?.PERatio) : (yfinance?.TrailingPE ?? 0m)),
                ForwardPE = ParseDecimal(alphaVantageOverview?.ForwardPE) > 0 ? ParseDecimal(alphaVantageOverview?.ForwardPE) : (yfinance?.ForwardPE ?? 0m),
                PriceToSalesRatioTTM = keyMetrics?.PriceToSalesRatio ?? (ParseDecimal(alphaVantageOverview?.PriceToSalesRatioTTM) > 0 ? ParseDecimal(alphaVantageOverview?.PriceToSalesRatioTTM) : (yfinance?.PriceToSalesTrailing12Months ?? 0m)),
                PriceToBookRatio = keyMetrics?.PbRatio ?? (ParseDecimal(alphaVantageOverview?.PriceToBookRatio) > 0 ? ParseDecimal(alphaVantageOverview?.PriceToBookRatio) : (yfinance?.PriceToBook ?? 0m)),
                EVToRevenue = (keyMetrics?.EvToSales ?? 0m) > 0 ? keyMetrics.EvToSales.Value : (ParseDecimal(alphaVantageOverview?.EVToRevenue) > 0 ? ParseDecimal(alphaVantageOverview?.EVToRevenue) : 0m),
                EVToEBITDA = (keyMetrics?.EnterpriseValueOverEBITDA ?? 0m) > 0 ? keyMetrics.EnterpriseValueOverEBITDA.Value : (ParseDecimal(alphaVantageOverview?.EVToEBITDA) > 0 ? ParseDecimal(alphaVantageOverview?.EVToEBITDA) : 0m),
                Beta = ParseDecimal(alphaVantageOverview?.Beta) > 0 ? ParseDecimal(alphaVantageOverview?.Beta) : (yfinance?.Beta ?? 0m),
                FiftyTwoWeekHigh = fmpQuote?.YearHigh ?? (ParseDecimal(alphaVantageOverview?.FiftyTwoWeekHigh) > 0 ? ParseDecimal(alphaVantageOverview?.FiftyTwoWeekHigh) : (yfinance?.FiftyTwoWeekHigh ?? 0m)),
                FiftyTwoWeekLow = fmpQuote?.YearLow ?? (ParseDecimal(alphaVantageOverview?.FiftyTwoWeekLow) > 0 ? ParseDecimal(alphaVantageOverview?.FiftyTwoWeekLow) : (yfinance?.FiftyTwoWeekLow ?? 0m)),
                FiftyDayMovingAverage = fmpQuote?.PriceAvg50 ?? (ParseDecimal(alphaVantageOverview?.FiftyDayMovingAverage) > 0 ? ParseDecimal(alphaVantageOverview?.FiftyDayMovingAverage) : 0m),
                TwoHundredDayMovingAverage = fmpQuote?.PriceAvg200 ?? (ParseDecimal(alphaVantageOverview?.TwoHundredDayMovingAverage) > 0 ? ParseDecimal(alphaVantageOverview?.TwoHundredDayMovingAverage) : 0m),
                SharesOutstanding = fmpQuote?.SharesOutstanding > 0 ? fmpQuote.SharesOutstanding : (long.TryParse(alphaVantageOverview?.SharesOutstanding, out var so) ? so : (yfinance?.SharesOutstanding ?? 0L)),
                DividendDate = alphaVantageOverview?.DividendDate,
                ExDividendDate = alphaVantageOverview?.ExDividendDate,
                LastSplitFactor = alphaVantageOverview?.LastSplitFactor,
                LastSplitDate = alphaVantageOverview?.LastSplitDate,
                IpoDate = fmpProfile?.IpoDate,
                IsActivelyTrading = fmpProfile?.IsActivelyTrading ?? false
            };

            return overview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting comprehensive overview for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get comprehensive financial statements analysis
    /// </summary>
    public async Task<EnhancedFinancialStatements> GetComprehensiveFinancialStatementsAsync(string symbol)
    {
        try
        {
            // Get data from FMP
            var fmpIncomeTask = _fmpService.GetIncomeStatementAsync(symbol, 4);
            var fmpBalanceTask = _fmpService.GetBalanceSheetAsync(symbol, 4);
            var fmpCashFlowTask = _fmpService.GetCashFlowAsync(symbol, 4);

            await Task.WhenAll(fmpIncomeTask, fmpBalanceTask, fmpCashFlowTask);

            var fmpIncome = await fmpIncomeTask;
            var fmpBalance = await fmpBalanceTask;
            var fmpCashFlow = await fmpCashFlowTask;

            // Convert FMP data to Enhanced types
            var statements = new EnhancedFinancialStatements
            {
                Symbol = symbol,
                IncomeStatements = fmpIncome?.Select(f => new EnhancedIncomeStatement
                {
                    Date = f.Date,
                    Period = f.Period,
                    Revenue = f.Revenue ?? 0L,
                    CostOfRevenue = f.CostOfRevenue ?? 0L,
                    GrossProfit = f.GrossProfit ?? 0L,
                    OperatingExpenses = f.OperatingExpenses ?? 0L,
                    OperatingIncome = f.OperatingIncome ?? 0L,
                    NetIncome = f.NetIncome ?? 0L,
                    EPS = (decimal)(f.Eps ?? 0m),
                    EPSDiluted = (decimal)(f.Epsdiluted ?? 0m)
                }).ToList() ?? new List<EnhancedIncomeStatement>(),
                BalanceSheets = fmpBalance?.Select(f => new EnhancedBalanceSheet
                {
                    Date = f.Date,
                    Period = f.Period,
                    TotalAssets = f.TotalAssets ?? 0L,
                    TotalLiabilities = f.TotalLiabilities ?? 0L,
                    TotalEquity = f.TotalEquity ?? 0L,
                    CashAndEquivalents = f.CashAndCashEquivalents ?? 0L,
                    TotalCurrentAssets = f.TotalCurrentAssets ?? 0L,
                    TotalCurrentLiabilities = f.TotalCurrentLiabilities ?? 0L,
                    TotalDebt = (f.ShortTermDebt ?? 0L) + (f.LongTermDebt ?? 0L),
                    LongTermDebt = f.LongTermDebt ?? 0L
                }).ToList() ?? new List<EnhancedBalanceSheet>(),
                CashFlowStatements = fmpCashFlow?.Select(f => new EnhancedCashFlow
                {
                    Date = f.Date,
                    Period = f.Period,
                    OperatingCashFlow = f.OperatingCashFlow ?? 0L,
                    InvestingCashFlow = f.NetCashProvidedByInvestingActivities ?? 0L,
                    FinancingCashFlow = f.NetCashProvidedByFinancingActivities ?? 0L,
                    FreeCashFlow = f.FreeCashFlow ?? 0L,
                    CapitalExpenditure = f.CapitalExpenditure ?? 0L,
                    NetChangeInCash = f.NetChangeInCash ?? 0L
                }).ToList() ?? new List<EnhancedCashFlow>(),
                LastUpdated = DateTime.UtcNow
            };

            return statements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting comprehensive financial statements for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get comprehensive valuation analysis with multi-source fallback (FMP -> Alpha Vantage -> Yahoo Finance)
    /// </summary>
    public async Task<EnhancedValuationAnalysis> GetComprehensiveValuationAnalysisAsync(string symbol)
    {
        try
        {
            // Get data from FMP, Alpha Vantage, and Yahoo Finance in parallel
            var fmpQuoteTask = _fmpService.GetQuoteAsync(symbol);
            var fmpMetricsTask = _fmpService.GetKeyMetricsAsync(symbol, 1);
            var fmpRatiosTask = _fmpService.GetFinancialRatiosAsync(symbol, 1);
            var fmpProfileTask = _fmpService.GetCompanyProfileAsync(symbol);
            var alphaVantageTask = _alphaVantageService.GetCompanyOverviewAsync(symbol);
            var yfinanceTask = _yfinanceService.GetFundamentalsAsync(symbol);

            await Task.WhenAll(fmpQuoteTask, fmpMetricsTask, fmpRatiosTask, fmpProfileTask, alphaVantageTask, yfinanceTask);

            var quote = await fmpQuoteTask;
            var metrics = (await fmpMetricsTask)?.FirstOrDefault();
            var ratios = (await fmpRatiosTask)?.FirstOrDefault();
            var profile = await fmpProfileTask;
            var alphaVantageOverview = await alphaVantageTask;
            var yfinance = await yfinanceTask;

            _logger.LogInformation($"Valuation data sources for {symbol} - FMP Quote: {quote != null}, FMP Metrics: {metrics != null}, FMP Ratios: {ratios != null}, AlphaVantage: {alphaVantageOverview != null}, YFinance: {yfinance != null}");


            // Calculate valuation metrics using FMP -> Alpha Vantage -> Yahoo Finance fallback
            var analysis = new EnhancedValuationAnalysis
            {
                Symbol = symbol,
                CurrentPrice = quote?.Price ?? (yfinance?.CurrentPrice ?? 0m),
                MarketCap = quote?.MarketCap ?? 0m,
                SharesOutstanding = quote?.SharesOutstanding ?? 0L,

                // Price multiples with fallbacks
                PERatio = (ratios?.PriceEarningsRatio ?? 0m) > 0 ? ratios.PriceEarningsRatio : (ParseDecimal(alphaVantageOverview?.PERatio) > 0 ? ParseDecimal(alphaVantageOverview?.PERatio) : (yfinance?.TrailingPE ?? 0m)),
                PriceToBook = (ratios?.PriceToBookRatio ?? 0m) > 0 ? ratios.PriceToBookRatio : (ParseDecimal(alphaVantageOverview?.PriceToBookRatio) > 0 ? ParseDecimal(alphaVantageOverview?.PriceToBookRatio) : (yfinance?.PriceToBook ?? 0m)),
                PriceToSales = (ratios?.PriceToSalesRatio ?? 0m) > 0 ? ratios.PriceToSalesRatio : (ParseDecimal(alphaVantageOverview?.PriceToSalesRatioTTM) > 0 ? ParseDecimal(alphaVantageOverview?.PriceToSalesRatioTTM) : (yfinance?.PriceToSalesTrailing12Months ?? 0m)),
                EVToEBITDA = (metrics?.EnterpriseValueOverEBITDA ?? 0m) > 0 ? metrics.EnterpriseValueOverEBITDA.Value : (ParseDecimal(alphaVantageOverview?.EVToEBITDA) > 0 ? ParseDecimal(alphaVantageOverview?.EVToEBITDA) : 0m),
                EVToRevenue = (metrics?.EvToSales ?? 0m) > 0 ? metrics.EvToSales.Value : (ParseDecimal(alphaVantageOverview?.EVToRevenue) > 0 ? ParseDecimal(alphaVantageOverview?.EVToRevenue) : 0m),

                // Growth metrics
                EPS = quote?.Eps ?? (ParseDecimal(alphaVantageOverview?.EPS) > 0 ? ParseDecimal(alphaVantageOverview?.EPS) : 0m),
                EPSGrowth = ParseDecimal(alphaVantageOverview?.QuarterlyEarningsGrowthYOY) > 0 ? ParseDecimal(alphaVantageOverview?.QuarterlyEarningsGrowthYOY) : (yfinance?.EarningsGrowth ?? 0m),
                RevenueGrowth = ParseDecimal(alphaVantageOverview?.QuarterlyRevenueGrowthYOY) > 0 ? ParseDecimal(alphaVantageOverview?.QuarterlyRevenueGrowthYOY) : (yfinance?.RevenueGrowth ?? 0m),

                // Profitability with fallbacks
                ROE = (ratios?.ReturnOnEquity ?? 0m) > 0 ? ratios.ReturnOnEquity : (ParseDecimal(alphaVantageOverview?.ReturnOnEquityTTM) > 0 ? ParseDecimal(alphaVantageOverview?.ReturnOnEquityTTM) : (yfinance?.ReturnOnEquity ?? 0m)),
                ROA = (ratios?.ReturnOnAssets ?? 0m) > 0 ? ratios.ReturnOnAssets : (ParseDecimal(alphaVantageOverview?.ReturnOnAssetsTTM) > 0 ? ParseDecimal(alphaVantageOverview?.ReturnOnAssetsTTM) : 0m),
                ProfitMargin = (ratios?.NetProfitMargin ?? 0m) > 0 ? ratios.NetProfitMargin : (ParseDecimal(alphaVantageOverview?.ProfitMargin) > 0 ? ParseDecimal(alphaVantageOverview?.ProfitMargin) : 0m),
                OperatingMargin = (ratios?.OperatingMargin ?? 0m) > 0 ? ratios.OperatingMargin : (ParseDecimal(alphaVantageOverview?.OperatingMarginTTM) > 0 ? ParseDecimal(alphaVantageOverview?.OperatingMarginTTM) : 0m),

                // Financial health with fallbacks
                DebtToEquity = (metrics?.DebtToEquity ?? 0m) > 0 ? metrics.DebtToEquity.Value : (ratios?.DebtEquityRatio ?? 0m),
                CurrentRatio = (metrics?.CurrentRatio ?? 0m) > 0 ? metrics.CurrentRatio.Value : (ratios?.CurrentRatio ?? 0m),
                InterestCoverage = (metrics?.InterestCoverage ?? 0m) > 0 ? metrics.InterestCoverage.Value : (ratios?.InterestCoverage ?? 0m),

                // Market data with fallbacks
                FiftyTwoWeekHigh = quote?.YearHigh ?? (ParseDecimal(alphaVantageOverview?.FiftyTwoWeekHigh) > 0 ? ParseDecimal(alphaVantageOverview?.FiftyTwoWeekHigh) : (yfinance?.FiftyTwoWeekHigh ?? 0m)),
                FiftyTwoWeekLow = quote?.YearLow ?? (ParseDecimal(alphaVantageOverview?.FiftyTwoWeekLow) > 0 ? ParseDecimal(alphaVantageOverview?.FiftyTwoWeekLow) : (yfinance?.FiftyTwoWeekLow ?? 0m)),
                Beta = 0, // FMP doesn't provide beta data

                // Analyst estimates
                AnalystTargetPrice = 0, // FMP doesn't provide analyst target price
                DividendYield = metrics?.DividendYield ?? ratios?.DividendYield ?? 0m,

                AnalysisDate = DateTime.UtcNow
            };

            // Calculate additional metrics
            analysis.UpsidePotential = analysis.AnalystTargetPrice > 0 ?
                ((analysis.AnalystTargetPrice - analysis.CurrentPrice) / analysis.CurrentPrice) * 100 : 0;

            analysis.DistanceFrom52WeekHigh = analysis.FiftyTwoWeekHigh > 0 ?
                ((analysis.CurrentPrice - analysis.FiftyTwoWeekHigh) / analysis.FiftyTwoWeekHigh) * 100 : 0;

            // Calculate intrinsic value and fair value
            analysis.IntrinsicValue = metrics?.GrahamNumber ?? 0.0m; // Graham Number as intrinsic value

            // Calculate fair value using multiple methods
            decimal fairValue = 0.0m;
            var currentPrice = analysis.CurrentPrice;

            // Method 1: Use Graham Number only if it's reasonable (within 3x of current price)
            if (metrics?.GrahamNumber.HasValue == true && 
                metrics.GrahamNumber.Value > 0 &&
                metrics.GrahamNumber.Value > currentPrice * 0.3m &&
                metrics.GrahamNumber.Value < currentPrice * 3.0m)
            {
                fairValue = metrics.GrahamNumber.Value;
                _logger.LogInformation($"Using Graham Number as fair value for {symbol}: ${fairValue:F2}");
            }
            // Method 2: Growth-adjusted valuation for high-growth stocks
            else if (analysis.RevenueGrowth > 0.15m && analysis.PriceToSales > 0 && quote?.Eps != null)
            {
                // For growth stocks, use revenue multiple approach
                var targetPS = 8m + (analysis.RevenueGrowth * 30m); // Growth-adjusted P/S
                var revenuePerShare = currentPrice / analysis.PriceToSales;
                fairValue = revenuePerShare * targetPS;
                _logger.LogInformation($"Calculated fair value for growth stock {symbol} using P/S method: ${fairValue:F2}");
            }
            // Method 3: Calculate using industry average P/E ratios if we have EPS
            else if (analysis.PERatio > 0 && quote?.Eps > 0)
            {
                // Use a conservative industry average P/E ratio (around 15-20 for tech)
                var industryAvgPE = symbol.Contains("AAPL") || symbol.Contains("MSFT") || symbol.Contains("NVDA") ? 25.0m : 15.0m;
                fairValue = industryAvgPE * quote.Eps;
                _logger.LogInformation($"Calculated fair value for {symbol} using industry average P/E ({industryAvgPE}): ${fairValue:F2}");
            }
            // Method 3: Use current P/E ratio if available
            else if (ratios?.PriceEarningsRatio > 0 && quote?.Eps > 0)
            {
                fairValue = ratios.PriceEarningsRatio * quote.Eps;
                _logger.LogInformation($"Calculated fair value for {symbol} using current P/E ratio: ${fairValue:F2}");
            }

            analysis.FairValue = fairValue;

            // Generate recommendation based on current price vs fair value
            if (analysis.FairValue > 0)
            {
                var premium = (analysis.CurrentPrice - analysis.FairValue) / analysis.FairValue;
                if (premium < -0.2m) // More than 20% undervalued
                {
                    analysis.Recommendation = "STRONG BUY";
                    analysis.Reasoning = $"Stock is {Math.Abs(premium):P1} undervalued based on fair value of ${analysis.FairValue:F2}";
                }
                else if (premium < -0.1m) // 10-20% undervalued
                {
                    analysis.Recommendation = "BUY";
                    analysis.Reasoning = $"Stock is {Math.Abs(premium):P1} undervalued based on fair value of ${analysis.FairValue:F2}";
                }
                else if (premium < 0.1m) // Within 10% of fair value
                {
                    analysis.Recommendation = "HOLD";
                    analysis.Reasoning = $"Stock is fairly valued at ${analysis.FairValue:F2}";
                }
                else if (premium < 0.2m) // 10-20% overvalued
                {
                    analysis.Recommendation = "SELL";
                    analysis.Reasoning = $"Stock is {premium:P1} overvalued based on fair value of ${analysis.FairValue:F2}";
                }
                else // More than 20% overvalued
                {
                    analysis.Recommendation = "STRONG SELL";
                    analysis.Reasoning = $"Stock is {premium:P1} overvalued based on fair value of ${analysis.FairValue:F2}";
                }
            }
            else
            {
                analysis.Recommendation = "HOLD";
                analysis.Reasoning = "Unable to determine fair value for recommendation";
            }

            // Generate AI analysis
            analysis.AIAnalysis = await GenerateValuationAIAnalysisAsync(analysis);

            // Set metric objects for API response
            analysis.ValuationMetrics = new
            {
                peRatio = analysis.PERatio,
                priceToBook = analysis.PriceToBook,
                priceToSales = analysis.PriceToSales,
                evToEbitda = analysis.EVToEBITDA,
                evToRevenue = analysis.EVToRevenue
            };

            analysis.GrowthMetrics = new
            {
                epsGrowth = analysis.EPSGrowth,
                revenueGrowth = analysis.RevenueGrowth
            };

            analysis.ProfitabilityMetrics = new
            {
                roe = analysis.ROE,
                roa = analysis.ROA,
                profitMargin = analysis.ProfitMargin,
                operatingMargin = analysis.OperatingMargin
            };

            analysis.FinancialHealthMetrics = new
            {
                debtToEquity = analysis.DebtToEquity,
                currentRatio = analysis.CurrentRatio,
                interestCoverage = analysis.InterestCoverage
            };

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting comprehensive valuation analysis for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get comprehensive technical analysis indicators
    /// </summary>
    public async Task<EnhancedTechnicalAnalysis> GetComprehensiveTechnicalAnalysisAsync(string symbol)
    {
        try
        {
            // Get technical indicators from Alpha Vantage
            var smaTask = _alphaVantageService.GetSMAAsync(symbol, 20);
            var emaTask = _alphaVantageService.GetEMAAsync(symbol, 20);
            var rsiTask = _alphaVantageService.GetRSIAsync(symbol, 14);
            var macdTask = _alphaVantageService.GetMACDAsync(symbol);
            var bbandsTask = _alphaVantageService.GetBollingerBandsAsync(symbol);
            var stochTask = _alphaVantageService.GetStochasticAsync(symbol);

            // Get current quote for price data
            var quoteTask = _fmpService.GetQuoteAsync(symbol);

            await Task.WhenAll(smaTask, emaTask, rsiTask, macdTask, bbandsTask, stochTask, quoteTask);

            var sma = await smaTask;
            var ema = await emaTask;
            var rsi = await rsiTask;
            var macd = await macdTask;
            var bbands = await bbandsTask;
            var stoch = await stochTask;
            var quote = await quoteTask;

            if (quote == null)
            {
                return null;
            }

            var analysis = new EnhancedTechnicalAnalysis
            {
                Symbol = symbol,
                CurrentPrice = quote.Price,
                PreviousClose = quote.PreviousClose,
                DayChange = quote.Change,
                DayChangePercent = quote.ChangesPercentage,

                // Moving averages
                SMA20 = (decimal)(sma == null || !sma.Any() ? 0 : GetLatestValue(sma, "SMA")),
                EMA20 = (decimal)(ema == null || !ema.Any() ? 0 : GetLatestValue(ema, "EMA")),

                // Momentum indicators
                RSI14 = (decimal)(rsi == null || !rsi.Any() ? 0 : GetLatestValue(rsi, "RSI")),

                // MACD
                MACD = (decimal)(macd == null || !macd.Any() ? 0 : GetLatestValue(macd, "MACD")),
                MACDSignalValue = (decimal)(macd == null || !macd.Any() ? 0 : GetLatestValue(macd, "MACD_Signal")),
                MACDHist = (decimal)(macd == null || !macd.Any() ? 0 : GetLatestValue(macd, "MACD_Hist")),

                // Bollinger Bands
                BBUpper = (decimal)(bbands == null || !bbands.Any() ? 0 : GetLatestValue(bbands, "Real Upper Band")),
                BBLower = (decimal)(bbands == null || !bbands.Any() ? 0 : GetLatestValue(bbands, "Real Lower Band")),
                BBMiddle = (decimal)(bbands == null || !bbands.Any() ? 0 : GetLatestValue(bbands, "Real Middle Band")),

                // Stochastic
                StochK = (decimal)(stoch == null || !stoch.Any() ? 0 : GetLatestValue(stoch, "SlowK")),
                StochD = (decimal)(stoch == null || !stoch.Any() ? 0 : GetLatestValue(stoch, "SlowD")),

                // Volume and volatility
                Volume = quote.Volume,
                AverageVolume = quote.AvgVolume,
                FiftyTwoWeekHigh = quote.YearHigh,
                FiftyTwoWeekLow = quote.YearLow,

                // Required signal properties (will be populated by CalculateTechnicalSignals)
                RSISignal = string.Empty,
                MACDSignal = string.Empty,
                BollingerSignal = string.Empty,
                StochasticSignal = string.Empty,
                MovingAverageSignal = string.Empty,

                AnalysisDate = DateTime.UtcNow
            };

            // Calculate technical signals
            analysis = CalculateTechnicalSignals(analysis);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting comprehensive technical analysis for {symbol}");
            return null;
        }
    }

    /// <summary>
    /// Get analyst recommendations and estimates
    /// </summary>
    public async Task<EnhancedAnalystAnalysis> GetComprehensiveAnalystAnalysisAsync(string symbol)
    {
        try
        {
            _logger.LogInformation($"Generating comprehensive quantitative analyst analysis for {symbol}");

            // Get comprehensive data from multiple sources to create our own "analyst" score
            var overviewTask = GetComprehensiveCompanyOverviewAsync(symbol);
            var valuationTask = GetComprehensiveValuationAnalysisAsync(symbol);
            var financialsTask = GetComprehensiveFinancialStatementsAsync(symbol);
            var estimatesTask = _fmpService.GetAnalystEstimatesAsync(symbol, 4);

            await Task.WhenAll(overviewTask, valuationTask, financialsTask, estimatesTask);

            var overview = await overviewTask;
            var valuation = await valuationTask;
            var financials = await financialsTask;
            var estimates = await estimatesTask;

            if (overview == null || valuation == null)
            {
                _logger.LogWarning($"Insufficient data for analyst analysis of {symbol}. Generating fallback analysis.");
                
                // Return basic analysis with limited data
                return new EnhancedAnalystAnalysis
                {
                    Symbol = symbol,
                    ConsensusRating = "Hold - Insufficient Data",
                    AverageTargetPrice = 0,
                    HighTargetPrice = 0,
                    LowTargetPrice = 0,
                    NumberOfAnalysts = 0,
                    BuyRatings = 0,
                    HoldRatings = 1,
                    SellRatings = 0,
                    AnalystTargetPrice = 0,
                    AnalystRatingStrongBuy = 0,
                    AnalystRatingBuy = 0,
                    AnalystRatingHold = 1,
                    AnalystRatingSell = 0,
                    AnalystRatingStrongSell = 0,
                    NumberOfAnalystOpinions = 1,
                    Estimates = new
                    {
                        error = "Insufficient data available. FMP API may have reached rate limit.",
                        suggestion = "Try again later or check your API keys.",
                        dataAvailable = new { overview = overview != null, valuation = valuation != null }
                    },
                    Upgrades = 0,
                    Downgrades = 0,
                    LatestEstimates = null,
                    AllEstimates = null,
                    AnalysisDate = DateTime.UtcNow
                };
            }

            // Calculate quantitative score (0-100) based on multiple factors
            var scores = CalculateQuantitativeScores(overview, valuation, financials);
            
            // Calculate target price using multiple valuation methods
            var targetPrices = CalculateTargetPrices(overview, valuation, financials, estimates);

            // Determine consensus rating based on quantitative score
            string consensusRating;
            int totalScore = scores.TotalScore;
            
            if (totalScore >= 80)
                consensusRating = "Strong Buy";
            else if (totalScore >= 65)
                consensusRating = "Buy";
            else if (totalScore >= 45)
                consensusRating = "Hold";
            else if (totalScore >= 30)
                consensusRating = "Sell";
            else
                consensusRating = "Strong Sell";

            // Convert score to rating distribution (simulate analyst consensus)
            var ratings = ConvertScoreToRatings(totalScore);

            // Generate AI-powered detailed analysis
            var aiAnalysis = await GenerateQuantitativeAnalystReportAsync(symbol, overview, valuation, financials, scores, targetPrices);

            var analysis = new EnhancedAnalystAnalysis
            {
                Symbol = symbol,
                ConsensusRating = consensusRating,
                AverageTargetPrice = targetPrices.Average,
                HighTargetPrice = targetPrices.High,
                LowTargetPrice = targetPrices.Low,
                NumberOfAnalysts = 10, // Representing our 10 quantitative factors
                BuyRatings = ratings.Buy,
                HoldRatings = ratings.Hold,
                SellRatings = ratings.Sell,
                AnalystTargetPrice = targetPrices.Average,
                AnalystRatingStrongBuy = ratings.StrongBuy,
                AnalystRatingBuy = ratings.Buy,
                AnalystRatingHold = ratings.Hold,
                AnalystRatingSell = ratings.Sell,
                AnalystRatingStrongSell = ratings.StrongSell,
                NumberOfAnalystOpinions = 10,
                Estimates = new
                {
                    quantitativeScore = totalScore,
                    valuationScore = scores.ValuationScore,
                    profitabilityScore = scores.ProfitabilityScore,
                    growthScore = scores.GrowthScore,
                    financialHealthScore = scores.FinancialHealthScore,
                    momentumScore = scores.MomentumScore,
                    aiAnalysis = aiAnalysis,
                    methodology = "Multi-factor quantitative model combining valuation, profitability, growth, financial health, and momentum indicators"
                },
                Upgrades = totalScore > 60 ? 1 : 0,
                Downgrades = totalScore < 40 ? 1 : 0,
                LatestEstimates = estimates?.FirstOrDefault(),
                AllEstimates = estimates,
                AnalysisDate = DateTime.UtcNow
            };

            _logger.LogInformation($"Generated quantitative analyst analysis for {symbol}: {consensusRating} (Score: {totalScore}/100)");
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating analyst analysis for {symbol}");
            return null;
        }
    }

    private (int TotalScore, int ValuationScore, int ProfitabilityScore, int GrowthScore, int FinancialHealthScore, int MomentumScore) 
        CalculateQuantitativeScores(EnhancedCompanyOverview overview, EnhancedValuationAnalysis valuation, EnhancedFinancialStatements financials)
    {
        int valuationScore = 0;
        int profitabilityScore = 0;
        int growthScore = 0;
        int financialHealthScore = 0;
        int momentumScore = 0;

        // 1. Valuation Score (0-20 points)
        if (valuation.PERatio > 0 && valuation.PERatio < 15) valuationScore += 7;
        else if (valuation.PERatio >= 15 && valuation.PERatio < 25) valuationScore += 4;
        else if (valuation.PERatio >= 25 && valuation.PERatio < 35) valuationScore += 2;
        
        if (valuation.PriceToBook > 0 && valuation.PriceToBook < 3) valuationScore += 4;
        else if (valuation.PriceToBook >= 3 && valuation.PriceToBook < 5) valuationScore += 2;
        
        if (valuation.PriceToSales > 0 && valuation.PriceToSales < 2) valuationScore += 4;
        else if (valuation.PriceToSales >= 2 && valuation.PriceToSales < 4) valuationScore += 2;
        
        if (valuation.CurrentPrice > 0 && valuation.FairValue > 0 && valuation.CurrentPrice < valuation.FairValue) valuationScore += 5;

        // 2. Profitability Score (0-20 points)
        if (valuation.ProfitMargin > 0.20m) profitabilityScore += 7;
        else if (valuation.ProfitMargin > 0.10m) profitabilityScore += 4;
        else if (valuation.ProfitMargin > 0.05m) profitabilityScore += 2;
        
        if (valuation.ROE > 0.20m) profitabilityScore += 7;
        else if (valuation.ROE > 0.15m) profitabilityScore += 4;
        else if (valuation.ROE > 0.10m) profitabilityScore += 2;
        
        if (valuation.OperatingMargin > 0.15m) profitabilityScore += 6;
        else if (valuation.OperatingMargin > 0.10m) profitabilityScore += 3;

        // 3. Growth Score (0-20 points)
        if (valuation.RevenueGrowth > 0.20m) growthScore += 10;
        else if (valuation.RevenueGrowth > 0.10m) growthScore += 6;
        else if (valuation.RevenueGrowth > 0.05m) growthScore += 3;
        
        if (valuation.EPSGrowth > 0.15m) growthScore += 10;
        else if (valuation.EPSGrowth > 0.10m) growthScore += 6;
        else if (valuation.EPSGrowth > 0.05m) growthScore += 3;

        // 4. Financial Health Score (0-20 points)
        if (valuation.DebtToEquity >= 0 && valuation.DebtToEquity < 0.5m) financialHealthScore += 7;
        else if (valuation.DebtToEquity < 1.0m) financialHealthScore += 4;
        else if (valuation.DebtToEquity < 2.0m) financialHealthScore += 2;
        
        if (valuation.CurrentRatio > 2.0m) financialHealthScore += 7;
        else if (valuation.CurrentRatio > 1.5m) financialHealthScore += 4;
        else if (valuation.CurrentRatio > 1.0m) financialHealthScore += 2;
        
        if (valuation.InterestCoverage > 5.0m) financialHealthScore += 6;
        else if (valuation.InterestCoverage > 3.0m) financialHealthScore += 3;

        // 5. Momentum Score (0-20 points)
        if (valuation.CurrentPrice > 0 && valuation.FiftyTwoWeekLow > 0)
        {
            var pricePosition = (valuation.CurrentPrice - valuation.FiftyTwoWeekLow) / (valuation.FiftyTwoWeekHigh - valuation.FiftyTwoWeekLow);
            if (pricePosition > 0.7m && pricePosition < 0.85m) momentumScore += 10; // Sweet spot
            else if (pricePosition > 0.5m && pricePosition < 0.7m) momentumScore += 7;
            else if (pricePosition > 0.3m && pricePosition < 0.5m) momentumScore += 5;
            else if (pricePosition < 0.3m) momentumScore += 8; // Near lows, potential bounce
        }
        
        if (overview.QuarterlyRevenueGrowthYOY > 0) momentumScore += 5;
        if (overview.QuarterlyEarningsGrowthYOY > 0) momentumScore += 5;

        int totalScore = valuationScore + profitabilityScore + growthScore + financialHealthScore + momentumScore;
        
        return (totalScore, valuationScore, profitabilityScore, growthScore, financialHealthScore, momentumScore);
    }

    private (decimal Low, decimal Average, decimal High) CalculateTargetPrices(
        EnhancedCompanyOverview overview, EnhancedValuationAnalysis valuation, 
        EnhancedFinancialStatements financials, List<FMPAnalystEstimates> estimates)
    {
        var targetPrices = new List<decimal>();
        decimal currentPrice = valuation.CurrentPrice;
        
        // Method 1: PE-based target (using sector-appropriate PE)
        if (valuation.EPS > 0)
        {
            // Use higher PE multiples for growth stocks
            var avgPE = valuation.RevenueGrowth > 0.20m ? 30m : 
                       valuation.RevenueGrowth > 0.15m ? 25m : 20m;
            var peTarget = avgPE * valuation.EPS;
            if (peTarget > currentPrice * 0.5m && peTarget < currentPrice * 2.0m) // Sanity check
                targetPrices.Add(peTarget);
        }
        
        // Method 2: PEG-based target (for growth stocks)
        if (valuation.EPS > 0 && valuation.EPSGrowth > 0.05m)
        {
            var fairPEG = 1.5m; // More realistic for growth stocks
            var targetPE = Math.Min(50m, fairPEG * (valuation.EPSGrowth * 100)); // Cap at 50 PE
            var pegTarget = targetPE * valuation.EPS;
            if (pegTarget > currentPrice * 0.5m && pegTarget < currentPrice * 2.5m)
                targetPrices.Add(pegTarget);
        }
        
        // Method 3: Revenue multiple (for high-growth stocks)
        if (valuation.RevenueGrowth > 0.15m && valuation.PriceToSales > 0)
        {
            // Growth-adjusted P/S multiple
            var targetPS = Math.Min(20m, 5m + (valuation.RevenueGrowth * 50m));
            var revenuePerShare = currentPrice / valuation.PriceToSales;
            var revenueTarget = revenuePerShare * targetPS;
            if (revenueTarget > currentPrice * 0.5m && revenueTarget < currentPrice * 2.0m)
                targetPrices.Add(revenueTarget);
        }
        
        // Method 4: DCF-based (simplified)
        if (financials?.CashFlowStatements?.Any() == true)
        {
            var latestFCF = financials.CashFlowStatements.OrderByDescending(c => c.Date).FirstOrDefault()?.FreeCashFlow ?? 0;
            if (latestFCF > 0 && valuation.SharesOutstanding > 0)
            {
                var fcfPerShare = latestFCF / (decimal)valuation.SharesOutstanding;
                var growthRate = Math.Max(0.05m, Math.Min(0.30m, valuation.RevenueGrowth)); // Cap between 5-30%
                var targetMultiple = 20m + (growthRate * 80m); // Growth-adjusted multiple
                var dcfTarget = fcfPerShare * targetMultiple;
                if (dcfTarget > currentPrice * 0.5m && dcfTarget < currentPrice * 2.5m)
                    targetPrices.Add(dcfTarget);
            }
        }
        
        // Method 5: Fair value from valuation analysis (only if reasonable)
        if (valuation.FairValue > 0 && 
            valuation.FairValue > currentPrice * 0.3m && 
            valuation.FairValue < currentPrice * 3.0m)
        {
            targetPrices.Add(valuation.FairValue);
        }

        // If we have valid prices, calculate statistics
        if (targetPrices.Any())
        {
            var sortedPrices = targetPrices.OrderBy(p => p).ToList();
            var avg = targetPrices.Average();
            
            // Ensure low < average < high
            var low = sortedPrices.First();
            var high = sortedPrices.Last();
            
            // Apply reasonable bounds
            low = Math.Max(low, currentPrice * 0.70m);
            high = Math.Min(high, currentPrice * 1.80m);
            avg = (low + high) / 2; // Recalculate average to be between low and high
            
            // Final sanity check
            if (low > high)
            {
                var temp = low;
                low = high;
                high = temp;
            }
            
            return (low, avg, high);
        }
        
        // Fallback: Use current price with conservative range
        return (
            currentPrice * 0.90m, 
            currentPrice * 1.05m, 
            currentPrice * 1.25m
        );
    }

    private (int StrongBuy, int Buy, int Hold, int Sell, int StrongSell) ConvertScoreToRatings(int totalScore)
    {
        // Convert 0-100 score to rating distribution
        if (totalScore >= 80)
            return (7, 2, 1, 0, 0);
        else if (totalScore >= 65)
            return (2, 6, 2, 0, 0);
        else if (totalScore >= 55)
            return (1, 4, 4, 1, 0);
        else if (totalScore >= 45)
            return (0, 2, 6, 2, 0);
        else if (totalScore >= 35)
            return (0, 1, 3, 5, 1);
        else if (totalScore >= 25)
            return (0, 0, 2, 5, 3);
        else
            return (0, 0, 1, 3, 6);
    }

    private async Task<string> GenerateQuantitativeAnalystReportAsync(
        string symbol, EnhancedCompanyOverview overview, EnhancedValuationAnalysis valuation, 
        EnhancedFinancialStatements financials, 
        (int TotalScore, int ValuationScore, int ProfitabilityScore, int GrowthScore, int FinancialHealthScore, int MomentumScore) scores,
        (decimal Low, decimal Average, decimal High) targetPrices)
    {
        try
        {
            var prompt = $@"Generate a professional quantitative analyst report for {symbol} ({overview.CompanyName}).

QUANTITATIVE SCORES (0-100 Scale):
- Overall Score: {scores.TotalScore}/100
- Valuation: {scores.ValuationScore}/20
- Profitability: {scores.ProfitabilityScore}/20
- Growth: {scores.GrowthScore}/20
- Financial Health: {scores.FinancialHealthScore}/20
- Momentum: {scores.MomentumScore}/20

KEY METRICS:
- Current Price: ${valuation.CurrentPrice:F2}
- Target Price Range: ${targetPrices.Low:F2} - ${targetPrices.High:F2}
- PE Ratio: {valuation.PERatio:F2}
- Profit Margin: {valuation.ProfitMargin:P2}
- ROE: {valuation.ROE:P2}
- Revenue Growth: {valuation.RevenueGrowth:P2}
- Debt/Equity: {valuation.DebtToEquity:F2}

Provide a 3-paragraph analysis:
1. Overall investment thesis based on the quantitative scores
2. Key strengths and weaknesses
3. Risk factors and catalysts to watch

Be concise, data-driven, and actionable.";

            var response = await _llmService.GetChatCompletionAsync(prompt);
            return response ?? "Quantitative analysis complete. Refer to scores above.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating AI analyst report for {symbol}");
            return $"Quantitative Score: {scores.TotalScore}/100. Analysis based on multi-factor scoring model.";
        }
    }

    /// <summary>
    /// Generate comprehensive fundamental analysis report
    /// </summary>
    public async Task<EnhancedFundamentalReport> GenerateComprehensiveReportAsync(string symbol)
    {
        try
        {
            // Get all analysis components in parallel
            var overviewTask = GetComprehensiveCompanyOverviewAsync(symbol);
            var statementsTask = GetComprehensiveFinancialStatementsAsync(symbol);
            var valuationTask = GetComprehensiveValuationAnalysisAsync(symbol);
            var technicalTask = GetComprehensiveTechnicalAnalysisAsync(symbol);
            var analystTask = GetComprehensiveAnalystAnalysisAsync(symbol);

            await Task.WhenAll(overviewTask, statementsTask, valuationTask, technicalTask, analystTask);

            var overview = await overviewTask;
            var statements = await statementsTask;
            var valuation = await valuationTask;
            var technical = await technicalTask;
            var analyst = await analystTask;

            var report = new EnhancedFundamentalReport
            {
                Symbol = symbol,
                CompanyOverview = overview,
                FinancialStatements = statements,
                ValuationAnalysis = valuation,
                TechnicalAnalysis = technical,
                AnalystAnalysis = analyst,
                ReportGenerated = DateTime.UtcNow,

                // Generate summary recommendations
                InvestmentRecommendation = GenerateInvestmentRecommendation(valuation, technical, analyst),
                RiskAssessment = GenerateRiskAssessment(valuation, technical),
                KeyStrengths = GenerateKeyStrengths(overview, valuation),
                KeyConcerns = GenerateKeyConcerns(valuation, technical)
            };

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating comprehensive report for {symbol}");
            return null;
        }
    }

    private List<EnhancedIncomeStatement> CombineIncomeStatements(
        AlphaVantageIncomeStatement alpha, List<FMPIncomeStatement> fmp)
    {
        // Implementation would combine and validate data from both sources
        // For now, return FMP data as it's more comprehensive
        return fmp?.Select(f => new EnhancedIncomeStatement
        {
            Date = f.Date,
            Period = f.Period,
            Revenue = f.Revenue ?? 0L,
            CostOfRevenue = f.CostOfRevenue ?? 0L,
            GrossProfit = f.GrossProfit ?? 0L,
            OperatingExpenses = f.OperatingExpenses ?? 0L,
            OperatingIncome = f.OperatingIncome ?? 0L,
            NetIncome = f.NetIncome ?? 0L,
            EPS = f.Eps ?? 0m,
            EPSDiluted = f.Epsdiluted ?? 0m
        }).ToList() ?? new List<EnhancedIncomeStatement>();
    }

    private List<EnhancedBalanceSheet> CombineBalanceSheets(
        AlphaVantageBalanceSheet alpha, List<FMPBalanceSheet> fmp)
    {
        // Implementation would combine and validate data from both sources
        return fmp?.Select(f => new EnhancedBalanceSheet
        {
            Date = f.Date,
            Period = f.Period,
            TotalAssets = f.TotalAssets ?? 0L,
            TotalLiabilities = f.TotalLiabilities ?? 0L,
            TotalEquity = f.TotalEquity ?? 0L,
            CashAndEquivalents = f.CashAndCashEquivalents ?? 0L,
            TotalCurrentAssets = f.TotalCurrentAssets ?? 0L,
            TotalCurrentLiabilities = f.TotalCurrentLiabilities ?? 0L,
            LongTermDebt = f.LongTermDebt ?? 0L,
            TotalDebt = f.TotalDebt ?? 0L
        }).ToList() ?? new List<EnhancedBalanceSheet>();
    }

    private List<EnhancedCashFlow> CombineCashFlowStatements(
        AlphaVantageCashFlow alpha, List<FMPCashFlow> fmp)
    {
        // Implementation would combine and validate data from both sources
        return fmp?.Select(f => new EnhancedCashFlow
        {
            Date = f.Date,
            Period = f.Period,
            OperatingCashFlow = f.NetCashProvidedByOperatingActivities ?? 0L,
            InvestingCashFlow = f.NetCashProvidedByInvestingActivities ?? 0L,
            FinancingCashFlow = f.NetCashProvidedByFinancingActivities ?? 0L,
            FreeCashFlow = f.FreeCashFlow ?? 0L,
            CapitalExpenditure = f.CapitalExpenditure ?? 0L,
            NetChangeInCash = f.NetChangeInCash ?? 0L
        }).ToList() ?? new List<EnhancedCashFlow>();
    }

    private decimal GetLatestValue(Dictionary<string, Dictionary<string, string>> data, string key = null)
    {
        if (data == null || data.Count == 0)
            return 0;

        var latestEntry = data.First();
        var indicatorKey = key ?? latestEntry.Value.Keys.First();

        if (latestEntry.Value.TryGetValue(indicatorKey, out var valueStr) &&
            decimal.TryParse(valueStr, out var value))
        {
            return value;
        }

        return 0;
    }

    private double GetLatestValue(List<AlphaVantageTechnicalData> data, string key)
    {
        if (data == null || !data.Any()) return 0;

        var latest = data.First();
        if (latest.Indicators.TryGetValue(key, out var value))
        {
            return (double)(decimal)value;
        }
        return 0;
    }

    private EnhancedTechnicalAnalysis CalculateTechnicalSignals(EnhancedTechnicalAnalysis analysis)
    {
        // RSI signals
        if (analysis.RSI14 > 70)
            analysis.RSISignal = "Overbought";
        else if (analysis.RSI14 < 30)
            analysis.RSISignal = "Oversold";
        else
            analysis.RSISignal = "Neutral";

        // MACD signals
        if (analysis.MACD > analysis.MACDSignalValue && analysis.MACDHist > 0)
            analysis.MACDSignal = "Bullish";
        else if (analysis.MACD < analysis.MACDSignalValue && analysis.MACDHist < 0)
            analysis.MACDSignal = "Bearish";
        else
            analysis.MACDSignal = "Neutral";

        // Bollinger Band signals
        if (analysis.CurrentPrice > analysis.BBUpper)
            analysis.BollingerSignal = "Above Upper Band";
        else if (analysis.CurrentPrice < analysis.BBLower)
            analysis.BollingerSignal = "Below Lower Band";
        else
            analysis.BollingerSignal = "Within Bands";

        // Stochastic signals
        if (analysis.StochK > 80 && analysis.StochD > 80)
            analysis.StochasticSignal = "Overbought";
        else if (analysis.StochK < 20 && analysis.StochD < 20)
            analysis.StochasticSignal = "Oversold";
        else
            analysis.StochasticSignal = "Neutral";

        // Moving average signals
        if (analysis.CurrentPrice > analysis.SMA20 && analysis.CurrentPrice > analysis.EMA20)
            analysis.MovingAverageSignal = "Above MAs";
        else if (analysis.CurrentPrice < analysis.SMA20 && analysis.CurrentPrice < analysis.EMA20)
            analysis.MovingAverageSignal = "Below MAs";
        else
            analysis.MovingAverageSignal = "Mixed";

        return analysis;
    }

    private string GenerateInvestmentRecommendation(
        EnhancedValuationAnalysis valuation,
        EnhancedTechnicalAnalysis technical,
        EnhancedAnalystAnalysis analyst)
    {
        var score = 0;

        // Valuation scoring
        if (valuation?.PERatio > 0 && valuation.PERatio < 20) score += 2;
        if (valuation?.PriceToBook > 0 && valuation.PriceToBook < 3) score += 1;
        if (valuation?.ROE > 0.15m) score += 2;
        if (valuation?.DebtToEquity < 1) score += 1;
        if (valuation?.UpsidePotential > 20) score += 1;

        // Technical scoring
        if (technical?.RSISignal == "Oversold") score += 1;
        if (technical?.MACDSignal == "Bullish") score += 1;
        if (technical?.MovingAverageSignal == "Above MAs") score += 1;

        // Analyst scoring
        if (analyst?.AnalystTargetPrice > 0 &&
            analyst.AnalystTargetPrice > (valuation?.CurrentPrice ?? 0) * 1.1m) score += 1;

        if (score >= 6) return "Strong Buy";
        if (score >= 4) return "Buy";
        if (score >= 2) return "Hold";
        return "Sell";
    }

    private string GenerateRiskAssessment(
        EnhancedValuationAnalysis valuation,
        EnhancedTechnicalAnalysis technical)
    {
        var risks = new List<string>();

        if (valuation?.DebtToEquity > 2) risks.Add("High leverage");
        if (valuation?.Beta > 1.5m) risks.Add("High volatility");
        if (valuation?.CurrentRatio < 1) risks.Add("Liquidity concerns");
        if (technical?.DistanceFrom52WeekHigh < -20) risks.Add("Near 52-week low");

        return risks.Count > 0 ? string.Join(", ", risks) : "Moderate risk";
    }

    private List<string> GenerateKeyStrengths(
        EnhancedCompanyOverview overview,
        EnhancedValuationAnalysis valuation)
    {
        var strengths = new List<string>();

        if (valuation?.ROE > 0.15m) strengths.Add("Strong profitability");
        if (valuation?.ProfitMargin > 0.1m) strengths.Add("Healthy margins");
        if (overview?.MarketCap > 10000000000) strengths.Add("Large market cap");
        if (valuation?.DividendYield > 0.02m) strengths.Add("Dividend payer");

        return strengths;
    }

    private List<string> GenerateKeyConcerns(
        EnhancedValuationAnalysis valuation,
        EnhancedTechnicalAnalysis technical)
    {
        var concerns = new List<string>();

        if (valuation?.PERatio > 30) concerns.Add("High valuation");
        if (valuation?.DebtToEquity > 2) concerns.Add("High debt levels");
        if (technical?.RSISignal == "Overbought") concerns.Add("Overbought technically");

        return concerns;
    }


    private async Task<string> GenerateValuationAIAnalysisAsync(EnhancedValuationAnalysis analysis)
    {
        var prompt = $@"You are a professional financial analyst specializing in equity valuation. Provide a comprehensive valuation analysis for {analysis.Symbol} in 4-5 well-structured paragraphs.

STOCK: {analysis.Symbol}
CURRENT PRICE: ${analysis.CurrentPrice:F2}
FAIR VALUE: ${analysis.FairValue:F2}
INTRINSIC VALUE: ${analysis.IntrinsicValue:F2}
RECOMMENDATION: {analysis.Recommendation}

VALUATION MULTIPLES:
P/E: {analysis.PERatio:F2}, Forward P/E: {analysis.ForwardPE:F2}, PEG: {analysis.PEGRatio:F2}
P/B: {analysis.PriceToBook:F2}, P/TangibleBook: {analysis.PriceToTangibleBook:F2}
P/S: {analysis.PriceToSales:F2}, P/FCF: {analysis.PriceToFreeCashFlow:F2}
EV/EBITDA: {analysis.EVToEBITDA:F2}, EV/Revenue: {analysis.EVToRevenue:F2}, EV/FCF: {analysis.EVToFCF:F2}

GROWTH: EPS Growth {analysis.EPSGrowth:P2}, Revenue Growth {analysis.RevenueGrowth:P2}
PROFITABILITY: ROE {analysis.ROE:P2}, ROA {analysis.ROA:P2}, Net Margin {analysis.ProfitMargin:P2}, Operating Margin {analysis.OperatingMargin:P2}, Gross Margin {analysis.GrossProfitMargin:P2}
FINANCIAL HEALTH: D/E {analysis.DebtToEquity:F2}, Current Ratio {analysis.CurrentRatio:F2}, Quick Ratio {analysis.QuickRatio:F2}, Interest Coverage {analysis.InterestCoverage:F2}

Format response in clean markdown with:
**1. VALUATION ASSESSMENT** - Analyze multiples, determine if over/under/fairly valued
**2. GROWTH AND EARNINGS QUALITY** - Evaluate growth sustainability and FCF generation
**3. PROFITABILITY AND EFFICIENCY** - Assess margins and operational efficiency
**4. FINANCIAL HEALTH AND RISK** - Analyze balance sheet strength and identify red flags
**5. INVESTMENT RECOMMENDATION** - Provide clear recommendation with entry price, targets, and risks

Use **bold** for headers, bullet points for lists, justify the {analysis.Recommendation} recommendation.";

        try
        {
            _logger.LogInformation("Generating valuation AI analysis for {Symbol}", analysis.Symbol);
            var aiAnalysis = await _llmService.GetChatCompletionAsync(prompt, "openai");
            
            if (string.IsNullOrWhiteSpace(aiAnalysis))
            {
                _logger.LogWarning("LLM returned empty valuation analysis for {Symbol}, retrying...", analysis.Symbol);
                aiAnalysis = await _llmService.GetChatCompletionAsync(prompt, "openai");
            }
            
            return aiAnalysis?.Trim() ?? $"Unable to generate detailed valuation analysis for {analysis.Symbol}. {analysis.Reasoning}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating valuation AI analysis for {Symbol}", analysis.Symbol);
            return $"Valuation analysis temporarily unavailable. {analysis.Reasoning}";
        }
    }

    public async Task<string> CompareCompaniesFundamentalAsync(string[] symbols)
    {
        try
        {
            var comparisons = new List<object>();

            foreach (var symbol in symbols)
            {
                var overview = await GetComprehensiveCompanyOverviewAsync(symbol);
                var valuation = await GetComprehensiveValuationAnalysisAsync(symbol);

                if (overview != null && valuation != null)
                {
                    comparisons.Add(new
                    {
                        symbol = symbol,
                        companyName = overview.CompanyName,
                        marketCap = overview.MarketCap,
                        peRatio = overview.PERatio,
                        currentPrice = valuation.CurrentPrice,
                        intrinsicValue = valuation.IntrinsicValue,
                        fairValue = valuation.FairValue,
                        recommendation = valuation.Recommendation
                    });
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(comparisons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing companies fundamental analysis");
            return "Error comparing companies";
        }
    }
}

// Data models for enhanced analysis
public class EnhancedCompanyOverview
{
    public required string Symbol { get; set; }
    public required string CompanyName { get; set; }
    public required string Description { get; set; }
    public required string Industry { get; set; }
    public required string Sector { get; set; }
    public required string Exchange { get; set; }
    public required string Country { get; set; }
    public required string Website { get; set; }
    public required string CEO { get; set; }
    public long Employees { get; set; }
    public long MarketCap { get; set; }
    public decimal PERatio { get; set; }
    public decimal PEGRatio { get; set; }
    public decimal BookValue { get; set; }
    public decimal DividendPerShare { get; set; }
    public decimal DividendYield { get; set; }
    public decimal EPS { get; set; }
    public decimal RevenuePerShareTTM { get; set; }
    public decimal ProfitMargin { get; set; }
    public decimal OperatingMarginTTM { get; set; }
    public decimal ReturnOnAssetsTTM { get; set; }
    public decimal ReturnOnEquityTTM { get; set; }
    public decimal QuarterlyEarningsGrowthYOY { get; set; }
    public decimal QuarterlyRevenueGrowthYOY { get; set; }
    public decimal AnalystTargetPrice { get; set; }
    public decimal TrailingPE { get; set; }
    public decimal ForwardPE { get; set; }
    public decimal PriceToSalesRatioTTM { get; set; }
    public decimal PriceToBookRatio { get; set; }
    public decimal EVToRevenue { get; set; }
    public decimal EVToEBITDA { get; set; }
    public decimal Beta { get; set; }
    public decimal FiftyTwoWeekHigh { get; set; }
    public decimal FiftyTwoWeekLow { get; set; }
    public decimal FiftyDayMovingAverage { get; set; }
    public decimal TwoHundredDayMovingAverage { get; set; }
    public long SharesOutstanding { get; set; }
    public required string DividendDate { get; set; }
    public required string ExDividendDate { get; set; }
    public required string LastSplitFactor { get; set; }
    public required string LastSplitDate { get; set; }
    public required string IpoDate { get; set; }
    public bool IsActivelyTrading { get; set; }
}

public class EnhancedFinancialStatements
{
    public required string Symbol { get; set; }
    public required List<EnhancedIncomeStatement> IncomeStatements { get; set; }
    public required List<EnhancedBalanceSheet> BalanceSheets { get; set; }
    public required List<EnhancedCashFlow> CashFlowStatements { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class EnhancedIncomeStatement
{
    public required string Date { get; set; }
    public required string Period { get; set; }
    public long Revenue { get; set; }
    public long CostOfRevenue { get; set; }
    public long GrossProfit { get; set; }
    public long OperatingExpenses { get; set; }
    public long OperatingIncome { get; set; }
    public long NetIncome { get; set; }
    public decimal EPS { get; set; }
    public decimal EPSDiluted { get; set; }
}

public class EnhancedBalanceSheet
{
    public required string Date { get; set; }
    public required string Period { get; set; }
    public long TotalAssets { get; set; }
    public long TotalLiabilities { get; set; }
    public long TotalEquity { get; set; }
    public long CashAndEquivalents { get; set; }
    public long TotalCurrentAssets { get; set; }
    public long TotalCurrentLiabilities { get; set; }
    public long LongTermDebt { get; set; }
    public long TotalDebt { get; set; }
}

public class EnhancedCashFlow
{
    public required string Date { get; set; }
    public required string Period { get; set; }
    public long OperatingCashFlow { get; set; }
    public long InvestingCashFlow { get; set; }
    public long FinancingCashFlow { get; set; }
    public long FreeCashFlow { get; set; }
    public long CapitalExpenditure { get; set; }
    public long NetChangeInCash { get; set; }
}

public class EnhancedValuationAnalysis
{
    public required string Symbol { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketCap { get; set; }
    public long SharesOutstanding { get; set; }
    public decimal PERatio { get; set; }
    public decimal PriceToBook { get; set; }
    public decimal PriceToSales { get; set; }
    public decimal EVToEBITDA { get; set; }
    public decimal EVToRevenue { get; set; }
    public decimal EPS { get; set; }
    public decimal EPSGrowth { get; set; }
    public decimal RevenueGrowth { get; set; }
    public decimal ROE { get; set; }
    public decimal ROA { get; set; }
    public decimal ProfitMargin { get; set; }
    public decimal OperatingMargin { get; set; }
    public decimal DebtToEquity { get; set; }
    public decimal CurrentRatio { get; set; }
    public decimal InterestCoverage { get; set; }
    public decimal FiftyTwoWeekHigh { get; set; }
    public decimal FiftyTwoWeekLow { get; set; }
    public decimal Beta { get; set; }
    public decimal AnalystTargetPrice { get; set; }
    public decimal DividendYield { get; set; }
    public decimal UpsidePotential { get; set; }
    public decimal DistanceFrom52WeekHigh { get; set; }
    public DateTime AnalysisDate { get; set; }
    public decimal IntrinsicValue { get; set; }
    public decimal FairValue { get; set; }
    public object ValuationMetrics { get; set; }
    public object GrowthMetrics { get; set; }
    public object ProfitabilityMetrics { get; set; }
    public object FinancialHealthMetrics { get; set; }
    public string Recommendation { get; set; }
    public string Reasoning { get; set; }
    public string AIAnalysis { get; set; }
    
    // Additional valuation metrics
    public decimal PEGRatio { get; set; }
    public decimal ForwardPE { get; set; }
    public decimal PriceToFreeCashFlow { get; set; }
    public decimal EnterpriseValue { get; set; }
    public decimal EVToFCF { get; set; }
    public decimal PriceToTangibleBook { get; set; }
    public decimal DividendPayoutRatio { get; set; }
    public decimal FreeCashFlowPerShare { get; set; }
    public decimal BookValuePerShare { get; set; }
    public decimal TangibleBookValuePerShare { get; set; }
    public decimal QuickRatio { get; set; }
    public decimal GrossProfitMargin { get; set; }
    public decimal EBITDA { get; set; }
    public decimal EBITDAMargin { get; set; }
    public decimal AssetTurnover { get; set; }
    public decimal InventoryTurnover { get; set; }
    public decimal WorkingCapital { get; set; }
}

public class EnhancedTechnicalAnalysis
{
    public required string Symbol { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal DayChange { get; set; }
    public decimal DayChangePercent { get; set; }
    public decimal SMA20 { get; set; }
    public decimal EMA20 { get; set; }
    public decimal RSI14 { get; set; }
    public required string RSISignal { get; set; }
    public decimal MACD { get; set; }
    public decimal MACDSignalValue { get; set; }
    public decimal MACDHist { get; set; }
    public required string MACDSignal { get; set; }
    public decimal BBUpper { get; set; }
    public decimal BBLower { get; set; }
    public decimal BBMiddle { get; set; }
    public required string BollingerSignal { get; set; }
    public decimal StochK { get; set; }
    public decimal StochD { get; set; }
    public required string StochasticSignal { get; set; }
    public required string MovingAverageSignal { get; set; }
    public long Volume { get; set; }
    public long AverageVolume { get; set; }
    public decimal FiftyTwoWeekHigh { get; set; }
    public decimal FiftyTwoWeekLow { get; set; }
    public decimal DistanceFrom52WeekHigh => FiftyTwoWeekHigh > 0 ? ((CurrentPrice - FiftyTwoWeekHigh) / FiftyTwoWeekHigh * 100) : 0;
    public DateTime AnalysisDate { get; set; }
}

public class EnhancedAnalystAnalysis
{
    public required string Symbol { get; set; }
    public decimal AnalystTargetPrice { get; set; }
    public int AnalystRatingStrongBuy { get; set; }
    public int AnalystRatingBuy { get; set; }
    public int AnalystRatingHold { get; set; }
    public int AnalystRatingSell { get; set; }
    public int AnalystRatingStrongSell { get; set; }
    public int NumberOfAnalystOpinions { get; set; }
    public FMPAnalystEstimates? LatestEstimates { get; set; }
    public List<FMPAnalystEstimates>? AllEstimates { get; set; }
    public DateTime AnalysisDate { get; set; }
    public string ConsensusRating { get; set; }
    public decimal AverageTargetPrice { get; set; }
    public decimal HighTargetPrice { get; set; }
    public decimal LowTargetPrice { get; set; }
    public int NumberOfAnalysts { get; set; }
    public int BuyRatings { get; set; }
    public int HoldRatings { get; set; }
    public int SellRatings { get; set; }
    public object Estimates { get; set; }
    public int Upgrades { get; set; }
    public int Downgrades { get; set; }
}

public class EnhancedFundamentalReport
{
    public required string Symbol { get; set; }
    public required EnhancedCompanyOverview CompanyOverview { get; set; }
    public required EnhancedFinancialStatements FinancialStatements { get; set; }
    public required EnhancedValuationAnalysis ValuationAnalysis { get; set; }
    public required EnhancedTechnicalAnalysis TechnicalAnalysis { get; set; }
    public required EnhancedAnalystAnalysis AnalystAnalysis { get; set; }
    public required string InvestmentRecommendation { get; set; }
    public required string RiskAssessment { get; set; }
    public required List<string> KeyStrengths { get; set; }
    public required List<string> KeyConcerns { get; set; }
    public DateTime ReportGenerated { get; set; }
}
