using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Plugin for Enhanced Fundamental Analysis service
/// Provides comprehensive fundamental analysis combining multiple data sources
/// </summary>
public class EnhancedFundamentalAnalysisPlugin
{
    private readonly EnhancedFundamentalAnalysisService _analysisService;

    public EnhancedFundamentalAnalysisPlugin(EnhancedFundamentalAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    [KernelFunction("get_comprehensive_company_overview")]
    [Description("Get comprehensive company overview combining data from multiple free financial data sources")]
    public async Task<string> GetComprehensiveCompanyOverviewAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        var overview = await _analysisService.GetComprehensiveCompanyOverviewAsync(symbol);
        if (overview == null)
        {
            return $"No comprehensive overview found for symbol: {symbol}";
        }

        return $@"Comprehensive Company Overview for {overview.Symbol} ({overview.CompanyName}):

COMPANY INFORMATION:
Industry: {overview.Industry}
Sector: {overview.Sector}
Exchange: {overview.Exchange}
Country: {overview.Country}
Website: {overview.Website}
CEO: {overview.CEO}
Employees: {overview.Employees:N0}
IPO Date: {overview.IpoDate}
Actively Trading: {overview.IsActivelyTrading}

MARKET DATA:
Market Cap: ${overview.MarketCap:N0}
Shares Outstanding: {overview.SharesOutstanding:N0}
Current P/E Ratio: {overview.PERatio:F2}
PEG Ratio: {overview.PEGRatio:F2}
Beta: {overview.Beta:F2}

VALUATION METRICS:
Price to Book: {overview.PriceToBookRatio:F2}
Price to Sales: {overview.PriceToSalesRatioTTM:F2}
EV to Revenue: {overview.EVToRevenue:F2}
EV to EBITDA: {overview.EVToEBITDA:F2}
52-Week High: ${overview.FiftyTwoWeekHigh:F2}
52-Week Low: ${overview.FiftyTwoWeekLow:F2}

PROFITABILITY:
EPS: ${overview.EPS:F2}
Profit Margin: {overview.ProfitMargin:P2}
Operating Margin: {overview.OperatingMarginTTM:P2}
Return on Assets: {overview.ReturnOnAssetsTTM:P2}
Return on Equity: {overview.ReturnOnEquityTTM:P2}

GROWTH:
Quarterly Earnings Growth: {overview.QuarterlyEarningsGrowthYOY:P2}
Quarterly Revenue Growth: {overview.QuarterlyRevenueGrowthYOY:P2}

DIVIDENDS:
Dividend Per Share: ${overview.DividendPerShare:F2}
Dividend Yield: {overview.DividendYield:P2}
Dividend Date: {overview.DividendDate}
Ex-Dividend Date: {overview.ExDividendDate}

ANALYST DATA:
Analyst Target Price: ${overview.AnalystTargetPrice:F2}

DESCRIPTION:
{overview.Description}";
    }

    [KernelFunction("get_comprehensive_financial_statements")]
    [Description("Get comprehensive financial statements combining data from multiple sources")]
    public async Task<string> GetComprehensiveFinancialStatementsAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        var statements = await _analysisService.GetComprehensiveFinancialStatementsAsync(symbol);
        if (statements == null)
        {
            return $"No comprehensive financial statements found for symbol: {symbol}";
        }

        var result = $"Comprehensive Financial Statements for {statements.Symbol} (Last Updated: {statements.LastUpdated:yyyy-MM-dd})\n\n";

        // Income Statement Summary
        result += "INCOME STATEMENT SUMMARY (Latest Period):\n";
        if (statements.IncomeStatements.Any())
        {
            var latestIncome = statements.IncomeStatements.First();
            result += $@"Period: {latestIncome.Date} ({latestIncome.Period})
Revenue: ${latestIncome.Revenue:N0}
Gross Profit: ${latestIncome.GrossProfit:N0}
Operating Income: ${latestIncome.OperatingIncome:N0}
Net Income: ${latestIncome.NetIncome:N0}
EPS: ${latestIncome.EPS:F2}
EPS Diluted: ${latestIncome.EPSDiluted:F2}

";
        }

