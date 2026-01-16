using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services.ResearchAgents;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ComprehensiveAnalysisController : ControllerBase
    {
        private readonly ComprehensiveStockAnalysisAgent _comprehensiveAgent;
        private readonly ILogger<ComprehensiveAnalysisController> _logger;

        public ComprehensiveAnalysisController(
            ComprehensiveStockAnalysisAgent comprehensiveAgent,
            ILogger<ComprehensiveAnalysisController> logger)
        {
            _comprehensiveAgent = comprehensiveAgent;
            _logger = logger;
        }

        /// <summary>
        /// Performs comprehensive analysis combining web search, sentiment, technical, and fundamental analysis
        /// </summary>
        [HttpGet("analyze")]
        public async Task<IActionResult> AnalyzeStock(
            [FromQuery] string symbol,
            [FromQuery] string assetType = "stock")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return BadRequest(new { error = "Symbol parameter is required" });
                }

                _logger.LogInformation("Comprehensive analysis request for {Symbol} ({AssetType})", 
                    symbol, assetType);

                var result = await _comprehensiveAgent.AnalyzeAndRecommendAsync(symbol, assetType);

                return Ok(new
                {
                    symbol = symbol.ToUpper(),
                    assetType = assetType,
                    analysis = result,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive analysis for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
