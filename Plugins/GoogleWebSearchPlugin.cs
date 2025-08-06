using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
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
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Starting search for: {query}");
                
                if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_searchEngineId))
                {
                    Console.WriteLine("DEBUG GoogleWebSearchPlugin: API key or Search Engine ID not configured");
                    _logger.LogWarning("Google Search API key or Search Engine ID not configured. Returning empty results.");
                    return results;
                }

                var requestUrl = $"{GoogleSearchApiUrl}?key={_apiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}&num={Math.Min(maxResults, 10)}";

                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Making request to: {requestUrl.Substring(0, 100)}...");
                _logger.LogInformation($"Searching Google for: {query}");
                _logger.LogDebug($"Request URL: {requestUrl}");

                var response = await _httpClient.GetStringAsync(requestUrl);
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Received response, length: {response.Length}");
                _logger.LogDebug($"Google API response: {response.Substring(0, Math.Min(500, response.Length))}...");
                var searchResponse = JsonSerializer.Deserialize<GoogleSearchResponse>(response);

                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Deserialized response, items count: {searchResponse?.Items?.Length ?? 0}");

                if (searchResponse?.Items != null)
                {
                    Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Processing {searchResponse.Items.Length} items");
                    foreach (var item in searchResponse.Items)
                    {
                        Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Adding result - Title: {item.Title?.Substring(0, Math.Min(50, item.Title?.Length ?? 0))}...");
                        results.Add(new WebSearchResult
                        {
                            Title = item.Title ?? string.Empty,
                            Snippet = item.Snippet ?? string.Empty,
                            Url = item.Link ?? string.Empty
                        });
                    }
                }

                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Final results count: {results.Count}");
                _logger.LogInformation($"Found {results.Count} search results");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: HTTP error - {ex.Message}");
                _logger.LogError(ex, "HTTP error occurred while searching Google");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: JSON parsing error - {ex.Message}");
                _logger.LogError(ex, "Error parsing Google search response");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Unexpected error - {ex.Message}");
                _logger.LogError(ex, "Unexpected error occurred while searching Google");
            }

            return results;
        }
    }

    // Google Custom Search API response models
    internal class GoogleSearchResponse
    {
        public GoogleSearchItem[]? Items { get; set; }
    }

    internal class GoogleSearchItem
    {
        public string? Title { get; set; }
        public string? Link { get; set; }
        public string? Snippet { get; set; }
    }
}
