using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Service for generating text embeddings using OpenAI API
    /// </summary>
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmbeddingService> _logger;
        private readonly IMemoryCache _cache;
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly int _batchSize;
        private readonly int _cacheTTLDays;
        private readonly string _embeddingEndpoint;

        public EmbeddingService(
            HttpClient httpClient,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<EmbeddingService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration
            _apiKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentException("OpenAI API key not configured");
            
            var embeddingConfig = new EmbeddingConfig();
            configuration.GetSection("Embedding").Bind(embeddingConfig);
            
            _modelName = embeddingConfig.ModelName;
            _batchSize = embeddingConfig.BatchSize;
            _cacheTTLDays = embeddingConfig.CacheTTLDays;
            _embeddingEndpoint = "https://api.openai.com/v1/embeddings";

            _logger.LogInformation(
                "EmbeddingService initialized with model: {Model}, batch size: {BatchSize}, cache TTL: {CacheTTL} days",
                _modelName, _batchSize, _cacheTTLDays);
        }

        /// <summary>
        /// Generate embedding for a single text
        /// </summary>
        /// <param name="text">Text to generate embedding for</param>
        /// <returns>1536-dimensional embedding vector</returns>
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text cannot be null or empty", nameof(text));
            }

            // Check cache first
            var cacheKey = $"embedding:{_modelName}:{GetTextHash(text)}";
            if (_cache.TryGetValue<float[]>(cacheKey, out var cachedEmbedding))
            {
                _logger.LogDebug("Cache hit for embedding: {CacheKey}", cacheKey);
                return cachedEmbedding;
            }

            // Generate new embedding
            var embeddings = await GenerateEmbeddingsAsync(new List<string> { text });
            var embedding = embeddings.First();

            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_cacheTTLDays)
            };
            _cache.Set(cacheKey, embedding, cacheOptions);

            return embedding;
        }

        /// <summary>
        /// Generate embeddings for multiple texts in batch (up to 50 texts per call)
        /// </summary>
        /// <param name="texts">List of texts to generate embeddings for</param>
        /// <returns>List of 1536-dimensional embedding vectors</returns>
        public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
        {
            if (texts == null || texts.Count == 0)
            {
                throw new ArgumentException("Texts list cannot be null or empty", nameof(texts));
            }

            var allEmbeddings = new List<float[]>();
            var uncachedTexts = new List<(int index, string text)>();
            var cachedResults = new Dictionary<int, float[]>();

            // Check cache for each text
            for (int i = 0; i < texts.Count; i++)
            {
                var text = texts[i];
                var cacheKey = $"embedding:{_modelName}:{GetTextHash(text)}";
                
                if (_cache.TryGetValue<float[]>(cacheKey, out var cachedEmbedding))
                {
                    cachedResults[i] = cachedEmbedding;
                }
                else
                {
                    uncachedTexts.Add((i, text));
                }
            }

            _logger.LogDebug(
                "Embedding batch: {Total} texts, {Cached} cached, {Uncached} to generate",
                texts.Count, cachedResults.Count, uncachedTexts.Count);

            // Process uncached texts in batches
            if (uncachedTexts.Count > 0)
            {
                var batches = uncachedTexts
                    .Select((item, index) => new { item, index })
                    .GroupBy(x => x.index / _batchSize)
                    .Select(g => g.Select(x => x.item).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    var batchTexts = batch.Select(x => x.text).ToList();
                    var batchEmbeddings = await CallEmbeddingApiAsync(batchTexts);

                    // Cache the results
                    for (int i = 0; i < batch.Count; i++)
                    {
                        var (originalIndex, text) = batch[i];
                        var embedding = batchEmbeddings[i];
                        
                        var cacheKey = $"embedding:{_modelName}:{GetTextHash(text)}";
                        var cacheOptions = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_cacheTTLDays)
                        };
                        _cache.Set(cacheKey, embedding, cacheOptions);
                        
                        cachedResults[originalIndex] = embedding;
                    }
                }
            }

            // Reconstruct results in original order
            for (int i = 0; i < texts.Count; i++)
            {
                allEmbeddings.Add(cachedResults[i]);
            }

            return allEmbeddings;
        }

        /// <summary>
        /// Call OpenAI embeddings API with retry logic and exponential backoff
        /// </summary>
        /// <param name="texts">List of texts to generate embeddings for (max 50)</param>
        /// <returns>List of embedding vectors</returns>
        private async Task<List<float[]>> CallEmbeddingApiAsync(List<string> texts)
        {
            if (texts.Count > _batchSize)
            {
                throw new ArgumentException($"Batch size cannot exceed {_batchSize}", nameof(texts));
            }

            const int maxRetries = 3;
            var startTime = DateTime.UtcNow;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var requestBody = new
                    {
                        model = _modelName,
                        input = texts
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody);
                    var request = new HttpRequestMessage(HttpMethod.Post, _embeddingEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError(
                            "OpenAI Embeddings API error (attempt {Attempt}/{MaxRetries}): {StatusCode} - {Response}",
                            attempt + 1, maxRetries, response.StatusCode, responseJson);

                        // If rate limited or server error, retry with exponential backoff
                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            if (attempt < maxRetries - 1)
                            {
                                var delayMs = (int)Math.Pow(2, attempt) * 1000; // 1s, 2s, 4s
                                _logger.LogWarning("Retrying after {Delay}ms...", delayMs);
                                await Task.Delay(delayMs);
                                continue;
                            }
                        }

                        throw new Exception($"OpenAI Embeddings API error: {response.StatusCode} - {responseJson}");
                    }

                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogInformation(
                        "Generated embeddings for {Count} texts using model {Model} in {Duration}ms",
                        texts.Count, _modelName, duration.TotalMilliseconds);

                    // Parse response
                    using var doc = JsonDocument.Parse(responseJson);
                    var dataArray = doc.RootElement.GetProperty("data");
                    var embeddings = new List<float[]>();

                    foreach (var item in dataArray.EnumerateArray())
                    {
                        var embeddingArray = item.GetProperty("embedding");
                        var embedding = new List<float>();
                        
                        foreach (var value in embeddingArray.EnumerateArray())
                        {
                            embedding.Add((float)value.GetDouble());
                        }
                        
                        embeddings.Add(embedding.ToArray());
                    }

                    return embeddings;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, 
                        "HTTP request failed (attempt {Attempt}/{MaxRetries})", 
                        attempt + 1, maxRetries);

                    if (attempt < maxRetries - 1)
                    {
                        var delayMs = (int)Math.Pow(2, attempt) * 1000;
                        _logger.LogWarning("Retrying after {Delay}ms...", delayMs);
                        await Task.Delay(delayMs);
                        continue;
                    }

                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogError(ex, 
                        "Embedding generation failed (attempt {Attempt}/{MaxRetries})", 
                        attempt + 1, maxRetries);

                    var delayMs = (int)Math.Pow(2, attempt) * 1000;
                    _logger.LogWarning("Retrying after {Delay}ms...", delayMs);
                    await Task.Delay(delayMs);
                }
            }

            throw new Exception($"Failed to generate embeddings after {maxRetries} attempts");
        }

        /// <summary>
        /// Generate a hash for text to use as cache key
        /// </summary>
        private string GetTextHash(string text)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            return Convert.ToBase64String(hashBytes);
        }
    }
}
