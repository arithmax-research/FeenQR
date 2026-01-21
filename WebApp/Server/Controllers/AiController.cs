using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace QuantResearchAgent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly ILogger<AiController> _logger;
        private readonly Kernel? _kernel;
        private readonly IConfiguration _configuration;

        public AiController(
            ILogger<AiController> logger,
            IConfiguration configuration,
            Kernel? kernel = null)
        {
            _logger = logger;
            _configuration = configuration;
            _kernel = kernel;
        }

        [HttpPost("analyze-stats")]
        public async Task<IActionResult> AnalyzeStatistics([FromBody] StatisticalAnalysisRequest request)
        {
            try
            {
                if (_kernel == null)
                {
                    return Ok(new { response = "AI analysis is not available. Please configure an API key in appsettings.json (OpenAI:ApiKey or DeepSeek:ApiKey)." });
                }

                var prompt = $@"You are an expert quantitative analyst and statistician. Analyze the following statistical test results and provide clear, actionable insights.

Symbol: {request.Symbol}
Analysis Type: {request.AnalysisType}

Statistical Results:
{request.Context}

User Question: {request.Query}

Provide a clear, professional analysis that:
1. Explains what the statistical results mean in plain English
2. Identifies the significance and practical implications
3. Highlights any important patterns or anomalies
4. Offers actionable insights for trading or investment decisions
5. Warns about any limitations or caveats

Keep your response concise (200-300 words), professional, and focused on what matters most to a quantitative trader or analyst.";

                var response = await _kernel.InvokePromptAsync(prompt);
                var result = response.ToString();

                return Ok(new { response = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI analysis");
                return Ok(new { response = $"I encountered an error analyzing the data: {ex.Message}. The statistical results are still valid - please review them directly." });
            }
        }

        public class StatisticalAnalysisRequest
        {
            public string Query { get; set; } = "";
            public string Context { get; set; } = "";
            public string Symbol { get; set; } = "";
            public string AnalysisType { get; set; } = "";
        }
    }
}
