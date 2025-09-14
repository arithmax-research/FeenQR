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
    public class DeepSeekService
    {
        private readonly string _apiKey;
        private readonly string _modelId;
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;
        private readonly ILogger<DeepSeekService> _logger;

    public DeepSeekService(IConfiguration configuration, HttpClient httpClient, ILogger<DeepSeekService> logger)
        {
            _apiKey = configuration["DeepSeek:ApiKey"] ?? throw new ArgumentException("DeepSeek API key not configured");
            _modelId = configuration["DeepSeek:ModelId"] ?? "deepseek-chat";
            _baseUrl = configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com/v1/chat/completions";
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
                    _logger?.LogError($"DeepSeek API error: {response.StatusCode} - {responseJson}");
                    throw new Exception($"DeepSeek API error: {response.StatusCode} - {responseJson}");
                }

                try
                {
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
                    _logger?.LogError(ex, $"Failed to parse DeepSeek response: {responseJson}");
                    throw new Exception($"Failed to parse DeepSeek response: {responseJson}", ex);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DeepSeekService.GetChatCompletionAsync failed");
                throw;
            }
        }
    }
}
