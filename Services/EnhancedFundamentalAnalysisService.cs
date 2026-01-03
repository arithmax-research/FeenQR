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
/// </summary>
public class EnhancedFundamentalAnalysisService
{
    private readonly AlphaVantageService _alphaVantageService;
    private readonly FinancialModelingPrepService _fmpService;
    private readonly ILogger<EnhancedFundamentalAnalysisService> _logger;

    public EnhancedFundamentalAnalysisService(
        AlphaVantageService alphaVantageService,
        FinancialModelingPrepService fmpService,
        ILogger<EnhancedFundamentalAnalysisService> logger)
    {
        _alphaVantageService = alphaVantageService;
        _fmpService = fmpService;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive company overview combining multiple data sources
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

            await Task.WhenAll(fmpProfileTask, fmpKeyMetricsTask, fmpRatiosTask, fmpQuoteTask);

            var fmpProfile = await fmpProfileTask;
            var fmpKeyMetrics = await fmpKeyMetricsTask;
            var fmpRatios = await fmpRatiosTask;
            var fmpQuote = await fmpQuoteTask;

            // Combine data from all sources
            var overview = new EnhancedCompanyOverview
            {
                Symbol = symbol,
                CompanyName = fmpProfile?.CompanyName,
                Description = fmpProfile?.Description,
                Industry = fmpProfile?.Industry,
                Sector = fmpProfile?.Sector,
                Exchange = fmpQuote?.Exchange,
                Country = fmpProfile?.Country,
                Website = fmpProfile?.Website,
                CEO = fmpProfile?.CEO,
                Employees = int.TryParse(fmpProfile?.FullTimeEmployees, out var emp) ? emp : 0,
                MarketCap = (long)(fmpQuote?.MarketCap ?? fmpKeyMetrics?.FirstOrDefault()?.MarketCap ?? 0),
                PERatio = fmpKeyMetrics?.FirstOrDefault()?.PeRatio ?? 0,
                PEGRatio = fmpRatios?.FirstOrDefault()?.PriceEarningsToGrowthRatio ?? 0,
                BookValue = fmpKeyMetrics?.FirstOrDefault()?.BookValuePerShare ?? 0,
                DividendPerShare = fmpKeyMetrics?.FirstOrDefault()?.DividendYield ?? 0,
                DividendYield = fmpRatios?.FirstOrDefault()?.DividendYield ?? 0,
                EPS = fmpKeyMetrics?.FirstOrDefault()?.NetIncomePerShare ?? 0,
                RevenuePerShareTTM = fmpKeyMetrics?.FirstOrDefault()?.RevenuePerShare ?? 0,
                ProfitMargin = fmpRatios?.FirstOrDefault()?.NetProfitMargin ?? 0,
                OperatingMarginTTM = fmpRatios?.FirstOrDefault()?.OperatingMargin ?? 0,
                ReturnOnAssetsTTM = fmpRatios?.FirstOrDefault()?.ReturnOnAssets ?? 0,
                ReturnOnEquityTTM = fmpRatios?.FirstOrDefault()?.ReturnOnEquity ?? 0,
                QuarterlyEarningsGrowthYOY = 0, // Not available in FMP basic plan
                QuarterlyRevenueGrowthYOY = 0, // Not available in FMP basic plan
                AnalystTargetPrice = 0, // Not available in FMP basic plan
                TrailingPE = fmpKeyMetrics?.FirstOrDefault()?.PeRatio ?? 0,
                ForwardPE = 0, // Not available in FMP basic plan
                PriceToSalesRatioTTM = fmpKeyMetrics?.FirstOrDefault()?.PriceToSalesRatio ?? 0,
                PriceToBookRatio = fmpKeyMetrics?.FirstOrDefault()?.PbRatio ?? 0,
                EVToRevenue = fmpKeyMetrics?.FirstOrDefault()?.EvToSales ?? 0,
                EVToEBITDA = fmpKeyMetrics?.FirstOrDefault()?.EnterpriseValueOverEBITDA ?? 0,
                Beta = 0, // Not available in FMP basic plan
                FiftyTwoWeekHigh = fmpQuote?.YearHigh ?? 0,
                FiftyTwoWeekLow = fmpQuote?.YearLow ?? 0,
                FiftyDayMovingAverage = fmpQuote?.PriceAvg50 ?? 0,
                TwoHundredDayMovingAverage = fmpQuote?.PriceAvg200 ?? 0,
                SharesOutstanding = fmpQuote?.SharesOutstanding ?? 0,
                DividendDate = null, // Not available in FMP basic plan
                ExDividendDate = null, // Not available in FMP basic plan
                LastSplitFactor = null, // Not available in FMP basic plan
                LastSplitDate = null, // Not available in FMP basic plan
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
                    Revenue = f.Revenue,
                    CostOfRevenue = f.CostOfRevenue,
                    GrossProfit = f.GrossProfit,
                    OperatingExpenses = f.OperatingExpenses,
                    OperatingIncome = f.OperatingIncome,
                    NetIncome = f.NetIncome,
                    EPS = f.Eps,
                    EPSDiluted = f.Epsdiluted
                }).ToList() ?? new List<EnhancedIncomeStatement>(),
                BalanceSheets = fmpBalance?.Select(f => new EnhancedBalanceSheet
                {
                    Date = f.Date,
                    Period = f.Period,
                    TotalAssets = f.TotalAssets,
                    TotalLiabilities = f.TotalLiabilities,
                    TotalEquity = f.TotalEquity,
                    CashAndEquivalents = f.CashAndCashEquivalents,
                    TotalCurrentAssets = f.TotalCurrentAssets,
                    TotalCurrentLiabilities = f.TotalCurrentLiabilities,
                    TotalDebt = f.ShortTermDebt + f.LongTermDebt,
                    LongTermDebt = f.LongTermDebt
                }).ToList() ?? new List<EnhancedBalanceSheet>(),
                CashFlowStatements = fmpCashFlow?.Select(f => new EnhancedCashFlow
                {
                    Date = f.Date,
                    Period = f.Period,
                    OperatingCashFlow = f.OperatingCashFlow,
                    InvestingCashFlow = f.NetCashUsedForInvestingActivites,
                    FinancingCashFlow = f.NetCashUsedProvidedByFinancingActivities,
                    FreeCashFlow = f.FreeCashFlow
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
    /// Get comprehensive valuation analysis
    /// </summary>
    public async Task<EnhancedValuationAnalysis> GetComprehensiveValuationAnalysisAsync(string symbol)
    {
        try
        {
            // Get current quote and key metrics from FMP
            var fmpQuoteTask = _fmpService.GetQuoteAsync(symbol);
            var fmpMetricsTask = _fmpService.GetKeyMetricsAsync(symbol, 1);
            var fmpRatiosTask = _fmpService.GetFinancialRatiosAsync(symbol, 1);
            var fmpProfileTask = _fmpService.GetCompanyProfileAsync(symbol);

            await Task.WhenAll(fmpQuoteTask, fmpMetricsTask, fmpRatiosTask, fmpProfileTask);

            var quote = await fmpQuoteTask;
            var metrics = (await fmpMetricsTask)?.FirstOrDefault();
            var ratios = (await fmpRatiosTask)?.FirstOrDefault();
            var profile = await fmpProfileTask;

            if (quote == null || metrics == null || ratios == null)
            {
                return null;
            }

            // Calculate valuation metrics using FMP data
            var analysis = new EnhancedValuationAnalysis
            {
                Symbol = symbol,
                CurrentPrice = quote.Price,
                MarketCap = quote.MarketCap,
                SharesOutstanding = quote.SharesOutstanding,

                // Price multiples
                PERatio = metrics.PeRatio,
                PriceToBook = metrics.PbRatio,
                PriceToSales = metrics.PriceToSalesRatio,
                EVToEBITDA = metrics.EnterpriseValueOverEBITDA,
                EVToRevenue = metrics.EvToSales,

                // Growth metrics (not available from FMP, set to 0)
                EPS = quote.Eps,
                EPSGrowth = 0, // FMP doesn't provide growth data
                RevenueGrowth = 0, // FMP doesn't provide growth data

                // Profitability
                ROE = metrics.Roe,
                ROA = ratios.ReturnOnAssets,
                ProfitMargin = ratios.NetProfitMargin,
                OperatingMargin = ratios.OperatingMargin,

                // Financial health
                DebtToEquity = metrics.DebtToEquity,
                CurrentRatio = metrics.CurrentRatio,
                InterestCoverage = metrics.InterestCoverage,

                // Market data
                FiftyTwoWeekHigh = quote.YearHigh,
                FiftyTwoWeekLow = quote.YearLow,
                Beta = 0, // FMP doesn't provide beta data

                // Analyst estimates
                AnalystTargetPrice = 0, // FMP doesn't provide analyst target price
                DividendYield = metrics.DividendYield,

                AnalysisDate = DateTime.UtcNow
            };

            // Calculate additional metrics
            analysis.UpsidePotential = analysis.AnalystTargetPrice > 0 ?
                ((analysis.AnalystTargetPrice - analysis.CurrentPrice) / analysis.CurrentPrice) * 100 : 0;

            analysis.DistanceFrom52WeekHigh = analysis.FiftyTwoWeekHigh > 0 ?
                ((analysis.CurrentPrice - analysis.FiftyTwoWeekHigh) / analysis.FiftyTwoWeekHigh) * 100 : 0;

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
            // Get analyst estimates from FMP
            var estimatesTask = _fmpService.GetAnalystEstimatesAsync(symbol, 4);

            await Task.WhenAll(estimatesTask);

            var estimates = await estimatesTask;

            if (estimates == null || estimates.Count == 0)
            {
                return null;
            }

            var analysis = new EnhancedAnalystAnalysis
            {
                Symbol = symbol,
                AnalystTargetPrice = 0, // FMP doesn't provide target price data
                AnalystRatingStrongBuy = 0, // FMP doesn't provide rating data
                AnalystRatingBuy = 0,
                AnalystRatingHold = 0,
                AnalystRatingSell = 0,
                AnalystRatingStrongSell = 0,
                NumberOfAnalystOpinions = 0,

                // Use latest estimates
                LatestEstimates = estimates.FirstOrDefault(),
                AllEstimates = estimates,

                AnalysisDate = DateTime.UtcNow
            };

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting comprehensive analyst analysis for {symbol}");
            return null;
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
            Revenue = f.Revenue,
            CostOfRevenue = f.CostOfRevenue,
            GrossProfit = f.GrossProfit,
            OperatingExpenses = f.OperatingExpenses,
            OperatingIncome = f.OperatingIncome,
            NetIncome = f.NetIncome,
            EPS = f.Eps,
            EPSDiluted = f.Epsdiluted
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
            TotalAssets = f.TotalAssets,
            TotalLiabilities = f.TotalLiabilities,
            TotalEquity = f.TotalEquity,
            CashAndEquivalents = f.CashAndCashEquivalents,
            TotalCurrentAssets = f.TotalCurrentAssets,
            TotalCurrentLiabilities = f.TotalCurrentLiabilities,
            LongTermDebt = f.LongTermDebt,
            TotalDebt = f.TotalDebt
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
            OperatingCashFlow = f.NetCashProvidedByOperatingActivities,
            InvestingCashFlow = f.NetCashUsedForInvestingActivites,
            FinancingCashFlow = f.NetCashUsedProvidedByFinancingActivities,
            FreeCashFlow = f.FreeCashFlow,
            CapitalExpenditure = f.CapitalExpenditure,
            NetChangeInCash = f.NetChangeInCash
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
    public required FMPAnalystEstimates LatestEstimates { get; set; }
    public required List<FMPAnalystEstimates> AllEstimates { get; set; }
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
