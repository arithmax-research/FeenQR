using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services;

/// <summary>
/// Low-level C# client for the Arithmax Chest (achest) API.
/// Mirrors the Python <c>achest.MarketDataClient</c>: one HTTP API that fronts
/// every data provider (Yahoo, Binance, Polygon, DataBento, Eulerpool, FRED,
/// Alpha Vantage, Tiingo, ...).
///
/// Endpoints used:
///   GET  /health
///   GET  /v1/providers
///   GET  /v1/route?symbol=&amp;resolution=&amp;provider=
///   POST /v1/data      { symbols, start, end, resolution, provider, format }
///   GET  /v1/eulerpool/{...}   (generic passthrough for extended research data)
///
/// Configuration (appsettings.json → "Achest"):
///   { "BaseUrl": "https://achestv2.misango.me", "Token": "&lt;DATA_API_TOKEN&gt;" }
/// </summary>
public class AchestClient : IDisposable
{
    public const string DefaultBaseUrl = "https://achestv2.misango.me";

    private readonly ILogger<AchestClient> _logger;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public string BaseUrl { get; }
    public bool HasToken => _http.DefaultRequestHeaders.Authorization != null;

    /// <summary>Creates a client from configuration (appsettings "Achest" section). Public for DI.</summary>
    public AchestClient(IConfiguration configuration, ILogger<AchestClient> logger)
        : this(
            configuration?["Achest:BaseUrl"],
            configuration?["Achest:Token"],
            logger,
            httpClient: null,
            requestTimeoutSeconds: int.TryParse(configuration?["Achest:RequestTimeoutSeconds"], out var t) ? t : 20)
    {
    }

