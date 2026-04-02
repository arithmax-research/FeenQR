using System.Collections.Generic;

namespace QuantResearchAgent.Services;

public class NewsApiSourceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class NewsApiMarketPulseTab
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string EmptyMessage { get; set; } = string.Empty;
    public List<NewsItem> Articles { get; set; } = new();
}

public class NewsApiMarketPulseResponse
{
    public string Symbol { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? SelectedSourceId { get; set; }
    public string? SelectedSourceName { get; set; }
    public string Domains { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<NewsApiSourceInfo> Sources { get; set; } = new();
    public List<NewsApiMarketPulseTab> Tabs { get; set; } = new();
}