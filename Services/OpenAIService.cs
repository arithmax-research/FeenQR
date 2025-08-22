using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services
{
    public class OpenAIService : ILLMService
    {
        private readonly string _apiKey;
        private readonly string _modelId;
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAIService> _logger;

        public OpenAIService(IConfiguration configuration, HttpClient httpClient, ILogger<OpenAIService> logger)
        {
            _apiKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentException("OpenAI API key not configured");
            _modelId = configuration["OpenAI:ModelId"] ?? "gpt-4o";
            _baseUrl = configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1/chat/completions";
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GetChatCompletionAsync(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    model = _modelId,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogError($"OpenAI API error: {response.StatusCode} - {responseJson}");
                    throw new Exception($"OpenAI API error: {response.StatusCode} - {responseJson}");
                }

                using var doc = JsonDocument.Parse(responseJson);
                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
                return content ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OpenAIService.GetChatCompletionAsync failed");
                throw;
            }
        }
    }
}