    /// <summary>Creates a client with an injected <see cref="HttpClient"/> (DI friendly).</summary>
    public AchestClient(string baseUrl, string token, ILogger<AchestClient> logger, HttpClient? httpClient, int requestTimeoutSeconds = 20)
    {
        _logger = logger;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
        _requestTimeout = TimeSpan.FromSeconds(requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 20);

        if (httpClient != null)
        {
            _http = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
            _ownsHttpClient = true;
        }

        _http.BaseAddress ??= new Uri(BaseUrl + "/");
        if (!string.IsNullOrWhiteSpace(token) && _http.DefaultRequestHeaders.Authorization == null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        _logger.LogInformation("AchestClient initialized (baseUrl={BaseUrl}, authenticated={Authenticated})",
            BaseUrl, HasToken);
    }

    // ── Health / discovery ─────────────────────────────────────────────

    /// <summary>Returns true when the achest service reports a healthy status.</summary>
    public async Task<bool> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync("health", ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "achest health check failed");
            return false;
        }
    }

    /// <summary>Returns provider capabilities keyed by provider name.</summary>
    public async Task<Dictionary<string, JsonElement>> GetProvidersAsync(CancellationToken ct = default)
    {
        var root = await GetJsonAsync("v1/providers", ct).ConfigureAwait(false);
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("providers", out var providers) &&
            providers.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(providers.GetRawText())
                   ?? new Dictionary<string, JsonElement>();
        }
        return new Dictionary<string, JsonElement>();
    }

    /// <summary>Inspects which provider achest would select for a symbol/resolution.</summary>
    public async Task<string?> RouteAsync(string symbol, string resolution = "daily", string provider = "auto", CancellationToken ct = default)
    {
        var path = $"v1/route?symbol={Uri.EscapeDataString(symbol)}&resolution={Uri.EscapeDataString(resolution)}&provider={Uri.EscapeDataString(provider)}";
        var root = await GetJsonAsync(path, ct).ConfigureAwait(false);
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("provider", out var p))
        {
            return p.GetString();
        }
        return null;
    }

    // ── OHLCV market data ──────────────────────────────────────────────

    /// <summary>
    /// Fetches normalized OHLCV bars for one or more symbols between
    /// <paramref name="start"/> (inclusive) and <paramref name="end"/> (exclusive).
    /// Mirrors achest's <c>client.get(...)</c>.
    /// </summary>
    public async Task<List<AchestBar>> GetDataAsync(
        IEnumerable<string> symbols,
        DateTime start,
        DateTime end,
        string resolution = "daily",
        string provider = "auto",
        CancellationToken ct = default)
    {
        var symbolList = symbols?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
                         ?? new List<string>();
        if (symbolList.Count == 0)
        {
            throw new ArgumentException("At least one symbol is required.", nameof(symbols));
        }

        var body = new AchestDataRequest
        {
            Symbols = symbolList,
            Start = start.ToString("yyyy-MM-dd"),
            End = end.ToString("yyyy-MM-dd"),
            Resolution = NormalizeResolution(resolution),
            Provider = string.IsNullOrWhiteSpace(provider) ? "auto" : provider,
            Format = "json",
        };

        using var resp = await PostWithRetryAsync("v1/data", body, ct).ConfigureAwait(false);
        var json = await SafeReadAsync(resp, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new AchestException(
                $"achest /v1/data failed ({(int)resp.StatusCode} {resp.StatusCode}): {json}",
                resp.StatusCode);
        }

        return ParseBars(json);
    }

    /// <summary>Convenience wrapper for a single symbol. Mirrors achest's <c>client.get(symbol, ...)</c>.</summary>
    public Task<List<AchestBar>> GetDataAsync(
        string symbol,
        DateTime start,
        DateTime end,
        string resolution = "daily",
        string provider = "auto",
        CancellationToken ct = default)
        => GetDataAsync(new[] { symbol }, start, end, resolution, provider, ct);

    /// <summary>Returns the latest <paramref name="limit"/> daily bars for a symbol.</summary>
    public async Task<List<AchestBar>> GetDailyBarsAsync(string symbol, int limit = 100, CancellationToken ct = default)
    {
        var end = DateTime.UtcNow.Date.AddDays(1);                 // end is exclusive in achest
        var start = end.AddDays(-Math.Max(limit, 1) * 2);          // pad for weekends/holidays
        var bars = await GetDataAsync(symbol, start, end, "daily", "auto", ct).ConfigureAwait(false);
        return bars.Count > limit ? bars.TakeLast(limit).ToList() : bars;
    }

    // ── Extended Eulerpool research data (generic passthrough) ─────────

    /// <summary>
    /// Calls an arbitrary achest Eulerpool endpoint, e.g.
    /// <c>GetEulerpoolAsync("fundamentals/overview/AAPL")</c> or
    /// <c>GetEulerpoolAsync("crypto/fear-greed")</c>.
    /// </summary>
    public async Task<JsonElement> GetEulerpoolAsync(string path, CancellationToken ct = default)
    {
        var clean = path.TrimStart('/');
        if (clean.StartsWith("v1/eulerpool/", StringComparison.OrdinalIgnoreCase))
        {
            return await GetJsonAsync(clean, ct).ConfigureAwait(false);
        }
        return await GetJsonAsync($"v1/eulerpool/{clean}", ct).ConfigureAwait(false);
    }

    // ── Internals ──────────────────────────────────────────────────────

    /// <summary>
    /// Per-request timeout for eulerpool/passthrough GETs. Prevents a single slow
    /// upstream endpoint (e.g. a hanging Eulerpool call) from blocking the whole
    /// request. Configurable via <c>Achest:RequestTimeoutSeconds</c> (default 20s).
    /// </summary>
    private readonly TimeSpan _requestTimeout;

    private async Task<JsonElement> GetJsonAsync(string path, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_requestTimeout);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.GetAsync(path, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AchestException($"achest GET {path} timed out after {_requestTimeout.TotalSeconds:F0}s", HttpStatusCode.RequestTimeout);
        }

        using (resp)
        {
            var json = await SafeReadAsync(resp, timeoutCts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                throw new AchestException(
                    $"achest GET {path} failed ({(int)resp.StatusCode} {resp.StatusCode}): {json}",
                    resp.StatusCode);
            }
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }
            using var doc = JsonDocument.Parse(SanitizeJson(json), JsonDocOpts);
            return doc.RootElement.Clone();
        }
    }

    private static readonly JsonDocumentOptions JsonDocOpts = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly Regex NonFiniteNumberRegex =
        new(@"(?<=[:,\[\s])-?(NaN|Infinity)(?=\s*[,}\]])", RegexOptions.Compiled);

    /// <summary>
    /// Some Eulerpool-backed achest endpoints emit bare <c>NaN</c>/<c>Infinity</c> literals
    /// (as Python's json.dumps does), which the strict System.Text.Json parser rejects.
    /// Replace those tokens with <c>null</c> so parsing succeeds.
    /// </summary>
    private static string SanitizeJson(string json)
    {
        if (json.IndexOf("NaN", StringComparison.Ordinal) < 0 &&
            json.IndexOf("Infinity", StringComparison.Ordinal) < 0)
        {
            return json;
        }
        return NonFiniteNumberRegex.Replace(json, "null");
    }

    private async Task<HttpResponseMessage> PostWithRetryAsync<T>(string path, T payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");

        const int maxAttempts = 3;
        Exception? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await _http.PostAsync(path, content, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException ||
                                       (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                last = ex;
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct).ConfigureAwait(false);
                }
            }
        }
        throw new AchestException($"achest POST {path} failed after {maxAttempts} attempts", HttpStatusCode.ServiceUnavailable, last);
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<AchestBar> ParseBars(string json)
    {
        var bars = new List<AchestBar>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return bars;
        }

        using var doc = JsonDocument.Parse(SanitizeJson(json), JsonDocOpts);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return bars;
        }

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            bars.Add(new AchestBar
            {
                Timestamp = ParseTimestamp(row),
                Symbol = GetString(row, "symbol"),
                Open = GetDouble(row, "open"),
                High = GetDouble(row, "high"),
                Low = GetDouble(row, "low"),
                Close = GetDouble(row, "close"),
                Volume = GetDouble(row, "volume"),
            });
        }

        return bars.OrderBy(b => b.Timestamp).ToList();
    }

    private static DateTime ParseTimestamp(JsonElement row)
    {
        if (!row.TryGetProperty("timestamp", out var ts))
        {
            return default;
        }

        if (ts.ValueKind == JsonValueKind.String)
        {
            var raw = ts.GetString();
            if (DateTime.TryParse(raw, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var dt))
            {
                return dt;
            }
        }
        else if (ts.ValueKind == JsonValueKind.Number && ts.TryGetInt64(out var epoch))
        {
            // Heuristic: >10^12 means milliseconds, else seconds.
            return epoch > 1_000_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }

        return default;
    }

    private static string GetString(JsonElement row, string name)
        => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static double GetDouble(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var v))
        {
            return 0d;
        }
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), out var d) => d,
            _ => 0d,
        };
    }

    /// <summary>Maps FeenQR resolution keywords onto achest's supported set.</summary>
    public static string NormalizeResolution(string? resolution)
    {
        var r = (resolution ?? "daily").Trim().ToLowerInvariant();
        return r switch
        {
            "1d" or "d" or "1day" or "day" => "daily",
            "1h" or "60m" or "60min" => "hour",
            "1m" or "min" or "1min" => "minute",
            "1s" or "sec" or "1sec" => "second",
            "trade" or "trades" => "tick",
            _ => r, // tick | second | minute | hour | daily
        };
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}

/// <summary>Request payload for <c>POST /v1/data</c> — matches achest's <c>DownloadRequest</c>.</summary>
public class AchestDataRequest
{
    [JsonPropertyName("symbols")]
    public List<string> Symbols { get; set; } = new();

    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;

    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;

    [JsonPropertyName("resolution")]
    public string Resolution { get; set; } = "daily";

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "auto";

    [JsonPropertyName("format")]
    public string Format { get; set; } = "json";
}

/// <summary>A single normalized OHLCV bar returned by achest.</summary>
public class AchestBar
{
    public DateTime Timestamp { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }
}

/// <summary>Raised when an achest request fails.</summary>
public class AchestException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public AchestException(string message, HttpStatusCode statusCode, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}


