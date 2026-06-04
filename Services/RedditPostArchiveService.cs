using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

public class RedditPostArchiveService
{
    private readonly ILogger<RedditPostArchiveService> _logger;
    private readonly HttpClient _httpClient;
    private readonly EmbeddingService _embeddingService;
    private readonly string _qdrantEndpoint;
    private readonly string _collectionName;
    private readonly int _vectorSize;
    private readonly int _batchSize;
    private readonly Dictionary<string, RedditStoredPost> _memoryFallback = new();
    private bool _qdrantAvailable;

    public RedditPostArchiveService(
        HttpClient httpClient,
        EmbeddingService embeddingService,
        IConfiguration configuration,
        ILogger<RedditPostArchiveService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var config = new QdrantConfig();
        configuration.GetSection("Qdrant").Bind(config);

        _qdrantEndpoint = config.Endpoint;
        _collectionName = config.RedditCollectionName;
        _vectorSize = config.VectorSize;
        _batchSize = config.BatchSize;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Reddit Qdrant archive...");

            var healthResponse = await _httpClient.GetAsync($"{_qdrantEndpoint}/");
            if (!healthResponse.IsSuccessStatusCode)
            {
                _qdrantAvailable = false;
                _logger.LogWarning("Qdrant health check failed for Reddit archive: {StatusCode}", healthResponse.StatusCode);
                return;
            }

            var collectionResponse = await _httpClient.GetAsync($"{_qdrantEndpoint}/collections/{_collectionName}");
            if (!collectionResponse.IsSuccessStatusCode)
            {
                await CreateCollectionAsync();
            }

            _qdrantAvailable = true;
            _logger.LogInformation("Reddit Qdrant archive initialized with collection {Collection}", _collectionName);
        }
        catch (Exception ex)
        {
            _qdrantAvailable = false;
            _logger.LogError(ex, "Failed to initialize Reddit archive. Falling back to memory storage.");
        }
    }

    public async Task<int> StorePostsAsync(string symbol, IEnumerable<RedditPost> posts)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        }

        var postList = posts?.Where(post => post != null).ToList() ?? new List<RedditPost>();
        if (postList.Count == 0)
        {
            return 0;
        }

        if (!_qdrantAvailable)
        {
            foreach (var post in postList)
            {
                var record = RedditStoredPost.From(symbol, post);
                _memoryFallback[record.Id] = record;
            }

            return postList.Count;
        }

        var stored = 0;
        foreach (var batch in postList.Chunk(Math.Max(1, _batchSize)))
        {
            var points = new List<object>();

            foreach (var post in batch)
            {
                var record = RedditStoredPost.From(symbol, post);
                var embedding = await _embeddingService.GenerateEmbeddingAsync(record.EmbeddingText);

                points.Add(new
                {
                    id = record.Id,
                    vector = embedding,
                    payload = record.ToPayload()
                });
            }

            var request = new { points };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"{_qdrantEndpoint}/collections/{_collectionName}/points", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to store Reddit posts in Qdrant. Falling back to memory storage for this batch.");
                foreach (var post in batch)
                {
                    var record = RedditStoredPost.From(symbol, post);
                    _memoryFallback[record.Id] = record;
                }
                continue;
            }

            stored += batch.Length;
        }

        return stored;
    }

    public async Task<List<RedditStoredPost>> GetPostsAsync(string symbol, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return new List<RedditStoredPost>();
        }

        if (!_qdrantAvailable)
        {
            return _memoryFallback.Values
                .Where(post => string.Equals(post.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(post => post.CreatedUtc)
                .Take(limit)
                .ToList();
        }

        var scrollRequest = new
        {
            limit = Math.Max(limit, 256),
            with_payload = true,
            with_vector = false
        };

        var content = new StringContent(JsonSerializer.Serialize(scrollRequest), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_qdrantEndpoint}/collections/{_collectionName}/points/scroll", content);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to retrieve Reddit posts from Qdrant. Returning in-memory fallback if available.");
            return _memoryFallback.Values
                .Where(post => string.Equals(post.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(post => post.CreatedUtc)
                .Take(limit)
                .ToList();
        }

        var json = await response.Content.ReadAsStringAsync();
        var results = new List<RedditStoredPost>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("points", out var points))
        {
            foreach (var point in points.EnumerateArray())
            {
                if (!point.TryGetProperty("payload", out var payload))
                {
                    continue;
                }

                results.Add(RedditStoredPost.FromPayload(payload));
            }
        }

        return results
            .Concat(_memoryFallback.Values.Where(post => string.Equals(post.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(post => post.Id)
            .Select(group => group.First())
            .Where(post => string.Equals(post.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(post => post.CreatedUtc)
            .Take(limit)
            .ToList();
    }

    public async Task<int> ClearPostsAsync(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return 0;
        }

        if (!_qdrantAvailable)
        {
            var keys = _memoryFallback.Values
                .Where(post => string.Equals(post.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .Select(post => post.Id)
                .ToList();

            foreach (var key in keys)
            {
                _memoryFallback.Remove(key);
            }

            return keys.Count;
        }

        var storedPosts = await GetPostsAsync(symbol, 1000);
        if (storedPosts.Count == 0)
        {
            return 0;
        }

        var deleteRequest = new
        {
            points = storedPosts.Select(post => post.Id).ToArray()
        };

        var content = new StringContent(JsonSerializer.Serialize(deleteRequest), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_qdrantEndpoint}/collections/{_collectionName}/points/delete", content);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to clear Reddit posts from Qdrant for {Symbol}", symbol);
        }

        var memoryKeys = _memoryFallback.Values
            .Where(post => string.Equals(post.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            .Select(post => post.Id)
            .ToList();

        foreach (var key in memoryKeys)
        {
            _memoryFallback.Remove(key);
        }

        return storedPosts.Count + memoryKeys.Count;
    }

    private async Task CreateCollectionAsync()
    {
        var requestBody = new
        {
            vectors = new
            {
                size = _vectorSize,
                distance = "Cosine"
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"{_qdrantEndpoint}/collections/{_collectionName}", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to create Reddit archive collection: {response.StatusCode} - {responseJson}");
        }
    }

    private static string CreatePointId(string symbol, RedditPost post)
    {
        if (!string.IsNullOrWhiteSpace(post.Id))
        {
            var stableBasis = $"{symbol}|{post.Subreddit}|{post.Id}".Trim().ToLowerInvariant();
            var stableHash = SHA256.HashData(Encoding.UTF8.GetBytes(stableBasis));
            return Convert.ToHexString(stableHash).ToLowerInvariant();
        }

        var basis = $"{symbol}|{post.Subreddit}|{post.Url}|{post.Title}".Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(basis));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class RedditStoredPost
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Subreddit { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public int Comments { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string SearchQuery { get; set; } = string.Empty;
    public string EmbeddingText { get; set; } = string.Empty;

    public static RedditStoredPost From(string symbol, RedditPost post)
    {
        var createdUtc = post.CreatedUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(post.CreatedUtc, DateTimeKind.Utc)
            : post.CreatedUtc.ToUniversalTime();

        return new RedditStoredPost
        {
            Id = CreatePointId(symbol, post),
            Symbol = symbol,
            Subreddit = post.Subreddit,
            Title = post.Title,
            Content = post.Content,
            Url = post.Url,
            Author = post.Author,
            Score = post.Score,
            Upvotes = post.Upvotes,
            Downvotes = post.Downvotes,
            Comments = post.Comments,
            CreatedUtc = createdUtc,
            SearchQuery = post.SearchQuery ?? symbol,
            EmbeddingText = string.Join("\n", new[]
            {
                symbol,
                post.Subreddit,
                post.SearchQuery ?? symbol,
                post.Title,
                post.Content,
                $"Score: {post.Score}",
                $"Comments: {post.Comments}",
                $"Upvotes: {post.Upvotes}",
                $"Downvotes: {post.Downvotes}"
            }.Where(part => !string.IsNullOrWhiteSpace(part)))
        };
    }

    public static RedditStoredPost FromPayload(JsonElement payload)
    {
        return new RedditStoredPost
        {
            Id = GetString(payload, "id"),
            Symbol = GetString(payload, "symbol"),
            Subreddit = GetString(payload, "subreddit"),
            Title = GetString(payload, "title"),
            Content = GetString(payload, "content"),
            Url = GetString(payload, "url"),
            Author = GetString(payload, "author"),
            Score = GetInt(payload, "score"),
            Upvotes = GetInt(payload, "upvotes"),
            Downvotes = GetInt(payload, "downvotes"),
            Comments = GetInt(payload, "comments"),
            CreatedUtc = DateTime.TryParse(GetString(payload, "created_utc"), out var created)
                ? DateTime.SpecifyKind(created, DateTimeKind.Utc)
                : DateTime.UtcNow,
            SearchQuery = GetString(payload, "search_query")
        };
    }

    public object ToPayload() => new
    {
        id = Id,
        symbol = Symbol,
        subreddit = Subreddit,
        title = Title,
        content = Content,
        url = Url,
        author = Author,
        score = Score,
        upvotes = Upvotes,
        downvotes = Downvotes,
        comments = Comments,
        created_utc = CreatedUtc.ToUniversalTime().ToString("o"),
        search_query = SearchQuery
    };

    private static string CreatePointId(string symbol, RedditPost post)
    {
        var basis = $"{symbol}|{post.Subreddit}|{post.Id}|{post.Url}|{post.Title}|{post.CreatedUtc:O}".Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(basis));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetString(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : 0;
    }
}