        // Balance Sheet Summary
        result += "BALANCE SHEET SUMMARY (Latest Period):\n";
        if (statements.BalanceSheets.Any())
        {
            var latestBalance = statements.BalanceSheets.First();
            result += $@"Period: {latestBalance.Date} ({latestBalance.Period})
Total Assets: ${latestBalance.TotalAssets:N0}
Total Liabilities: ${latestBalance.TotalLiabilities:N0}
Total Equity: ${latestBalance.TotalEquity:N0}
Cash & Equivalents: ${latestBalance.CashAndEquivalents:N0}
Total Current Assets: ${latestBalance.TotalCurrentAssets:N0}
Total Current Liabilities: ${latestBalance.TotalCurrentLiabilities:N0}
Long-term Debt: ${latestBalance.LongTermDebt:N0}
Total Debt: ${latestBalance.TotalDebt:N0}

";
        }

        // Cash Flow Summary
        result += "CASH FLOW SUMMARY (Latest Period):\n";
        if (statements.CashFlowStatements.Any())
        {
            var latestCashFlow = statements.CashFlowStatements.First();
            result += $@"Period: {latestCashFlow.Date} ({latestCashFlow.Period})
Operating Cash Flow: ${latestCashFlow.OperatingCashFlow:N0}
Investing Cash Flow: ${latestCashFlow.InvestingCashFlow:N0}
Financing Cash Flow: ${latestCashFlow.FinancingCashFlow:N0}
Free Cash Flow: ${latestCashFlow.FreeCashFlow:N0}
Capital Expenditure: ${latestCashFlow.CapitalExpenditure:N0}
Net Change in Cash: ${latestCashFlow.NetChangeInCash:N0}

";
        }

        return result.Trim();
    }

    [KernelFunction("get_comprehensive_valuation_analysis")]
    [Description("Get comprehensive valuation analysis with multiple metrics and ratios")]
    public async Task<string> GetComprehensiveValuationAnalysisAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        var analysis = await _analysisService.GetComprehensiveValuationAnalysisAsync(symbol);
        if (analysis == null)
        {
            return $"No comprehensive valuation analysis available for symbol: {symbol}";
        }

        return $@"Comprehensive Valuation Analysis for {analysis.Symbol} (Analysis Date: {analysis.AnalysisDate:yyyy-MM-dd})

CURRENT MARKET DATA:
Current Price: ${analysis.CurrentPrice:F2}
Market Cap: ${analysis.MarketCap:N0}
Shares Outstanding: {analysis.SharesOutstanding:N0}

VALUATION MULTIPLES:
P/E Ratio: {analysis.PERatio:F2}
Price to Book: {analysis.PriceToBook:F2}
Price to Sales: {analysis.PriceToSales:F2}
EV/EBITDA: {analysis.EVToEBITDA:F2}
EV/Revenue: {analysis.EVToRevenue:F2}

PROFITABILITY METRICS:
EPS: ${analysis.EPS:F2}
ROE: {analysis.ROE:P2}
ROA: {analysis.ROA:P2}
Profit Margin: {analysis.ProfitMargin:P2}
Operating Margin: {analysis.OperatingMargin:P2}

GROWTH METRICS:
EPS Growth (YoY): {analysis.EPSGrowth:P2}
Revenue Growth (YoY): {analysis.RevenueGrowth:P2}

FINANCIAL HEALTH:
Debt to Equity: {analysis.DebtToEquity:F2}
Current Ratio: {analysis.CurrentRatio:F2}
Interest Coverage: {analysis.InterestCoverage:F2}

MARKET POSITIONING:
52-Week High: ${analysis.FiftyTwoWeekHigh:F2}
52-Week Low: ${analysis.FiftyTwoWeekLow:F2}
Distance from 52W High: {analysis.DistanceFrom52WeekHigh:F2}%
Beta: {analysis.Beta:F2}

