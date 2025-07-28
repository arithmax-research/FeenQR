using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using QuantResearchAgent.Services.ResearchAgents;

namespace QuantResearchAgent.Plugins
{
    public class MarketSentimentNewsPlugin
    {
        private readonly HttpClient _httpClient;
        public MarketSentimentNewsPlugin(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<List<MarketNewsItem>> GetNewsAsync(string ticker, int maxResults = 10)
        {
            var url = $"http://localhost:5001/news?ticker={ticker}";
            var response = await _httpClient.GetStringAsync(url);
            var newsList = JsonSerializer.Deserialize<List<MarketNewsItem>>(response) ?? new List<MarketNewsItem>();
            return newsList.Count > maxResults ? newsList.GetRange(0, maxResults) : newsList;
        }
    }

    public class MarketNewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public DateTime ProviderPublishTime { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Thumbnail { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
