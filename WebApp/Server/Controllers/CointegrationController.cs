using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CointegrationController : ControllerBase
    {
        private readonly ILogger<CointegrationController> _logger;
        private readonly CointegrationService _cointegrationService;
        private readonly CausalityService _causalityService;

        public CointegrationController(
            ILogger<CointegrationController> logger,
            CointegrationService cointegrationService,
            CausalityService causalityService)
        {
            _logger = logger;
            _cointegrationService = cointegrationService;
            _causalityService = causalityService;
        }

        /// <summary>
        /// Perform Engle-Granger cointegration test
        /// </summary>
        [HttpPost("engle-granger")]
        public async Task<IActionResult> EngleGrangerTest([FromBody] PairTestRequest request)
        {
            try
            {
                _logger.LogInformation("Performing Engle-Granger test for {Symbol1} vs {Symbol2}", request.Symbol1, request.Symbol2);

                var result = await _cointegrationService.EngleGrangerTestAsync(request.Symbol1, request.Symbol2, request.LookbackDays);
                
                return Ok(new
                {
                    success = true,
                    data = result,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing Engle-Granger test");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Perform Johansen cointegration test
        /// </summary>
        [HttpPost("johansen")]
        public async Task<IActionResult> JohansenTest([FromBody] MultiPairTestRequest request)
        {
            try
            {
                _logger.LogInformation("Performing Johansen test for multiple pairs");

                var result = await _cointegrationService.JohansenTestAsync(request.Symbols, request.LookbackDays);
                
                return Ok(new
                {
                    success = true,
                    data = result,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing Johansen test");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Perform Granger causality test
        /// </summary>
        [HttpPost("granger-causality")]
        public async Task<IActionResult> GrangerCausalityTest([FromBody] PairTestRequest request)
        {
            try
            {
                _logger.LogInformation("Performing Granger causality test for {Symbol1} vs {Symbol2}", request.Symbol1, request.Symbol2);

                var result = await _causalityService.GrangerCausalityTestAsync(request.Symbol1, request.Symbol2, request.LookbackDays, request.MaxLags);
                
                return Ok(new
                {
                    success = true,
                    data = result,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing Granger causality test");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Perform lead-lag analysis
        /// </summary>
        [HttpPost("lead-lag-analysis")]
        public async Task<IActionResult> LeadLagAnalysis([FromBody] PairTestRequest request)
        {
            try
            {
                _logger.LogInformation("Performing lead-lag analysis for {Symbol1} vs {Symbol2}", request.Symbol1, request.Symbol2);

                var result = await _causalityService.LeadLagAnalysisAsync(request.Symbol1, request.Symbol2, request.LookbackDays);
                
                return Ok(new
                {
                    success = true,
                    data = result,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing lead-lag analysis");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Comprehensive cointegration analysis
        /// </summary>
        [HttpPost("comprehensive-analysis")]
        public async Task<IActionResult> ComprehensiveAnalysis([FromBody] PairTestRequest request)
        {
            try
            {
                _logger.LogInformation("Performing comprehensive cointegration analysis for {Symbol1} vs {Symbol2}", request.Symbol1, request.Symbol2);

                var result = await _cointegrationService.ComprehensiveCointegrationAnalysisAsync(request.Symbol1, request.Symbol2, request.LookbackDays);
                
                return Ok(new
                {
                    success = true,
                    data = result,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing comprehensive analysis");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class PairTestRequest
    {
        public string? Symbol1 { get; set; }
        public string? Symbol2 { get; set; }
        public int LookbackDays { get; set; } = 252; // 1 year of trading days
        public int MaxLags { get; set; } = 5;
    }

    public class MultiPairTestRequest
    {
        public string[]? Symbols { get; set; }
        public int LookbackDays { get; set; } = 252;
    }
}
