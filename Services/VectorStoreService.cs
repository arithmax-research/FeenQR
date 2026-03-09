using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Service for storing and retrieving article chunks in Qdrant vector database
    /// Provides in-memory fallback when Qdrant is unavailable
    /// </summary>
    public class VectorStoreService
    {
        private readonly ILogger<VectorStoreService> _logger;
        private readonly string _qdrantEndpoint;
        private readonly string _collectionName;
        private readonly int _vectorSize;
        private readonly int _batchSize;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, (ArticleChunk chunk, float[] embedding)> _memoryFallback;
        private bool _qdrantAvailable;

        public VectorStoreService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<VectorStoreService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration
            var config = new QdrantConfig();
            configuration.GetSection("Qdrant").Bind(config);

            _qdrantEndpoint = config.Endpoint;
            _collectionName = config.CollectionName;
            _vectorSize = config.VectorSize;
            _batchSize = config.BatchSize;

            _memoryFallback = new Dictionary<string, (ArticleChunk, float[])>();
            _qdrantAvailable = false;

            _logger.LogInformation(
                "VectorStoreService initialized - Endpoint: {Endpoint}, Collection: {Collection}, VectorSize: {VectorSize}",
                _qdrantEndpoint, _collectionName, _vectorSize);
        }

        /// <summary>
        /// Initialize Qdrant connection and create collection if needed
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing Qdrant connection...");

                // Check if Qdrant is available
                var healthCheckUrl = $"{_qdrantEndpoint}/";
                var healthResponse = await _httpClient.GetAsync(healthCheckUrl);

                if (!healthResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Qdrant health check failed: {StatusCode}. Falling back to in-memory storage.",
                        healthResponse.StatusCode);
                    _qdrantAvailable = false;
                    return;
                }

                // Check if collection exists
                var collectionUrl = $"{_qdrantEndpoint}/collections/{_collectionName}";
                var collectionResponse = await _httpClient.GetAsync(collectionUrl);

                if (collectionResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Collection '{Collection}' already exists", _collectionName);
                    _qdrantAvailable = true;
                    return;
                }

                // Create collection if it doesn't exist
                await CreateCollectionAsync();
                _qdrantAvailable = true;

                _logger.LogInformation("Qdrant initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Qdrant. Falling back to in-memory storage.");
                _qdrantAvailable = false;
            }
        }

        /// <summary>
        /// Create Qdrant collection with specified vector size
        /// </summary>
        private async Task CreateCollectionAsync()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var createCollectionUrl = $"{_qdrantEndpoint}/collections/{_collectionName}";

                var requestBody = new
                {
                    vectors = new
                    {
                        size = _vectorSize,
                        distance = "Cosine"
                    }
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(createCollectionUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to create collection: {response.StatusCode} - {responseJson}");
                }

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Created collection '{Collection}' with {VectorSize} dimensions in {Duration}ms",
                    _collectionName, _vectorSize, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Qdrant collection '{Collection}'", _collectionName);
                throw;
            }
        }

        /// <summary>
        /// Store a single chunk with its embedding
        /// </summary>
        /// <param name="chunk">Article chunk to store</param>
        /// <param name="embedding">Vector embedding for the chunk</param>
        /// <returns>True if stored successfully</returns>
        public async Task<bool> StoreChunkAsync(ArticleChunk chunk, float[] embedding)
        {
            if (chunk == null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            if (embedding == null || embedding.Length != _vectorSize)
            {
                throw new ArgumentException($"Embedding must be {_vectorSize} dimensions", nameof(embedding));
            }

            var startTime = DateTime.UtcNow;

            try
            {
                if (!_qdrantAvailable)
                {
                    StoreInMemory(chunk, embedding);
                    return true;
                }

                var upsertUrl = $"{_qdrantEndpoint}/collections/{_collectionName}/points";

                var point = new
                {
                    points = new[]
                    {
                        new
                        {
                            id = chunk.ChunkId,
                            vector = embedding,
                            payload = new
                            {
                                article_url = chunk.ArticleUrl,
                                title = chunk.Title,
                                publisher = chunk.Publisher,
                                published_date = chunk.PublishedDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                source = chunk.Source,
                                chunk_index = chunk.ChunkIndex,
                                content = chunk.Content,
                                content_length = chunk.ContentLength
                            }
                        }
                    }
                };

                var requestJson = JsonSerializer.Serialize(point);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(upsertUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to store chunk in Qdrant: {StatusCode} - {Response}",
                        response.StatusCode, responseJson);
                    
                    // Fallback to in-memory storage
                    StoreInMemory(chunk, embedding);
                    return false;
                }

                var duration = DateTime.UtcNow - startTime;
                _logger.LogDebug(
                    "Stored chunk {ChunkId} in Qdrant in {Duration}ms",
                    chunk.ChunkId, duration.TotalMilliseconds);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing chunk {ChunkId} in Qdrant", chunk.ChunkId);
                
                // Fallback to in-memory storage
                StoreInMemory(chunk, embedding);
                return false;
            }
        }

        /// <summary>
        /// Store multiple chunks with embeddings in batch (up to 100 chunks per operation)
        /// </summary>
        /// <param name="items">List of chunk-embedding pairs to store</param>
        /// <returns>Number of chunks successfully stored</returns>
        public async Task<int> StoreChunksAsync(List<(ArticleChunk chunk, float[] embedding)> items)
        {
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            // Validate embeddings
            foreach (var (chunk, embedding) in items)
            {
                if (embedding == null || embedding.Length != _vectorSize)
                {
                    throw new ArgumentException($"All embeddings must be {_vectorSize} dimensions");
                }
            }

            var startTime = DateTime.UtcNow;
            var totalStored = 0;

            try
            {
                // Process in batches
                var batches = items
                    .Select((item, index) => new { item, index })
                    .GroupBy(x => x.index / _batchSize)
                    .Select(g => g.Select(x => x.item).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    if (!_qdrantAvailable)
                    {
                        // Store in memory fallback
                        foreach (var (chunk, embedding) in batch)
                        {
                            StoreInMemory(chunk, embedding);
                        }
                        totalStored += batch.Count;
                        continue;
                    }

                    try
                    {
                        var upsertUrl = $"{_qdrantEndpoint}/collections/{_collectionName}/points";

                        var points = batch.Select(item => new
                        {
                            id = item.chunk.ChunkId,
                            vector = item.embedding,
                            payload = new
                            {
                                article_url = item.chunk.ArticleUrl,
                                title = item.chunk.Title,
                                publisher = item.chunk.Publisher,
                                published_date = item.chunk.PublishedDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                source = item.chunk.Source,
                                chunk_index = item.chunk.ChunkIndex,
                                content = item.chunk.Content,
                                content_length = item.chunk.ContentLength
                            }
                        }).ToArray();

                        var requestBody = new { points };
                        var requestJson = JsonSerializer.Serialize(requestBody);
                        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                        var response = await _httpClient.PutAsync(upsertUrl, content);
                        var responseJson = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError(
                                "Failed to store batch in Qdrant: {StatusCode} - {Response}",
                                response.StatusCode, responseJson);
                            
                            // Fallback to in-memory storage for this batch
                            foreach (var (chunk, embedding) in batch)
                            {
                                StoreInMemory(chunk, embedding);
                            }
                        }
                        else
                        {
                            totalStored += batch.Count;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error storing batch in Qdrant");
                        
                        // Fallback to in-memory storage for this batch
                        foreach (var (chunk, embedding) in batch)
                        {
                            StoreInMemory(chunk, embedding);
                        }
                    }
                }

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Stored {Count} chunks in {Duration}ms (batch operation, {Batches} batches)",
                    totalStored, duration.TotalMilliseconds, batches.Count);

                return totalStored;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch storage operation");
                return totalStored;
            }
        }

        /// <summary>
        /// Check if an article already exists in the vector store (deduplication)
        /// </summary>
        /// <param name="articleUrl">Article URL to check</param>
        /// <returns>True if article exists</returns>
        public async Task<bool> ArticleExistsAsync(string articleUrl)
        {
            if (string.IsNullOrWhiteSpace(articleUrl))
            {
                throw new ArgumentException("Article URL cannot be null or empty", nameof(articleUrl));
            }

            var startTime = DateTime.UtcNow;

            try
            {
                if (!_qdrantAvailable)
                {
                    // Check in-memory fallback
                    return _memoryFallback.Values.Any(item => item.chunk.ArticleUrl == articleUrl);
                }

                // Query Qdrant for any chunks with this article URL
                var scrollUrl = $"{_qdrantEndpoint}/collections/{_collectionName}/points/scroll";

                var requestBody = new
                {
                    filter = new
                    {
                        must = new[]
                        {
                            new
                            {
                                key = "article_url",
                                match = new { value = articleUrl }
                            }
                        }
                    },
                    limit = 1,
                    with_payload = false,
                    with_vector = false
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(scrollUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to check article existence in Qdrant: {StatusCode} - {Response}",
                        response.StatusCode, responseJson);
                    return false;
                }

                using var doc = JsonDocument.Parse(responseJson);
                var result = doc.RootElement.GetProperty("result");
                var points = result.GetProperty("points");

                var exists = points.GetArrayLength() > 0;

                var duration = DateTime.UtcNow - startTime;
                _logger.LogDebug(
                    "Checked article existence for {Url}: {Exists} in {Duration}ms",
                    articleUrl, exists, duration.TotalMilliseconds);

                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking article existence for {Url}", articleUrl);
                return false;
            }
        }

        /// <summary>
        /// Semantic search with filters for date range, source, and symbol
        /// </summary>
        /// <param name="queryEmbedding">Query vector embedding</param>
        /// <param name="topK">Number of top results to return</param>
        /// <param name="filter">Optional filter criteria</param>
        /// <param name="offset">Pagination offset (default: 0)</param>
        /// <returns>List of search results with similarity scores</returns>
        public async Task<List<ChunkSearchResult>> SearchAsync(
            float[] queryEmbedding,
            int topK = 10,
            ChunkFilter? filter = null,
            int offset = 0)
        {
            if (queryEmbedding == null || queryEmbedding.Length != _vectorSize)
            {
                throw new ArgumentException($"Query embedding must be {_vectorSize} dimensions", nameof(queryEmbedding));
            }

            if (topK <= 0)
            {
                throw new ArgumentException("topK must be greater than 0", nameof(topK));
            }

            var startTime = DateTime.UtcNow;

            try
            {
                if (!_qdrantAvailable)
                {
                    // Search in-memory fallback
                    return SearchInMemory(queryEmbedding, topK, filter, offset);
                }

                var searchUrl = $"{_qdrantEndpoint}/collections/{_collectionName}/points/search";

                // Build filter conditions
                var filterConditions = new List<object>();

                if (filter != null)
                {
                    if (filter.StartDate.HasValue)
                    {
                        filterConditions.Add(new
                        {
                            key = "published_date",
                            range = new
                            {
                                gte = filter.StartDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                            }
                        });
                    }

                    if (filter.EndDate.HasValue)
                    {
                        filterConditions.Add(new
                        {
                            key = "published_date",
                            range = new
                            {
                                lte = filter.EndDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                            }
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(filter.Source))
                    {
                        filterConditions.Add(new
                        {
                            key = "source",
                            match = new { value = filter.Source }
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(filter.Symbol))
                    {
                        filterConditions.Add(new
                        {
                            key = "symbol",
                            match = new { value = filter.Symbol }
                        });
                    }
                }

                // Build request body
                var requestBody = new Dictionary<string, object>
                {
                    ["vector"] = queryEmbedding,
                    ["limit"] = topK,
                    ["offset"] = offset,
                    ["with_payload"] = true,
                    ["with_vector"] = false
                };

                if (filterConditions.Count > 0)
                {
                    requestBody["filter"] = new { must = filterConditions };
                }

                var requestJson = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(searchUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to search in Qdrant: {StatusCode} - {Response}",
                        response.StatusCode, responseJson);
                    return new List<ChunkSearchResult>();
                }

                using var doc = JsonDocument.Parse(responseJson);
                var result = doc.RootElement.GetProperty("result");

                var searchResults = new List<ChunkSearchResult>();

                foreach (var point in result.EnumerateArray())
                {
                    var payload = point.GetProperty("payload");
                    var score = point.GetProperty("score").GetSingle();

                    var chunk = new ArticleChunk
                    {
                        ChunkId = point.GetProperty("id").GetString() ?? string.Empty,
                        ArticleUrl = payload.GetProperty("article_url").GetString() ?? string.Empty,
                        Title = payload.GetProperty("title").GetString() ?? string.Empty,
                        Publisher = payload.GetProperty("publisher").GetString() ?? string.Empty,
                        PublishedDate = DateTime.Parse(payload.GetProperty("published_date").GetString() ?? DateTime.UtcNow.ToString()),
                        Source = payload.GetProperty("source").GetString() ?? string.Empty,
                        ChunkIndex = payload.GetProperty("chunk_index").GetInt32(),
                        Content = payload.GetProperty("content").GetString() ?? string.Empty,
                        ContentLength = payload.GetProperty("content_length").GetInt32()
                    };

                    searchResults.Add(new ChunkSearchResult
                    {
                        Chunk = chunk,
                        SimilarityScore = score
                    });
                }

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Semantic search returned {Count} results in {Duration}ms (topK: {TopK}, offset: {Offset})",
                    searchResults.Count, duration.TotalMilliseconds, topK, offset);

                return searchResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing semantic search");
                return new List<ChunkSearchResult>();
            }
        }

        /// <summary>
        /// Retrieve all chunks for a specific article URL
        /// </summary>
        /// <param name="articleUrl">Article URL to retrieve chunks for</param>
        /// <param name="pageSize">Number of chunks per page (default: 100)</param>
        /// <param name="offset">Pagination offset (default: 0)</param>
        /// <returns>List of article chunks ordered by chunk index</returns>
        public async Task<List<ArticleChunk>> GetArticleChunksAsync(
            string articleUrl,
            int pageSize = 100,
            int offset = 0)
        {
            if (string.IsNullOrWhiteSpace(articleUrl))
            {
                throw new ArgumentException("Article URL cannot be null or empty", nameof(articleUrl));
            }

            if (pageSize <= 0)
            {
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
            }

            var startTime = DateTime.UtcNow;

            try
            {
                if (!_qdrantAvailable)
                {
                    // Retrieve from in-memory fallback
                    return GetArticleChunksFromMemory(articleUrl, pageSize, offset);
                }

                var scrollUrl = $"{_qdrantEndpoint}/collections/{_collectionName}/points/scroll";

                var requestBody = new
                {
                    filter = new
                    {
                        must = new[]
                        {
                            new
                            {
                                key = "article_url",
                                match = new { value = articleUrl }
                            }
                        }
                    },
                    limit = pageSize,
                    offset = offset,
                    with_payload = true,
                    with_vector = false
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(scrollUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to retrieve article chunks from Qdrant: {StatusCode} - {Response}",
                        response.StatusCode, responseJson);
                    return new List<ArticleChunk>();
                }

                using var doc = JsonDocument.Parse(responseJson);
                var result = doc.RootElement.GetProperty("result");
                var points = result.GetProperty("points");

                var chunks = new List<ArticleChunk>();

                foreach (var point in points.EnumerateArray())
                {
                    var payload = point.GetProperty("payload");

                    var chunk = new ArticleChunk
                    {
                        ChunkId = point.GetProperty("id").GetString() ?? string.Empty,
                        ArticleUrl = payload.GetProperty("article_url").GetString() ?? string.Empty,
                        Title = payload.GetProperty("title").GetString() ?? string.Empty,
                        Publisher = payload.GetProperty("publisher").GetString() ?? string.Empty,
                        PublishedDate = DateTime.Parse(payload.GetProperty("published_date").GetString() ?? DateTime.UtcNow.ToString()),
                        Source = payload.GetProperty("source").GetString() ?? string.Empty,
                        ChunkIndex = payload.GetProperty("chunk_index").GetInt32(),
                        Content = payload.GetProperty("content").GetString() ?? string.Empty,
                        ContentLength = payload.GetProperty("content_length").GetInt32()
                    };

                    chunks.Add(chunk);
                }

                // Sort by chunk index to maintain article order
                chunks = chunks.OrderBy(c => c.ChunkIndex).ToList();

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Retrieved {Count} chunks for article {Url} in {Duration}ms (pageSize: {PageSize}, offset: {Offset})",
                    chunks.Count, articleUrl, duration.TotalMilliseconds, pageSize, offset);

                return chunks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving article chunks for {Url}", articleUrl);
                return new List<ArticleChunk>();
            }
        }

        /// <summary>
        /// Search in-memory fallback storage
        /// </summary>
        private List<ChunkSearchResult> SearchInMemory(
            float[] queryEmbedding,
            int topK,
            ChunkFilter? filter,
            int offset)
        {
            var results = new List<ChunkSearchResult>();

            foreach (var (chunkId, (chunk, embedding)) in _memoryFallback)
            {
                // Apply filters
                if (filter != null)
                {
                    if (filter.StartDate.HasValue && chunk.PublishedDate < filter.StartDate.Value)
                        continue;

                    if (filter.EndDate.HasValue && chunk.PublishedDate > filter.EndDate.Value)
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.Source) && chunk.Source != filter.Source)
                        continue;

                    // Note: Symbol filtering would require symbol field in chunk
                }

                // Calculate cosine similarity
                var similarity = CosineSimilarity(queryEmbedding, embedding);

                results.Add(new ChunkSearchResult
                {
                    Chunk = chunk,
                    SimilarityScore = similarity
                });
            }

            // Sort by similarity score descending and apply pagination
            return results
                .OrderByDescending(r => r.SimilarityScore)
                .Skip(offset)
                .Take(topK)
                .ToList();
        }

        /// <summary>
        /// Retrieve article chunks from in-memory fallback storage
        /// </summary>
        private List<ArticleChunk> GetArticleChunksFromMemory(string articleUrl, int pageSize, int offset)
        {
            return _memoryFallback.Values
                .Where(item => item.chunk.ArticleUrl == articleUrl)
                .Select(item => item.chunk)
                .OrderBy(c => c.ChunkIndex)
                .Skip(offset)
                .Take(pageSize)
                .ToList();
        }

        /// <summary>
        /// Calculate cosine similarity between two vectors
        /// </summary>
        private float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vectors must have the same length");

            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            magnitudeA = (float)Math.Sqrt(magnitudeA);
            magnitudeB = (float)Math.Sqrt(magnitudeB);

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct / (magnitudeA * magnitudeB);
        }

        /// <summary>
        /// Store chunk in in-memory fallback storage
        /// </summary>
        /// <param name="chunk">Article chunk to store</param>
        /// <param name="embedding">Vector embedding</param>
        private void StoreInMemory(ArticleChunk chunk, float[] embedding)
        {
            _memoryFallback[chunk.ChunkId] = (chunk, embedding);
            _logger.LogDebug(
                "Stored chunk {ChunkId} in in-memory fallback storage (total: {Count})",
                chunk.ChunkId, _memoryFallback.Count);
        }
    }

    /// <summary>
    /// Filter criteria for chunk search queries
    /// </summary>
    public class ChunkFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Source { get; set; }
        public string? Symbol { get; set; }
    }

    /// <summary>
    /// Search result containing chunk and similarity score
    /// </summary>
    public class ChunkSearchResult
    {
        public ArticleChunk Chunk { get; set; } = new ArticleChunk();
        public float SimilarityScore { get; set; }
    }
}
