using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly DeepSeekService _deepSeekService;
        private readonly OpenAIService _openAIService;

        public ChatController(
            ILogger<ChatController> logger,
            DeepSeekService deepSeekService,
            OpenAIService openAIService)
        {
            _logger = logger;
            _deepSeekService = deepSeekService;
            _openAIService = openAIService;
        }

        [HttpPost("deepseek")]
        public async Task<IActionResult> ChatWithDeepSeek([FromBody] ChatRequest request)
        {
            try
            {
                var result = await _deepSeekService.GetChatCompletionAsync(request.Message);
                var response = new ChatResponse
                {
                    Message = result,
                    Timestamp = DateTime.UtcNow
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeepSeek chat");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("openai")]
        public async Task<IActionResult> ChatWithOpenAI([FromBody] ChatRequest request)
        {
            try
            {
                var result = await _openAIService.GetChatCompletionAsync(request.Message);
                var response = new ChatResponse
                {
                    Message = result,
                    Timestamp = DateTime.UtcNow
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OpenAI chat");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
