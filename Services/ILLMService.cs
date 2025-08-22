using System.Threading.Tasks;

namespace QuantResearchAgent.Services
{
    public interface ILLMService
    {
        Task<string> GetChatCompletionAsync(string prompt);
    }
}