ANALYST SENTIMENT:
Analyst Target Price: ${analysis.AnalystTargetPrice:F2}
Upside Potential: {analysis.UpsidePotential:F2}%
Dividend Yield: {analysis.DividendYield:P2}";
    }

    [KernelFunction("get_comprehensive_technical_analysis")]
    [Description("Get comprehensive technical analysis with multiple indicators and signals")]
    public async Task<string> GetComprehensiveTechnicalAnalysisAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        var analysis = await _analysisService.GetComprehensiveTechnicalAnalysisAsync(symbol);
        if (analysis == null)
        {
            return $"No comprehensive technical analysis available for symbol: {symbol}";
        }

        return $@"Comprehensive Technical Analysis for {analysis.Symbol} (Analysis Date: {analysis.AnalysisDate:yyyy-MM-dd})

PRICE ACTION:
Current Price: ${analysis.CurrentPrice:F2}
Previous Close: ${analysis.PreviousClose:F2}
Day Change: ${analysis.DayChange:F2} ({analysis.DayChangePercent:F2}%)

MOVING AVERAGES:
SMA(20): ${analysis.SMA20:F2}
EMA(20): ${analysis.EMA20:F2}
Signal: {analysis.MovingAverageSignal}

MOMENTUM INDICATORS:
RSI(14): {analysis.RSI14:F2} - {analysis.RSISignal}
Stochastic %K: {analysis.StochK:F2}
Stochastic %D: {analysis.StochD:F2}
Stochastic Signal: {analysis.StochasticSignal}

MACD:
MACD Line: {analysis.MACD:F4}
Signal Line: {analysis.MACDSignal:F4}
Histogram: {analysis.MACDHist:F4}
MACD Signal: {analysis.MACDSignal}

BOLLINGER BANDS:
Upper Band: ${analysis.BBUpper:F2}
Middle Band: ${analysis.BBMiddle:F2}
Lower Band: ${analysis.BBLower:F2}
Bollinger Signal: {analysis.BollingerSignal}

VOLUME ANALYSIS:
Volume: {analysis.Volume:N0}
Average Volume: {analysis.AverageVolume:N0}
Volume Ratio: {(analysis.AverageVolume > 0 ? (decimal)analysis.Volume / analysis.AverageVolume : 0):F2}x

VOLATILITY:
52-Week High: ${analysis.FiftyTwoWeekHigh:F2}
52-Week Low: ${analysis.FiftyTwoWeekLow:F2}
Distance from 52W High: {((analysis.CurrentPrice - analysis.FiftyTwoWeekHigh) / analysis.FiftyTwoWeekHigh * 100):F2}%";
    }

    [KernelFunction("get_comprehensive_analyst_analysis")]
    [Description("Get comprehensive analyst analysis with estimates and ratings")]
    public async Task<string> GetComprehensiveAnalystAnalysisAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        var analysis = await _analysisService.GetComprehensiveAnalystAnalysisAsync(symbol);
        if (analysis == null)
        {
            return $"No comprehensive analyst analysis available for symbol: {symbol}";
        }

        var result = $@"Comprehensive Analyst Analysis for {analysis.Symbol} (Analysis Date: {analysis.AnalysisDate:yyyy-MM-dd})

ANALYST TARGET PRICE:
Target Price: ${analysis.AnalystTargetPrice:F2}

ANALYST RATINGS SUMMARY:
Strong Buy: {analysis.AnalystRatingStrongBuy}
Buy: {analysis.AnalystRatingBuy}
Hold: {analysis.AnalystRatingHold}
Sell: {analysis.AnalystRatingSell}
Strong Sell: {analysis.AnalystRatingStrongSell}
Total Analysts: {analysis.NumberOfAnalystOpinions}

