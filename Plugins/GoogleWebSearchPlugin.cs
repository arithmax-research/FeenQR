using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Linq;

namespace QuantResearchAgent.Plugins
{
    /// <summary>
    /// Google Custom Search API implementation of IWebSearchPlugin
    /// </summary>
    public class GoogleWebSearchPlugin : IWebSearchPlugin
    {
        private readonly string _apiKey;
        private readonly string _searchEngineId;
        private readonly HttpClient _httpClient;

        public GoogleWebSearchPlugin(IConfiguration config, HttpClient httpClient)
        {
            _apiKey = config["GoogleSearch:ApiKey"] ?? string.Empty;
            _searchEngineId = config["GoogleSearch:SearchEngineId"] ?? string.Empty;
            _httpClient = httpClient;
        }

        public async Task<List<WebSearchResult>> SearchAsync(string query, int maxResults = 5)
        {
            var results = new List<WebSearchResult>();
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_searchEngineId))
                return results;

            var url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_searchEngineId}&q={System.Net.WebUtility.UrlEncode(query)}&num={maxResults}";
            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    results.Add(new WebSearchResult
                    {
                        Title = item.GetProperty("title").GetString() ?? string.Empty,
                        Snippet = item.GetProperty("snippet").GetString() ?? string.Empty,
                        Url = item.GetProperty("link").GetString() ?? string.Empty
                    });
                }
            }
            return results;
        }
    }
}
