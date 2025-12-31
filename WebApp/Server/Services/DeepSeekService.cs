namespace QuantResearchAgent.Services;

public class DeepSeekService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekService> _logger;

    public DeepSeekService(IHttpClientFactory httpClientFactory, ILogger<DeepSeekService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    public async Task<string> ChatAsync(string message)
    {
        try
        {
            _logger.LogInformation("Processing DeepSeek chat message");
            
            // TODO: Implement real DeepSeek API call
            await Task.Delay(100);
            return $"DeepSeek response to: {message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeepSeek chat");
            return "Error processing request";
        }
    }

    public async Task<string> GetChatCompletionAsync(string message)
    {
        return await ChatAsync(message);
    }
}
