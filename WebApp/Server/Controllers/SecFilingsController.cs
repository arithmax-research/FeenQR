using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.Text.Json;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecFilingsController : ControllerBase
    {
        private readonly ILogger<SecFilingsController> _logger;
        private readonly SecFilingsService _secFilingsService;
        private readonly EarningsService _earningsService;

        public SecFilingsController(
            ILogger<SecFilingsController> logger,
            SecFilingsService secFilingsService,
            EarningsService earningsService)
        {
            _logger = logger;
            _secFilingsService = secFilingsService;
            _earningsService = earningsService;
        }

        /// <summary>
        /// Get SEC filing analysis for a symbol
        /// </summary>
        [HttpPost("analysis")]
        public async Task<IActionResult> GetSecAnalysis([FromBody] SecAnalysisRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching SEC analysis for {Symbol}", request.Symbol);

                var analysis = await _secFilingsService.AnalyzeSecFilingAsync(request.Symbol, request.FilingType);
                
                return Ok(new
                {
                    success = true,
                    data = analysis,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching SEC analysis");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get SEC filing history
        /// </summary>
        [HttpGet("history/{symbol}")]
        public async Task<IActionResult> GetSecHistory(string symbol, [FromQuery] int limit = 10)
        {
            try
            {
                _logger.LogInformation("Fetching SEC filing history for {Symbol}", symbol);

                var history = await _secFilingsService.GetFilingHistoryAsync(symbol, limit);
                
                return Ok(new
                {
                    success = true,
                    data = history,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching SEC history");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get risk factors from 10-K filing
        /// </summary>
        [HttpPost("risk-factors")]
        public async Task<IActionResult> GetRiskFactors([FromBody] SymbolRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching risk factors for {Symbol}", request.Symbol);

                var riskFactors = await _secFilingsService.ExtractRiskFactorsAsync(request.Symbol);
                
                return Ok(new
                {
                    success = true,
                    data = riskFactors,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching risk factors");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get MD&A (Management Discussion & Analysis) from latest 10-K
        /// </summary>
        [HttpPost("management-discussion")]
        public async Task<IActionResult> GetMdA([FromBody] SymbolRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching MD&A for {Symbol}", request.Symbol);

                var mda = await _secFilingsService.ExtractMdAAsync(request.Symbol);
                
                return Ok(new
                {
                    success = true,
                    data = mda,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching MD&A");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get comprehensive SEC analysis (all sections)
        /// </summary>
        [HttpPost("comprehensive")]
        public async Task<IActionResult> GetComprehensiveSecAnalysis([FromBody] SymbolRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching comprehensive SEC analysis for {Symbol}", request.Symbol);

                var comprehensive = await _secFilingsService.GetComprehensiveAnalysisAsync(request.Symbol);
                
                return Ok(new
                {
                    success = true,
                    data = comprehensive,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching comprehensive SEC analysis");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get earnings analysis
        /// </summary>
        [HttpPost("earnings-analysis")]
        public async Task<IActionResult> GetEarningsAnalysis([FromBody] SymbolRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching earnings analysis for {Symbol}", request.Symbol);

                var analysis = await _earningsService.AnalyzeEarningsAsync(request.Symbol);
                
                return Ok(new
                {
                    success = true,
                    data = analysis,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching earnings analysis");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get earnings history
        /// </summary>
        [HttpGet("earnings-history/{symbol}")]
        public async Task<IActionResult> GetEarningsHistory(string symbol, [FromQuery] int limit = 8)
        {
            try
            {
                _logger.LogInformation("Fetching earnings history for {Symbol}", symbol);

                var history = await _earningsService.GetEarningsHistoryAsync(symbol, limit);
                
                return Ok(new
                {
                    success = true,
                    data = history,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching earnings history");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get earnings call sentiment analysis
        /// </summary>
        [HttpPost("earnings-sentiment")]
        public async Task<IActionResult> GetEarningsSentiment([FromBody] SymbolRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching earnings sentiment for {Symbol}", request.Symbol);

                var sentiment = await _earningsService.AnalyzeEarningsCallSentimentAsync(request.Symbol);
                
                return Ok(new
                {
                    success = true,
                    data = sentiment,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching earnings sentiment");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get strategic insights from earnings calls
        /// </summary>
        [HttpPost("earnings-strategic")]
        public async Task<IActionResult> GetEarningsStrategic([FromBody] SymbolRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching strategic insights for {Symbol}", request.Symbol);

                var strategic = await _earningsService.ExtractStrategicInsightsAsync(request.Symbol);
                
                return Ok(new
                {
                    success = true,
                    data = strategic,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching strategic insights");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get earnings risks analysis
        /// </summary>
        [HttpPost("earnings-risks")]
        public async Task<IActionResult> GetEarningsRisks([FromBody] SymbolRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching earnings risks for {Symbol}", request.Symbol);

                var risks = await _earningsService.AnalyzeEarningsRisksAsync(request.Symbol);
                
                return Ok(new
                {
                    success = true,
                    data = risks,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching earnings risks");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get comprehensive earnings analysis (all sections)
        /// </summary>
        [HttpPost("earnings-comprehensive")]
        public async Task<IActionResult> GetComprehensiveEarningsAnalysis([FromBody] SymbolRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching comprehensive earnings analysis for {Symbol}", request.Symbol);

                var comprehensive = await _earningsService.GetComprehensiveAnalysisAsync(request.Symbol);
                
                return Ok(new
                {
                    success = true,
                    data = comprehensive,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching comprehensive earnings analysis");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class SecAnalysisRequest
    {
        public string Symbol { get; set; }
        public string FilingType { get; set; } = "10-K"; // 10-K, 10-Q, 8-K
    }

    public class SymbolRequest
    {
        public string Symbol { get; set; }
    }
}
