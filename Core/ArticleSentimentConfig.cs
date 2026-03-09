namespace QuantResearchAgent.Core;

/// <summary>
/// Configuration for article scraping service
/// </summary>
public class ArticleScraperConfig
{
    public int MaxConcurrency { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 10;
    public int RateLimitPerSecond { get; set; } = 1;
    public string UserAgent { get; set; } = "QuantResearchAgent/1.0";
}

/// <summary>
/// Configuration for article chunking
/// </summary>
public class ChunkingConfig
{
    public int MinChunkSize { get; set; } = 500;
    public int MaxChunkSize { get; set; } = 1500;
    public int OverlapSize { get; set; } = 100;
    public int MaxChunksPerArticle { get; set; } = 50;
}

/// <summary>
/// Configuration for Qdrant vector database
/// </summary>
public class QdrantConfig
{
    public string Endpoint { get; set; } = "http://localhost:6333";
    public string CollectionName { get; set; } = "article_chunks";
    public int VectorSize { get; set; } = 1536;
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// Configuration for OpenAI embedding service
/// </summary>
public class EmbeddingConfig
{
    public string ModelName { get; set; } = "text-embedding-3-small";
    public int BatchSize { get; set; } = 50;
    public int CacheTTLDays { get; set; } = 7;
}

/// <summary>
/// Configuration for NewsAPI integration
/// </summary>
public class NewsApiConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://newsapi.org/v2";
    public int DefaultPageSize { get; set; } = 20;
    public string Language { get; set; } = "en";
}
