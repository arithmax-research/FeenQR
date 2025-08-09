using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace QuantResearchAgent.Plugins
{
    /// <summary>
    /// Google Custom Search API implementation for web search functionality
    /// </summary>
    public class GoogleWebSearchPlugin : IWebSearchPlugin
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleWebSearchPlugin> _logger;
        private readonly string? _apiKey;
        private readonly string? _searchEngineId;
        private const string GoogleSearchApiUrl = "https://www.googleapis.com/customsearch/v1";

        public GoogleWebSearchPlugin(HttpClient httpClient, ILogger<GoogleWebSearchPlugin> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["GoogleSearch:ApiKey"];
            _searchEngineId = configuration["GoogleSearch:SearchEngineId"];
        }

        public async Task<List<WebSearchResult>> SearchAsync(string query, int maxResults = 5)
        {
            var results = new List<WebSearchResult>();

            try
            {
                if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_searchEngineId))
                {
                    _logger.LogError("Google Search API key or Search Engine ID not configured. Returning empty results.");
                    return results;
                }

                var requestUrl = $"{GoogleSearchApiUrl}?key={_apiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}&num={Math.Min(maxResults, 10)}";

                _logger.LogInformation($"Searching Google for: {query}");

                // Add delay to allow rate limits to reset from previous requests
                await Task.Delay(2000); // Reduced from 10 seconds to 2 seconds

                // Implement retry logic for rate limiting
                GoogleSearchResponse? searchResponse = null;
                bool success = false;

                for (int attempt = 0; attempt < 3 && !success; attempt++)
                {
                    try
                    {
                        var response = await _httpClient.GetStringAsync(requestUrl);
                        _logger.LogInformation($"Raw API response length: {response.Length}");
                        
                        searchResponse = JsonSerializer.Deserialize<GoogleSearchResponse>(response);
                        _logger.LogInformation($"Parsed response - Items count: {searchResponse?.Items?.Length ?? 0}");
                        
                        success = true; // Mark as successful
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                    {
                        // Rate limiting - wait and retry
                        var waitTime = (attempt + 1) * 30000; // 30, 60, 90 seconds
                        _logger.LogWarning($"Rate limited on attempt {attempt + 1}, waiting {waitTime}ms before retry");
                        
                        if (attempt < 2) // Not the last attempt
                        {
                            await Task.Delay(waitTime);
                        }
                        else
                        {
                            _logger.LogError("Max retry attempts reached due to rate limiting");
                            throw;
                        }
                    }
                    catch (Exception ex) when ((ex.Data.Contains("StatusCode") && ex.Data["StatusCode"]?.ToString() == "429") || 
                                               ex.Message.Contains("429") || 
                                               ex.Message.Contains("Too Many Requests"))
                    {
                        // Alternative rate limiting detection
                        var waitTime = (attempt + 1) * 30000; // 30, 60, 90 seconds
                        _logger.LogWarning($"Rate limited on attempt {attempt + 1}, waiting {waitTime}ms before retry. Error: {ex.Message}");
                        
                        if (attempt < 2) // Not the last attempt
                        {
                            await Task.Delay(waitTime);
                        }
                        else
                        {
                            _logger.LogError("Max retry attempts reached due to rate limiting");
                            throw;
                        }
                    }
                }

                // Process the results after successful API call
                if (searchResponse?.Items != null)
                {
                    foreach (var item in searchResponse.Items)
                    {
                        var result = new WebSearchResult
                        {
                            Title = item.Title ?? "No Title",
                            Url = item.Link ?? "No URL",
                            Snippet = item.Snippet ?? "No Description"
                        };
                        
                        results.Add(result);
                    }
                }

                _logger.LogInformation($"Found {results.Count} search results");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error occurred while searching Google");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing Google search response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while searching Google");
            }

            return results;
        }
    }

    // Google Custom Search API response models
    internal class GoogleSearchResponse
    {
        [JsonPropertyName("items")]
        public GoogleSearchItem[]? Items { get; set; }
    }

    internal class GoogleSearchItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        
        [JsonPropertyName("link")]
        public string? Link { get; set; }
        
        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }
    }
}