";

        if (analysis.LatestEstimates != null)
        {
            result += $@"LATEST ANALYST ESTIMATES:
Period: {analysis.LatestEstimates.Date} ({analysis.LatestEstimates.Period})
Revenue Estimate: ${analysis.LatestEstimates.EstimatedRevenueAvg:N0}M (Low: ${analysis.LatestEstimates.EstimatedRevenueLow:N0}M, High: ${analysis.LatestEstimates.EstimatedRevenueHigh:N0}M)
EBITDA Estimate: ${analysis.LatestEstimates.EstimatedEbitdaAvg:N0}M (Low: ${analysis.LatestEstimates.EstimatedEbitdaLow:N0}M, High: ${analysis.LatestEstimates.EstimatedEbitdaHigh:N0}M)
Net Income Estimate: ${analysis.LatestEstimates.EstimatedNetIncomeAvg:N0}M (Low: ${analysis.LatestEstimates.EstimatedNetIncomeLow:N0}M, High: ${analysis.LatestEstimates.EstimatedNetIncomeHigh:N0}M)
EPS Estimate: ${analysis.LatestEstimates.EstimatedEpsAvg:F2} (Low: ${analysis.LatestEstimates.EstimatedEpsLow:F2}, High: ${analysis.LatestEstimates.EstimatedEpsHigh:F2})
Number of Analysts: Revenue: {analysis.LatestEstimates.NumberAnalystEstimatedRevenue}, EPS: {analysis.LatestEstimates.NumberAnalystsEstimatedEps}

";
        }

        if (analysis.AllEstimates?.Count > 1)
        {
            result += "HISTORICAL ESTIMATES:\n";
            foreach (var estimate in analysis.AllEstimates.Skip(1).Take(3))
            {
                result += $@"{estimate.Date} ({estimate.Period}): EPS ${estimate.EstimatedEpsAvg:F2}, Revenue ${estimate.EstimatedRevenueAvg:N0}M
";
            }
        }

        return result.Trim();
    }

    [KernelFunction("generate_comprehensive_fundamental_report")]
    [Description("Generate a comprehensive fundamental analysis report combining all available data sources")]
    public async Task<string> GenerateComprehensiveFundamentalReportAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        var report = await _analysisService.GenerateComprehensiveReportAsync(symbol);
        if (report == null)
        {
            return $"Unable to generate comprehensive report for symbol: {symbol}";
        }

        var result = $@"COMPREHENSIVE FUNDAMENTAL ANALYSIS REPORT
========================================

Symbol: {report.Symbol}
Report Generated: {report.ReportGenerated:yyyy-MM-dd HH:mm:ss UTC}

INVESTMENT RECOMMENDATION: {report.InvestmentRecommendation}
RISK ASSESSMENT: {report.RiskAssessment}

KEY STRENGTHS:
{string.Join("\n", report.KeyStrengths.Select(s => $"• {s}"))}

KEY CONCERNS:
{string.Join("\n", report.KeyConcerns.Select(c => $"• {c}"))}

";

        if (report.CompanyOverview != null)
        {
            result += $@"COMPANY OVERVIEW:
• Company: {report.CompanyOverview.CompanyName}
• Industry: {report.CompanyOverview.Industry}
• Sector: {report.CompanyOverview.Sector}
• Market Cap: ${report.CompanyOverview.MarketCap:N0}
• Employees: {report.CompanyOverview.Employees:N0}
• P/E Ratio: {report.CompanyOverview.PERatio:F2}

";
        }

        if (report.ValuationAnalysis != null)
        {
            result += $@"VALUATION ANALYSIS:
• Current Price: ${report.ValuationAnalysis.CurrentPrice:F2}
• P/E Ratio: {report.ValuationAnalysis.PERatio:F2}
• Price to Book: {report.ValuationAnalysis.PriceToBook:F2}
• ROE: {report.ValuationAnalysis.ROE:P2}
• Debt to Equity: {report.ValuationAnalysis.DebtToEquity:F2}
• Analyst Target: ${report.ValuationAnalysis.AnalystTargetPrice:F2} ({report.ValuationAnalysis.UpsidePotential:F1}% upside)

";
        }

        if (report.TechnicalAnalysis != null)
        {
            result += $@"TECHNICAL ANALYSIS:
• Current Price: ${report.TechnicalAnalysis.CurrentPrice:F2}
• RSI(14): {report.TechnicalAnalysis.RSI14:F1} ({report.TechnicalAnalysis.RSISignal})
• MACD Signal: {report.TechnicalAnalysis.MACDSignal}
• Bollinger Bands: {report.TechnicalAnalysis.BollingerSignal}
• Moving Averages: {report.TechnicalAnalysis.MovingAverageSignal}

";
        }

        if (report.FinancialStatements?.IncomeStatements?.Any() == true)
        {
            var latestIncome = report.FinancialStatements.IncomeStatements.First();
            result += $@"LATEST FINANCIAL RESULTS:
• Revenue: ${latestIncome.Revenue:N0}
• Net Income: ${latestIncome.NetIncome:N0}
• EPS: ${latestIncome.EPS:F2}
• Gross Margin: {(latestIncome.Revenue > 0 ? (decimal)latestIncome.GrossProfit / latestIncome.Revenue : 0):P1}

";
        }

        result += @"
