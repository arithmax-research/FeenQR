using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace Server;

[ApiController]
[Route("api/[controller]")]
public class FundamentalAnalysisController : ControllerBase
{
    private readonly EnhancedFundamentalAnalysisService _fundamentalService;
    private readonly ILogger<FundamentalAnalysisController> _logger;

    public FundamentalAnalysisController(
        EnhancedFundamentalAnalysisService fundamentalService,
        ILogger<FundamentalAnalysisController> logger)
    {
        _fundamentalService = fundamentalService;
        _logger = logger;
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok("API is working");
    }

    [HttpGet("company-overview")]
    public async Task<IActionResult> GetCompanyOverview([FromQuery] string symbol)
    {
        try
        {
            _logger.LogInformation("Getting company overview for {Symbol}", symbol);
            var overview = await _fundamentalService.GetComprehensiveCompanyOverviewAsync(symbol);
            
            if (overview == null)
            {
                return NotFound(new { error = $"No data found for symbol {symbol}" });
            }

            return Ok(new
            {
                symbol = overview.Symbol,
                companyName = overview.CompanyName,
                description = overview.Description,
                industry = overview.Industry,
                sector = overview.Sector,
                exchange = overview.Exchange,
                country = overview.Country,
                website = overview.Website,
                ceo = overview.CEO,
                employees = overview.Employees,
                marketCap = overview.MarketCap,
                peRatio = overview.PERatio,
                pegRatio = overview.PEGRatio,
                bookValue = overview.BookValue,
                dividendPerShare = overview.DividendPerShare,
                dividendYield = overview.DividendYield,
                eps = overview.EPS,
                revenuePerShareTTM = overview.RevenuePerShareTTM,
                profitMargin = overview.ProfitMargin,
                operatingMarginTTM = overview.OperatingMarginTTM,
                returnOnAssetsTTM = overview.ReturnOnAssetsTTM,
                returnOnEquityTTM = overview.ReturnOnEquityTTM,
                quarterlyEarningsGrowthYOY = overview.QuarterlyEarningsGrowthYOY,
                quarterlyRevenueGrowthYOY = overview.QuarterlyRevenueGrowthYOY,
                analystTargetPrice = overview.AnalystTargetPrice,
                trailingPE = overview.TrailingPE,
                forwardPE = overview.ForwardPE,
                priceToSalesRatioTTM = overview.PriceToSalesRatioTTM,
                priceToBookRatio = overview.PriceToBookRatio,
                evToRevenue = overview.EVToRevenue,
                evToEBITDA = overview.EVToEBITDA,
                beta = overview.Beta,
                fiftyTwoWeekHigh = overview.FiftyTwoWeekHigh,
                fiftyTwoWeekLow = overview.FiftyTwoWeekLow,
                fiftyDayMovingAverage = overview.FiftyDayMovingAverage,
                twoHundredDayMovingAverage = overview.TwoHundredDayMovingAverage,
                sharesOutstanding = overview.SharesOutstanding,
                dividendDate = overview.DividendDate,
                exDividendDate = overview.ExDividendDate,
                lastSplitFactor = overview.LastSplitFactor,
                lastSplitDate = overview.LastSplitDate,
                ipoDate = overview.IpoDate,
                isActivelyTrading = overview.IsActivelyTrading
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company overview for {Symbol}", symbol);
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    [HttpPost("overview")]
    public async Task<IActionResult> GetCompanyOverview([FromBody] FundamentalAnalysisRequest request)
    {
        try
        {
            _logger.LogInformation("Getting company overview for {Symbol}", request.Symbol);
            var overview = await _fundamentalService.GetComprehensiveCompanyOverviewAsync(request.Symbol);
            
            if (overview == null)
            {
                return NotFound(new { error = $"No data found for symbol {request.Symbol}" });
            }

            return Ok(new
            {
                symbol = overview.Symbol,
                companyName = overview.CompanyName,
                description = overview.Description,
                industry = overview.Industry,
                sector = overview.Sector,
                exchange = overview.Exchange,
                country = overview.Country,
                website = overview.Website,
                ceo = overview.CEO,
                employees = overview.Employees,
                marketCap = overview.MarketCap,
                peRatio = overview.PERatio,
                pegRatio = overview.PEGRatio,
                bookValue = overview.BookValue,
                dividendPerShare = overview.DividendPerShare,
                dividendYield = overview.DividendYield,
                eps = overview.EPS,
                revenuePerShareTTM = overview.RevenuePerShareTTM,
                profitMargin = overview.ProfitMargin,
                operatingMarginTTM = overview.OperatingMarginTTM,
                returnOnAssetsTTM = overview.ReturnOnAssetsTTM,
                returnOnEquityTTM = overview.ReturnOnEquityTTM,
                quarterlyEarningsGrowthYOY = overview.QuarterlyEarningsGrowthYOY,
                quarterlyRevenueGrowthYOY = overview.QuarterlyRevenueGrowthYOY,
                analystTargetPrice = overview.AnalystTargetPrice,
                trailingPE = overview.TrailingPE,
                forwardPE = overview.ForwardPE,
                priceToSalesRatioTTM = overview.PriceToSalesRatioTTM,
                priceToBookRatio = overview.PriceToBookRatio,
                evToRevenue = overview.EVToRevenue,
                evToEBITDA = overview.EVToEBITDA,
                beta = overview.Beta,
                fiftyTwoWeekHigh = overview.FiftyTwoWeekHigh,
                fiftyTwoWeekLow = overview.FiftyTwoWeekLow,
                fiftyDayMovingAverage = overview.FiftyDayMovingAverage,
                twoHundredDayMovingAverage = overview.TwoHundredDayMovingAverage,
                sharesOutstanding = overview.SharesOutstanding,
                dividendDate = overview.DividendDate,
                exDividendDate = overview.ExDividendDate,
                lastSplitFactor = overview.LastSplitFactor,
                lastSplitDate = overview.LastSplitDate,
                ipoDate = overview.IpoDate,
                isActivelyTrading = overview.IsActivelyTrading,
                timestamp = DateTime.UtcNow,
                type = "company_overview"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in company overview for {Symbol}", request.Symbol);
            return StatusCode(500, new { error = "Internal server error processing request" });
        }
    }

    [HttpPost("financials")]
    public async Task<IActionResult> GetFinancialStatements([FromBody] FundamentalAnalysisRequest request)
    {
        try
        {
            _logger.LogInformation("Getting financial statements for {Symbol}", request.Symbol);
            var statements = await _fundamentalService.GetComprehensiveFinancialStatementsAsync(request.Symbol);
            
            if (statements == null)
            {
                return NotFound(new { error = $"No financial data found for symbol {request.Symbol}" });
            }

            return Ok(new
            {
                symbol = statements.Symbol,
                incomeStatements = statements.IncomeStatements,
                balanceSheets = statements.BalanceSheets,
                cashFlowStatements = statements.CashFlowStatements,
                lastUpdated = statements.LastUpdated,
                timestamp = DateTime.UtcNow,
                type = "financial_statements"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in financial statements for {Symbol}", request.Symbol);
            return StatusCode(500, new { error = "Internal server error processing request" });
        }
    }

    [HttpPost("valuation")]
    public async Task<IActionResult> GetValuationAnalysis([FromBody] FundamentalAnalysisRequest request)
    {
        try
        {
            _logger.LogInformation("Getting valuation analysis for {Symbol}", request.Symbol);
            var valuation = await _fundamentalService.GetComprehensiveValuationAnalysisAsync(request.Symbol);
            
            if (valuation == null)
            {
                return NotFound(new { error = $"No valuation data found for symbol {request.Symbol}" });
            }

            return Ok(new
            {
                symbol = valuation.Symbol,
                currentPrice = valuation.CurrentPrice,
                intrinsicValue = valuation.IntrinsicValue,
                fairValue = valuation.FairValue,
                valuationMetrics = valuation.ValuationMetrics,
                growthMetrics = valuation.GrowthMetrics,
                profitabilityMetrics = valuation.ProfitabilityMetrics,
                financialHealthMetrics = valuation.FinancialHealthMetrics,
                recommendation = valuation.Recommendation,
                reasoning = valuation.Reasoning,
                timestamp = DateTime.UtcNow,
                type = "valuation_analysis"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in valuation analysis for {Symbol}", request.Symbol);
            return StatusCode(500, new { error = "Internal server error processing request" });
        }
    }

    [HttpPost("analyst")]
    public async Task<IActionResult> GetAnalystAnalysis([FromBody] FundamentalAnalysisRequest request)
    {
        try
        {
            _logger.LogInformation("Getting analyst analysis for {Symbol}", request.Symbol);
            var analyst = await _fundamentalService.GetComprehensiveAnalystAnalysisAsync(request.Symbol);
            
            if (analyst == null)
            {
                return NotFound(new { error = $"No analyst data found for symbol {request.Symbol}" });
            }

            return Ok(new
            {
                symbol = analyst.Symbol,
                consensusRating = analyst.ConsensusRating,
                averageTargetPrice = analyst.AverageTargetPrice,
                highTargetPrice = analyst.HighTargetPrice,
                lowTargetPrice = analyst.LowTargetPrice,
                numberOfAnalysts = analyst.NumberOfAnalysts,
                buyRatings = analyst.BuyRatings,
                holdRatings = analyst.HoldRatings,
                sellRatings = analyst.SellRatings,
                estimates = analyst.Estimates,
                upgrades = analyst.Upgrades,
                downgrades = analyst.Downgrades,
                timestamp = DateTime.UtcNow,
                type = "analyst_analysis"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in analyst analysis for {Symbol}", request.Symbol);
            return StatusCode(500, new { error = "Internal server error processing request" });
        }
    }

    [HttpPost("compare")]
    public async Task<IActionResult> CompareCompanies([FromBody] FundamentalCompareRequest request)
    {
        try
        {
            _logger.LogInformation("Comparing companies: {Symbols}", string.Join(", ", request.Symbols));
            var comparisonJson = await _fundamentalService.CompareCompaniesFundamentalAsync(request.Symbols.ToArray());
            
            if (string.IsNullOrEmpty(comparisonJson))
            {
                return NotFound(new { error = "No comparison data found" });
            }

            // Deserialize the JSON string to get the actual comparison objects
            var comparisons = System.Text.Json.JsonSerializer.Deserialize<List<object>>(comparisonJson);

            return Ok(new
            {
                comparisons = comparisons,
                timestamp = DateTime.UtcNow,
                type = "company_comparison"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing companies");
            return StatusCode(500, new { error = "Internal server error processing request" });
        }
    }
}

public class FundamentalAnalysisRequest
{
    public string Symbol { get; set; } = string.Empty;
}

public class FundamentalCompareRequest
{
    public List<string> Symbols { get; set; } = new();
}
