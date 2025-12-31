namespace QuantResearchAgent.Services;

public class OpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(IHttpClientFactory httpClientFactory, ILogger<OpenAIService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    public async Task<string> ChatAsync(string message)
    {
        try
        {
            _logger.LogInformation("Processing OpenAI chat message");
            
            // TODO: Implement real OpenAI API call
            await Task.Delay(100);
            return $"OpenAI response to: {message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OpenAI chat");
            return "Error processing request";
        }
    }

    public async Task<string> GetChatCompletionAsync(string message)
    {
        return await ChatAsync(message);
    }
}