DISCLAIMER:
This report is for informational purposes only and should not be considered as investment advice.
Always conduct your own research and consider consulting with a financial advisor before making investment decisions.
Data sources: Alpha Vantage, IEX Cloud, Financial Modeling Prep (all free tiers)";

        return result;
    }

    [KernelFunction("compare_companies_fundamental")]
    [Description("Compare fundamental metrics between multiple companies")]
    public async Task<string> CompareCompaniesFundamentalAsync(
        [Description("Comma-separated list of stock symbols (e.g., AAPL,MSFT,GOOGL)")] string symbols)
    {
        var symbolList = symbols.Split(',').Select(s => s.Trim()).ToList();
        if (symbolList.Count < 2 || symbolList.Count > 5)
        {
            return "Please provide between 2 and 5 stock symbols separated by commas.";
        }

        var comparisons = new List<(string Symbol, EnhancedValuationAnalysis Analysis)>();

        foreach (var symbol in symbolList)
        {
            var analysis = await _analysisService.GetComprehensiveValuationAnalysisAsync(symbol);
            if (analysis != null)
            {
                comparisons.Add((symbol, analysis));
            }
        }

        if (comparisons.Count < 2)
        {
            return "Unable to retrieve sufficient data for comparison.";
        }

        var result = $"Fundamental Comparison Report ({comparisons.Count} companies)\n\n";
        result += "Symbol\tPrice\tP/E\tP/B\tROE\tD/E\tMkt Cap\n";
        result += "------\t-----\t---\t---\t---\t---\t-------\n";

        foreach (var (symbol, analysis) in comparisons)
        {
            result += $"{symbol}\t${analysis.CurrentPrice:F0}\t{analysis.PERatio:F1}\t{analysis.PriceToBook:F1}\t{analysis.ROE:P0}\t{analysis.DebtToEquity:F1}\t${analysis.MarketCap/1000000:F0}M\n";
        }

        result += "\nVALUATION ANALYSIS:\n";

        // Find best and worst performers
        var bestPE = comparisons.OrderBy(c => c.Analysis.PERatio).First();
        var worstPE = comparisons.OrderByDescending(c => c.Analysis.PERatio).First();
        result += $"Most Attractive P/E: {bestPE.Symbol} ({bestPE.Analysis.PERatio:F1})\n";
        result += $"Highest P/E: {worstPE.Symbol} ({worstPE.Analysis.PERatio:F1})\n\n";

        var bestROE = comparisons.OrderByDescending(c => c.Analysis.ROE).First();
        var worstROE = comparisons.OrderBy(c => c.Analysis.ROE).First();
        result += $"Highest ROE: {bestROE.Symbol} ({bestROE.Analysis.ROE:P1})\n";
        result += $"Lowest ROE: {worstROE.Symbol} ({worstROE.Analysis.ROE:P1})\n\n";

        var lowestDebt = comparisons.OrderBy(c => c.Analysis.DebtToEquity).First();
        var highestDebt = comparisons.OrderByDescending(c => c.Analysis.DebtToEquity).First();
        result += $"Lowest Debt/Equity: {lowestDebt.Symbol} ({lowestDebt.Analysis.DebtToEquity:F1})\n";
        result += $"Highest Debt/Equity: {highestDebt.Symbol} ({highestDebt.Analysis.DebtToEquity:F1})\n";

        return result;
    }
}
