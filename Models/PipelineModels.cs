using System;
using System.Collections.Generic;

namespace QuantResearchAgent.Models
{
    /// <summary>
    /// Represents a news article from the dual pipeline (NewsAPI + yfinance + newspaper3k)
    /// </summary>
    public class PipelineArticle
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty; // metadata snippet from API
        public string FullContent { get; set; } = string.Empty; // scraped body via newspaper3k
        public string Source { get; set; } = string.Empty; // NewsAPI, yfinance, etc.
        public string Provider { get; set; } = string.Empty; // CNBC, Bloomberg, Reuters, etc.
        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;
        public string Author { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsScraped { get; set; } = false; // true if FullContent was filled via newspaper3k
        public DateTime AddedDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Search result from the dual pipeline
    /// </summary>
    public class PipelineSearchResult
    {
        public string Ticker { get; set; } = string.Empty;
        public string SourceFilter { get; set; } = "All"; // "All", "Bloomberg", "Reuters", etc.
        public List<PipelineArticle> Articles { get; set; } = new();
        public int TotalFound { get; set; } = 0;
        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Configuration for the dual news pipeline
    /// </summary>
    public class NewsPipelineConfig
    {
        public string NewsApiKey { get; set; } = string.Empty;
        public int NewsApiPageSize { get; set; } = 15;
        public int YfinanceMaxResults { get; set; } = 10;
        public int ScraperTimeoutSeconds { get; set; } = 10;
        public int MaxConcurrentScrapes { get; set; } = 5;
        public int WordLimitPerArticle { get; set; } = 3000;
    }

    /// <summary>
    /// Article payload for sentiment analysis
    /// </summary>
    public class ArticleForAnalysis
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime PublishedDate { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// Batch sentiment request
    /// </summary>
    public class BatchSentimentRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public List<ArticleForAnalysis> Articles { get; set; } = new();
    }

    /// <summary>
    /// Sentiment result for an article
    /// </summary>
    public class ArticleSentimentResult
    {
        public string ArticleId { get; set; } = string.Empty;
        public string Sentiment { get; set; } = "Neutral"; // Positive, Neutral, Negative
        public double Score { get; set; } = 0.0; // -1.0 to 1.0
        public double Confidence { get; set; } = 0.0;
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Batch sentiment analysis result
    /// </summary>
    public class BatchSentimentResult
    {
        public string Symbol { get; set; } = string.Empty;
        public List<ArticleSentimentResult> ArticleResults { get; set; } = new();
        public string OverallSentiment { get; set; } = "Neutral";
        public double OverallScore { get; set; } = 0.0;
        public double Confidence { get; set; } = 0.0;
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }
}
