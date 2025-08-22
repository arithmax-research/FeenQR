using System.Threading.Tasks;

namespace QuantResearchAgent.Services
{
    public class LLMRouterService : ILLMService
    {
        private readonly OpenAIService _openAIService;
        private readonly DeepSeekService _deepSeekService;

        public LLMRouterService(OpenAIService openAIService, DeepSeekService deepSeekService)
        {
            _openAIService = openAIService;
            _deepSeekService = deepSeekService;
        }

        // Default: use OpenAI for ILLMService interface
        public async Task<string> GetChatCompletionAsync(string prompt)
        {
            return await _openAIService.GetChatCompletionAsync(prompt);
        }

        // Advanced: allow explicit provider selection
        public async Task<string> GetChatCompletionAsync(string prompt, string provider)
        {
            if (provider.ToLower() == "deepseek")
                return await _deepSeekService.GetChatCompletionAsync(prompt);
            return await _openAIService.GetChatCompletionAsync(prompt);
        }
    }
}
