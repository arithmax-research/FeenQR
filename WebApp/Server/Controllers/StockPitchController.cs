using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockPitchController : ControllerBase
    {
        private readonly StockPitchService _stockPitchService;
        private readonly ILogger<StockPitchController> _logger;

        public StockPitchController(
            StockPitchService stockPitchService,
            ILogger<StockPitchController> logger)
        {
            _stockPitchService = stockPitchService;
            _logger = logger;
        }

        /// <summary>
        /// Generate a complete institutional-quality stock pitch for a given symbol.
        /// Uses DeepSeek/LLM combined with real research data from all available
        /// data sources (Fundamental Analysis, Valuation, News/Sentiment, SEC Filings,
        /// Earnings, Technical Analysis, etc.)
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateStockPitch([FromBody] StockPitchRequest request)
        {
            try
            {
                _logger.LogInformation("Generating stock pitch for {Symbol}, position: {PositionType}", 
                    request.Symbol, request.PositionType);

                if (string.IsNullOrWhiteSpace(request.Symbol))
                {
                    return BadRequest(new { error = "Symbol is required" });
                }

                var result = await _stockPitchService.GenerateStockPitchAsync(
                    request.Symbol.Trim().ToUpper(), 
                    request.PositionType ?? "auto");

                if (!result.Success)
                {
                    return StatusCode(500, new { 
                        error = "Failed to generate stock pitch", 
                        details = string.Join("; ", result.Errors),
                        symbol = result.Symbol
                    });
                }

                return Ok(new
                {
                    success = true,
                    symbol = result.Symbol,
                    positionType = result.PositionType,
                    targetPrice = result.TargetPriceFormatted,
                    expectedReturn = result.ExpectedReturnFormatted,
                    conviction = result.Conviction,
                    timeHorizon = result.TimeHorizon,
                    generatedAt = result.GeneratedAt,
                    generationTimeMs = result.GenerationTimeMs,
                    dataSourcesUsed = result.DataSourcesUsed,
                    pitch = new
                    {
                        stockRecommendation = result.StockRecommendation,
                        companyOverview = result.CompanyOverview,
                        investmentThesis = result.InvestmentThesis,
                        catalysts = result.Catalysts,
                        valuationAndReturns = result.ValuationAndReturns,
                        risksAndMitigation = result.RisksAndMitigation,
                        conclusion = result.Conclusion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating stock pitch for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = $"Internal error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Quick endpoint that generates a summary-level stock pitch (faster, less detail).
        /// </summary>
        [HttpPost("quick")]
        public async Task<IActionResult> QuickStockPitch([FromBody] StockPitchRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Symbol))
                {
                    return BadRequest(new { error = "Symbol is required" });
                }

                var result = await _stockPitchService.GenerateStockPitchAsync(
                    request.Symbol.Trim().ToUpper(), 
                    request.PositionType ?? "auto");

                if (!result.Success)
                {
                    return StatusCode(500, new { error = "Failed to generate stock pitch" });
                }

                // Return a condensed summary
                return Ok(new
                {
                    success = true,
                    symbol = result.Symbol,
                    summary = result.Conclusion,
                    recommendation = result.StockRecommendation?.Split('\n').FirstOrDefault() ?? result.PositionType,
                    targetPrice = result.TargetPriceFormatted,
                    expectedReturn = result.ExpectedReturnFormatted,
                    conviction = result.Conviction,
                    generatedAt = result.GeneratedAt,
                    dataSourcesUsed = result.DataSourcesUsed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick pitch for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Health check endpoint.
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { 
                status = "healthy", 
                service = "StockPitchService",
                timestamp = DateTime.UtcNow 
            });
        }
    }

    public class StockPitchRequest
    {
        /// <summary>Stock ticker symbol (e.g., AAPL, MSFT, TSLA)</summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// Position type: "long", "short", or "auto" (let AI decide based on data).
        /// Default is "auto".
        /// </summary>
        public string? PositionType { get; set; } = "auto";
    }
}
