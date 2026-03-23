using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace QuantResearchAgent.Services;

public class DataRequirementAdvisor
{
    private readonly ILogger<DataRequirementAdvisor> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public DataRequirementAdvisor(ILogger<DataRequirementAdvisor> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = configuration["DeepSeek:ApiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? throw new InvalidOperationException("DEEPSEEK_API_KEY not set");
        _model = configuration["DeepSeek:ModelId"] ?? "deepseek-chat";
    }

    public async Task<List<string>> AnalyzeDataRequirementsAsync(string strategyText)
    {
        try
        {
            var prompt = $@"
Analyze this trading strategy description and determine what data types are ABSOLUTELY REQUIRED for QuantConnect LEAN backtesting.

Strategy: {strategyText}

IMPORTANT: Only include data types that are explicitly mentioned in the strategy or clearly required for the core trading logic. Do not include data for performance metrics, benchmarks, or general analysis unless the strategy specifically requires it.

Common types include:
- 'equity' for US stock/ETF data (required for most equity strategies)
- 'crypto' for cryptocurrency data
- 'forex' for foreign exchange data
- 'commodity' for commodity/futures data
- 'interest-rate' for risk-free rate data (only if strategy uses it for calculations)
- 'alternative' for alternative data sources

For simple technical indicator strategies on equities, typically only 'equity' is needed.

Return a JSON array of data types required. Only return the JSON array, no other text.
Example: [""equity""]
";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 200
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/v1/chat/completions")
            {
                Headers = { { "Authorization", $"Bearer {_apiKey}" } },
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonDocument.Parse(responseContent);
            var content = jsonResponse.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Empty response from DeepSeek for data requirements");
                return new List<string> { "equity" }; // fallback
            }

            // Try to parse as JSON array
            try
            {
                var dataTypes = JsonSerializer.Deserialize<List<string>>(content.Trim());
                _logger.LogInformation("DeepSeek identified data requirements: {Types}", string.Join(", ", dataTypes ?? new List<string>()));
                return dataTypes ?? new List<string> { "equity" };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse DeepSeek response as JSON: {Content}", content);
                return new List<string> { "equity" }; // fallback
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing data requirements with DeepSeek");
            return new List<string> { "equity" }; // fallback to basic equity data
        }
    }
}