using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Linq;

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
        private readonly bool _enabled;
        private readonly bool _useOfflineFallback;
        private const string GoogleSearchApiUrl = "https://www.googleapis.com/customsearch/v1";

        public GoogleWebSearchPlugin(HttpClient httpClient, ILogger<GoogleWebSearchPlugin> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["GoogleSearch:ApiKey"];
            _searchEngineId = configuration["GoogleSearch:SearchEngineId"];
            _enabled = configuration.GetValue<bool>("GoogleSearch:Enabled", true);
            _useOfflineFallback = configuration.GetValue<bool>("GoogleSearch:UseOfflineFallback", true);
            
            // Set timeout for HTTP requests to prevent hanging
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<WebSearchResult>> SearchAsync(string query, int maxResults = 5)
        {
            var results = new List<WebSearchResult>();

            try
            {
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Starting search for: {query}");
                
                // Check if websearch is disabled
                if (!_enabled)
                {
                    Console.WriteLine("DEBUG GoogleWebSearchPlugin: Web search is disabled in configuration");
                    _logger.LogInformation("Google Search is disabled in configuration. Use 'GoogleSearch:Enabled': true to enable.");
                    
                    if (_useOfflineFallback)
                    {
                        return await FallbackSearchAsync(query, maxResults);
                    }
                    return results;
                }
                
                if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_searchEngineId))
                {
                    Console.WriteLine("DEBUG GoogleWebSearchPlugin: API key or Search Engine ID not configured");
                    _logger.LogWarning("Google Search API key or Search Engine ID not configured.");
                    
                    if (_useOfflineFallback)
                    {
                        Console.WriteLine("DEBUG GoogleWebSearchPlugin: Using offline fallback due to missing configuration");
                        return await FallbackSearchAsync(query, maxResults);
                    }
                    return results;
                }

                // First, test network connectivity to Google APIs
                if (!await TestNetworkConnectivityAsync())
                {
                    Console.WriteLine("DEBUG GoogleWebSearchPlugin: Network connectivity test failed");
                    _logger.LogWarning("Network connectivity to Google APIs failed.");
                    
                    if (_useOfflineFallback)
                    {
                        Console.WriteLine("DEBUG GoogleWebSearchPlugin: Using offline fallback due to network issues");
                        return await FallbackSearchAsync(query, maxResults);
                    }
                    return results;
                }

                var requestUrl = $"{GoogleSearchApiUrl}?key={_apiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}&num={Math.Min(maxResults, 10)}";

                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Making request to: {requestUrl.Substring(0, 100)}...");
                _logger.LogInformation($"Searching Google for: {query}");
                _logger.LogDebug($"Request URL: {requestUrl}");

                var response = await _httpClient.GetStringAsync(requestUrl);
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Received response, length: {response.Length}");
                _logger.LogDebug($"Google API response: {response.Substring(0, Math.Min(500, response.Length))}...");
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var searchResponse = JsonSerializer.Deserialize<GoogleSearchResponse>(response, options);

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
            catch (HttpRequestException ex) when (ex.Message.Contains("Name or service not known") || ex.Message.Contains("Connection refused"))
            {
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Network connectivity error - {ex.Message}");
                _logger.LogWarning($"Network connectivity error occurred while searching Google: {ex.Message}");
                
                if (_useOfflineFallback)
                {
                    return await FallbackSearchAsync(query, maxResults);
                }
                return results;
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Request timeout - {ex.Message}");
                _logger.LogWarning($"Google search request timed out: {ex.Message}");
                
                if (_useOfflineFallback)
                {
                    return await FallbackSearchAsync(query, maxResults);
                }
                return results;
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

        /// <summary>
        /// Test network connectivity to Google APIs
        /// </summary>
        private async Task<bool> TestNetworkConnectivityAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync("https://www.googleapis.com/");
                return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound; // 404 is OK, means we can reach Google
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Network connectivity test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fallback search mechanism when Google API is unavailable
        /// </summary>
        private Task<List<WebSearchResult>> FallbackSearchAsync(string query, int maxResults)
        {
            Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Using fallback search for: {query}");
            _logger.LogInformation($"Using fallback search mechanism for query: {query}");

            var results = new List<WebSearchResult>();
            
            // Extract the symbol from the query if possible
            var symbol = ExtractSymbolFromQuery(query);
            
            if (!string.IsNullOrEmpty(symbol))
            {
                // Adjust maxResults to avoid duplication
                var fallbackResults = GenerateFallbackFinancialResults(symbol, query);
                results.AddRange(fallbackResults.Take(Math.Min(maxResults, fallbackResults.Count)));
                
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Generated {results.Count} fallback results");
                _logger.LogInformation($"Generated {results.Count} fallback search results for {symbol}");
            }
            else
            {
                // Generate generic financial research suggestions
                results.AddRange(GenerateGenericFallbackResults(query).Take(maxResults));
                Console.WriteLine($"DEBUG GoogleWebSearchPlugin: Generated {results.Count} generic fallback results");
            }

            return Task.FromResult(results);
        }

        /// <summary>
        /// Extract stock symbol from search query
        /// </summary>
        private string ExtractSymbolFromQuery(string query)
        {
            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.FirstOrDefault(p => p.Length <= 5 && p.All(char.IsLetter))?.ToUpper() ?? string.Empty;
        }

        /// <summary>
        /// Generate curated financial news results for a specific symbol
        /// </summary>
        private List<WebSearchResult> GenerateFallbackFinancialResults(string symbol, string originalQuery)
        {
            var currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            
            return new List<WebSearchResult>
            {
                new WebSearchResult
                {
                    Title = $"{symbol} Stock Analysis and Financial Performance - Yahoo Finance",
                    Snippet = $"Get the latest financial data, earnings reports, and stock analysis for {symbol}. Real-time quotes, charts, and financial news.",
                    Url = $"https://finance.yahoo.com/quote/{symbol}"
                },
                new WebSearchResult
                {
                    Title = $"{symbol} Company Profile and Stock Quote - MarketWatch",
                    Snippet = $"View {symbol} stock price, news, earnings, fundamentals, and analyst recommendations. Latest market data and financial metrics.",
                    Url = $"https://www.marketwatch.com/investing/stock/{symbol.ToLower()}"
                },
                new WebSearchResult
                {
                    Title = $"{symbol} Stock News and Analysis - Bloomberg",
                    Snippet = $"Breaking news, analysis and opinion articles about {symbol} stock. Market trends, earnings updates, and investment insights.",
                    Url = $"https://www.bloomberg.com/quote/{symbol}:US"
                },
                new WebSearchResult
                {
                    Title = $"{symbol} SEC Filings and Financial Reports - SEC.gov",
                    Snippet = $"Official SEC filings for {symbol} including 10-K annual reports, 10-Q quarterly reports, and 8-K current reports.",
                    Url = $"https://www.sec.gov/edgar/search/#/q={symbol}&dateRange=custom"
                },
                new WebSearchResult
                {
                    Title = $"{symbol} Stock Research and Analyst Reports - Seeking Alpha",
                    Snippet = $"In-depth analysis, earnings estimates, and investment thesis for {symbol}. Community insights and professional research.",
                    Url = $"https://seekingalpha.com/symbol/{symbol}"
                }
            };
        }

        /// <summary>
        /// Generate generic financial research results
        /// </summary>
        private List<WebSearchResult> GenerateGenericFallbackResults(string query)
        {
            return new List<WebSearchResult>
            {
                new WebSearchResult
                {
                    Title = "Financial News and Market Analysis - Yahoo Finance",
                    Snippet = "Stay informed with the latest financial news, market trends, and investment insights. Real-time data and expert analysis.",
                    Url = "https://finance.yahoo.com/"
                },
                new WebSearchResult
                {
                    Title = "Stock Market News and Analysis - MarketWatch",
                    Snippet = "Breaking news and analysis on stocks, economics, and markets. Investment tools and portfolio tracking.",
                    Url = "https://www.marketwatch.com/"
                },
                new WebSearchResult
                {
                    Title = "Business News and Financial Markets - Bloomberg",
                    Snippet = "Global business and financial news, stock quotes, and market data. Professional investment insights.",
                    Url = "https://www.bloomberg.com/"
                }
            };
        }
    }

    // Google Custom Search API response models with proper JSON property mapping
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
