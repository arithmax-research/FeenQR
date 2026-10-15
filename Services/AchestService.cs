using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Text.Json;

namespace QuantResearchAgent.Services;

/// <summary>
/// High-level façade over <see cref="AchestClient"/> that returns FeenQR's
/// native <see cref="MarketData"/> shapes. This is the single entry point the
/// rest of the app should use for market data so all provider access flows
/// through the Arithmax Chest API.
/// </summary>
public class AchestService
{
    private readonly AchestClient _client;
    private readonly ILogger<AchestService> _logger;

    /// <summary>
    /// Deduplicates concurrent Eulerpool requests for the same path.
    /// When N callers ask for the same endpoint simultaneously, only one
    /// HTTP request is dispatched; the others share its result (or failure).
    /// Entries are removed on completion so subsequent calls re-fetch.
    /// </summary>
    private readonly Dictionary<string, Task<System.Text.Json.JsonElement?>> _inflightEulerpool
        = new();

    /// <summary>
    /// Short-lived cache that keeps successful Eulerpool responses in memory
    /// for a few seconds so that back-to-back requests (e.g. from different
    /// fallback chains that fire sequentially rather than in parallel) avoid
    /// redundant network calls.
    /// </summary>
    private readonly Dictionary<string, CachedEulerpoolResult> _eulerpoolCache = new();

    /// <summary>Duration to keep a successful Eulerpool result in cache (5 seconds).</summary>
    internal static readonly TimeSpan EulerpoolCacheTtl = TimeSpan.FromSeconds(5);

    public AchestService(AchestClient client, ILogger<AchestService> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>True when the achest service is reachable.</summary>
    public Task<bool> IsHealthyAsync(CancellationToken ct = default) => _client.HealthAsync(ct);

    /// <summary>Resolves the provider achest would use for a symbol.</summary>
    public Task<string?> RouteProviderAsync(string symbol, string resolution = "daily", CancellationToken ct = default)
        => _client.RouteAsync(symbol, resolution, "auto", ct);

    /// <summary>
    /// Returns the latest quote for a symbol as a <see cref="MarketData"/> object,
    /// derived from the two most recent achest daily bars.
    /// </summary>
    public async Task<MarketData?> GetQuoteAsync(string symbol, string resolution = "daily", CancellationToken ct = default)
    {
        try
        {
            var bars = await _client.GetDataAsync(symbol, DateTime.UtcNow.Date.AddDays(-14), DateTime.UtcNow.Date.AddDays(1), resolution, "auto", ct).ConfigureAwait(false);
            if (bars.Count == 0)
            {
                return null;
            }

            var latest = bars[^1];
            var previous = bars.Count > 1 ? bars[^2] : null;
            var change = previous != null ? latest.Close - previous.Close : latest.Close - latest.Open;
            var changePct = previous != null && previous.Close != 0 ? (change / previous.Close) * 100 : 0;

            return new MarketData
            {
                Symbol = string.IsNullOrWhiteSpace(latest.Symbol) ? symbol : latest.Symbol,
                Price = latest.Close,
                Close = latest.Close,
                Volume = latest.Volume,
                High24h = latest.High,
                Low24h = latest.Low,
                Change24h = change,
                ChangePercent24h = changePct,
                Timestamp = latest.Timestamp,
                Source = "achest",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "achest quote fetch failed for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>
    /// Returns historical OHLCV bars mapped to <see cref="MarketData"/> records,
    /// newest last, capped at <paramref name="limit"/> rows.
    /// </summary>
    public async Task<List<MarketData>?> GetHistoricalDataAsync(string symbol, int limit = 100, string resolution = "daily", CancellationToken ct = default)
    {
        try
        {
            var res = AchestClient.NormalizeResolution(resolution);
            var end = DateTime.UtcNow.Date.AddDays(1);
            var start = end.AddDays(-Math.Max(limit, 1) * 2);
            var bars = await _client.GetDataAsync(symbol, start, end, res, "auto", ct).ConfigureAwait(false);
            if (bars.Count == 0)
            {
                return null;
            }

            var mapped = bars.Select(b => new MarketData
            {
                Symbol = string.IsNullOrWhiteSpace(b.Symbol) ? symbol : b.Symbol,
                Price = b.Close,
                Close = b.Close,
                Volume = b.Volume,
                High24h = b.High,
                Low24h = b.Low,
                Timestamp = b.Timestamp,
                Source = "achest",
            }).ToList();

            return mapped.Count > limit ? mapped.TakeLast(limit).ToList() : mapped;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "achest historical fetch failed for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>Raw bars passthrough, when callers need OHLCV directly.</summary>
    public Task<List<AchestBar>> GetBarsAsync(string symbol, DateTime start, DateTime end, string resolution = "daily", CancellationToken ct = default)
        => _client.GetDataAsync(symbol, start, end, resolution, "auto", ct);

    /// <summary>
    /// Computes the market beta of a symbol versus a benchmark index (default S&amp;P 500,
    /// <c>^GSPC</c>) from daily OHLCV returned by achest (Yahoo-routed). Beta is the
    /// covariance of daily returns divided by the benchmark variance over
    /// <paramref name="lookbackDays"/> trading days. Returns <c>null</c> when there
    /// is insufficient overlapping data.
    /// </summary>
    public async Task<double?> GetBetaAsync(string symbol, string benchmark = "^GSPC", int lookbackDays = 365, CancellationToken ct = default)
    {
        try
        {
            var end = DateTime.UtcNow.Date.AddDays(1);
            // Pad the calendar range so we get ~lookbackDays trading days.
            var start = end.AddDays(-(int)(lookbackDays * 1.6));

            var symbolBars = await _client.GetDataAsync(symbol, start, end, "daily", "auto", ct).ConfigureAwait(false);
            if (symbolBars.Count < 30)
            {
                return null;
            }

            List<AchestBar> benchBars;
            if (string.Equals(symbol, benchmark, StringComparison.OrdinalIgnoreCase))
            {
                return 1.0;
            }
            benchBars = await _client.GetDataAsync(benchmark, start, end, "daily", "auto", ct).ConfigureAwait(false);
            if (benchBars.Count < 30)
            {
                return null;
            }

            // Align on date (daily close), compute simple returns.
            var benchByDate = benchBars
                .GroupBy(b => b.Timestamp.Date)
                .ToDictionary(g => g.Key, g => g.Last().Close);

            var symbolReturns = new List<double>();
            var benchReturns = new List<double>();
            double? prevSymbol = null;
            double? prevBench = null;

            foreach (var bar in symbolBars.OrderBy(b => b.Timestamp))
            {
                var date = bar.Timestamp.Date;
                if (!benchByDate.TryGetValue(date, out var benchClose))
                {
                    continue;
                }
                if (prevSymbol is > 0 && prevBench is > 0 && bar.Close > 0 && benchClose > 0)
                {
                    symbolReturns.Add(bar.Close / prevSymbol.Value - 1.0);
                    benchReturns.Add(benchClose / prevBench.Value - 1.0);
                }
                prevSymbol = bar.Close;
                prevBench = benchClose;
            }

            if (symbolReturns.Count < 20)
            {
                return null;
            }

            var n = symbolReturns.Count;
            var meanS = symbolReturns.Average();
            var meanB = benchReturns.Average();

            double cov = 0, varB = 0;
            for (var i = 0; i < n; i++)
            {
                var ds = symbolReturns[i] - meanS;
                var db = benchReturns[i] - meanB;
                cov += ds * db;
                varB += db * db;
            }

            if (varB == 0)
            {
                return null;
            }

            return cov / varB;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "achest beta computation failed for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>Access to the underlying low-level client for extended endpoints.</summary>
    public AchestClient Client => _client;

    // ── Analyst recommendations & valuation (Eulerpool) ────────────────

    /// <summary>
    /// Analyst consensus recommendations for the most recent period, including the
    /// buy/hold/sell tally and consensus price targets. Returns null when unavailable.
    /// </summary>
    public async Task<AchestAnalystConsensus?> GetAnalystConsensusAsync(string symbol, CancellationToken ct = default)
    {
        var data = await TryEulerpoolAsync($"analyst/recommendations/{Uri.EscapeDataString(symbol)}", ct);
        if (!data.HasValue || data.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var row in data.Value.EnumerateArray())
        {
            // Rows are returned most-recent-first; take the first with a usable target.
            return new AchestAnalystConsensus
            {
                Period = GetStr(row, "period"),
                StrongBuy = (int)(GetDbl(row, "strongBuy") ?? 0),
                Buy = (int)(GetDbl(row, "buy") ?? 0),
                Hold = (int)(GetDbl(row, "hold") ?? 0),
                Sell = (int)(GetDbl(row, "sell") ?? 0),
                StrongSell = (int)(GetDbl(row, "strongSell") ?? 0),
                TargetMean = GetDbl(row, "targetMean"),
                TargetMedian = GetDbl(row, "targetMedian"),
                TargetHigh = GetDbl(row, "targetHigh"),
                TargetLow = GetDbl(row, "targetLow"),
            };
        }
        return null;
    }

    /// <summary>Latest consensus price target (high/low/mean/median) for a symbol.</summary>
    public async Task<AchestPriceTarget?> GetPriceTargetAsync(string symbol, CancellationToken ct = default)
    {
        var data = await TryEulerpoolAsync($"analyst/price-target/{Uri.EscapeDataString(symbol)}", ct);
        if (!data.HasValue || data.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var d = data.Value;
        return new AchestPriceTarget
        {
            Ticker = GetStr(d, "ticker"),
            TargetHigh = GetDbl(d, "target_high"),
            TargetLow = GetDbl(d, "target_low"),
            TargetMean = GetDbl(d, "target_mean"),
            TargetMedian = GetDbl(d, "target_median"),
            LastUpdated = GetStr(d, "last_updated"),
        };
    }

    /// <summary>Analyst-computed fair value with upside/downside (Eulerpool model).</summary>
    public async Task<AchestFairValue?> GetFairValueAsync(string symbol, CancellationToken ct = default)
    {
        var data = await TryEulerpoolAsync($"fundamentals/fair-value/{Uri.EscapeDataString(symbol)}", ct);
        if (!data.HasValue || data.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var d = data.Value;
        return new AchestFairValue
        {
            FairValue = GetDbl(d, "fairValue"),
            FairValueIncome = GetDbl(d, "fairValueIncome"),
            FairValueRevenue = GetDbl(d, "fairValueRevenue"),
            FairValueDividend = GetDbl(d, "fairValueDividend"),
            LastPrice = GetDbl(d, "lastPrice"),
            Upside = GetDbl(d, "upside"),
        };
    }

    /// <summary>Recent individual analyst grades (upgrades/downgrades/maintains).</summary>
    public async Task<List<AchestAnalystGrade>> GetAnalystGradesAsync(string symbol, int limit = 25, CancellationToken ct = default)
    {
        var result = new List<AchestAnalystGrade>();
        var data = await TryEulerpoolAsync($"analyst/grades/{Uri.EscapeDataString(symbol)}", ct);
        if (!data.HasValue || data.Value.ValueKind != JsonValueKind.Array)
        {
            return result;
        }
        foreach (var row in data.Value.EnumerateArray())
        {
            result.Add(new AchestAnalystGrade
            {
                Date = GetStr(row, "date"),
                Company = GetStr(row, "grading_company") ?? GetStr(row, "company"),
                PreviousGrade = GetStr(row, "previous_grade") ?? GetStr(row, "fromGrade"),
                NewGrade = GetStr(row, "new_grade") ?? GetStr(row, "toGrade"),
                Action = GetStr(row, "action"),
            });
            if (result.Count >= limit) break;
        }
        return result;
    }

    private static string? GetStr(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
            : null;

    private static double? GetDbl(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), out var d) => d,
            _ => null
        };
    }


    // ── Generic Eulerpool passthrough with convenience accessors ───────

    /// <summary>
    /// Fetch any achest Eulerpool endpoint as a raw <see cref="JsonElement"/>.
    /// Returns <c>null</c> on failure (never throws), so callers can fall back.
    ///
    /// <b>Deduplication:</b> concurrent calls for the same <paramref name="path"/>
    /// share a single HTTP request — see the in-flight and short-term caches.
    /// Once the shared task completes (success or failure) the in-flight entry
    /// is evicted; successful results stay in a short-lived cache for a few
    /// seconds to cover back-to-back sequential calls from different services.
    /// </summary>
    public async Task<System.Text.Json.JsonElement?> TryEulerpoolAsync(string path, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
        {
            return null;
        }

        // 1. Short-term cache hit (successful response fetched < TTL ago).
        lock (_eulerpoolCache)
        {
            var cached = _eulerpoolCache.ContainsKey(path)
                ? _eulerpoolCache[path]
                : null;
            if (cached != null && !cached.IsExpired)
            {
                _logger.LogDebug("Returning cached Eulerpool result for {Path} (age={Age:F0}s)",
                    path, (DateTime.UtcNow - cached.FetchedAt).TotalSeconds);
                return cached.Result;
            }
        }

        // 2. Deduplicate in-flight requests for the same path.
        Task<System.Text.Json.JsonElement?> pendingTask = default;
        lock (_inflightEulerpool)
        {
            pendingTask = _inflightEulerpool.ContainsKey(path)
                ? _inflightEulerpool[path]
                : null;
            if (pendingTask == null)
            {
                // Register this call as the shared task for this path.
                pendingTask = DedupEulerpoolFetchAsync(path, ct);
                _inflightEulerpool[path] = pendingTask;
            }
            else
            {
                _logger.LogDebug("Deduplicating concurrent Eulerpool request for {Path}", path);
            }
        }

        return await pendingTask.ConfigureAwait(false);
    }

    /// <summary>Performs the actual fetch and always cleans up the dedup map.</summary>
    private async Task<System.Text.Json.JsonElement?> DedupEulerpoolFetchAsync(string path, CancellationToken ct)
    {
        System.Text.Json.JsonElement? result = null;
        try
        {
            result = await _client.GetEulerpoolAsync(path, ct).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "achest eulerpool fetch failed for {Path}", path);
            return null;
        }
        finally
        {
            // Evict from the dedup map so future calls re-fetch.
            lock (_inflightEulerpool)
            {
                _inflightEulerpool.Remove(path);
            }

            // Populate the short-term cache on success.
            if (result != null)
            {
                lock (_eulerpoolCache)
                {
                    _eulerpoolCache[path] = new CachedEulerpoolResult
                    {
                        Result = result,
                        FetchedAt = DateTime.UtcNow,
                    };
                }
            }
        }
    }

    /// <summary>Company profile + overview combined (Eulerpool fundamentals).</summary>
    public Task<System.Text.Json.JsonElement?> GetCompanyOverviewAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/overview/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>40+ financial ratios (P/E, ROE, margins, growth, leverage).</summary>
    public Task<System.Text.Json.JsonElement?> GetFinancialMetricsAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/metrics/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Company profile: description, sector, employees, website.</summary>
    public Task<System.Text.Json.JsonElement?> GetCompanyProfileAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/profile/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Annual income statements.</summary>
    public Task<System.Text.Json.JsonElement?> GetIncomeStatementAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/income/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Annual balance sheets.</summary>
    public Task<System.Text.Json.JsonElement?> GetBalanceSheetAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/balance/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Annual cash-flow statements.</summary>
    public Task<System.Text.Json.JsonElement?> GetCashFlowAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/cashflow/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Analyst estimates (revenue / EPS / EBIT).</summary>
    public Task<System.Text.Json.JsonElement?> GetAnalystEstimatesAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"analyst/estimates/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>News and research for a ticker.</summary>
    public Task<System.Text.Json.JsonElement?> GetNewsAsync(string ticker, CancellationToken ct = default)
        => TryEulerpoolAsync($"news/{Uri.EscapeDataString(ticker)}", ct);

    /// <summary>Market-wide news.</summary>
    public Task<System.Text.Json.JsonElement?> GetMarketNewsAsync(CancellationToken ct = default)
        => TryEulerpoolAsync("news/market", ct);

    /// <summary>Latest quotes for the market.</summary>
    public Task<System.Text.Json.JsonElement?> GetLatestQuotesAsync(CancellationToken ct = default)
        => TryEulerpoolAsync("market/latest-quotes", ct);

    /// <summary>Macro economic calendar.</summary>
    public Task<System.Text.Json.JsonElement?> GetEconomicCalendarAsync(CancellationToken ct = default)
        => TryEulerpoolAsync("macro/calendar", ct);

    /// <summary>FRED series via achest macro endpoint.</summary>
    public Task<System.Text.Json.JsonElement?> GetFredSeriesAsync(string seriesId, CancellationToken ct = default)
        => TryEulerpoolAsync($"macro/fred/{Uri.EscapeDataString(seriesId)}", ct);

    /// <summary>Earnings calendar for a specific date (yyyy-MM-dd).</summary>
    public Task<System.Text.Json.JsonElement?> GetEarningsCalendarAsync(string date, CancellationToken ct = default)
        => TryEulerpoolAsync($"calendar/earnings/{Uri.EscapeDataString(date)}", ct);

    /// <summary>Crypto extended data by endpoint (top, fear-greed, ...).</summary>
    public Task<System.Text.Json.JsonElement?> GetCryptoAsync(string endpoint, CancellationToken ct = default)
        => TryEulerpoolAsync($"crypto/{endpoint.TrimStart('/')}", ct);

    /// <summary>Options &amp; derivatives data by endpoint (chain/AAPL, greeks, ...).</summary>
    public Task<System.Text.Json.JsonElement?> GetOptionsAsync(string endpoint, CancellationToken ct = default)
        => TryEulerpoolAsync($"options/{endpoint.TrimStart('/')}", ct);

    /// <summary>ETF data (profile, holdings, flows).</summary>
    public Task<System.Text.Json.JsonElement?> GetEtfAsync(string identifier, string dataType = "profile", CancellationToken ct = default)
        => TryEulerpoolAsync($"etf/{dataType}/{Uri.EscapeDataString(identifier)}", ct);

    // ── Reddit / social sentiment (Eulerpool `markets.social_sentiment`) ──

    /// <summary>
    /// Raw daily Reddit/social sentiment rows for <paramref name="ticker"/> exactly as achest
    /// returns them. <c>GET /v1/eulerpool/sentiment/social-feed/{ticker}</c> proxies Eulerpool's
    /// <c>GET /api/1/sentiment/social/{ticker}</c> (the <c>markets.social_sentiment</c> table:
    /// mentions, bullish/bearish mention counts, sentiment score and trending rank) and falls back
    /// to achest's pandas-normalised <c>GET /v1/eulerpool/sentiment/social/{ticker}</c> route when
    /// the primary route has no rows. Returns <c>null</c> when achest is unreachable.
    /// </summary>
    public async Task<System.Text.Json.JsonElement?> GetSocialSentimentRawAsync(string ticker, CancellationToken ct = default)
    {
        // NOTE: achest's route names are inverted relative to Eulerpool's own paths —
        // achest `sentiment/social-feed/{t}` -> Eulerpool `/sentiment/social/{t}`            (canonical rows)
        // achest `sentiment/social/{t}`      -> Eulerpool `/sentiment/social-sentiment/{t}`  (pandas view)
        var primary = await TryEulerpoolAsync($"sentiment/social-feed/{Uri.EscapeDataString(ticker)}", ct).ConfigureAwait(false);
        if (HasRows(primary))
        {
            return primary;
        }

        return await TryEulerpoolAsync($"sentiment/social/{Uri.EscapeDataString(ticker)}", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Daily social (Reddit) sentiment for <paramref name="ticker"/>, newest first and capped at
    /// <paramref name="days"/> rows. Each point carries mention volume, bullish/bearish mention
    /// counts, a sentiment score in [-1, 1] and Eulerpool's discussion rank (1 = most discussed).
    /// Returns an empty list when achest has no social sentiment for the ticker.
    /// </summary>
    public async Task<List<AchestSocialSentimentPoint>> GetSocialSentimentAsync(string ticker, int days = 30, CancellationToken ct = default)
    {
        var raw = await GetSocialSentimentRawAsync(ticker, ct).ConfigureAwait(false);
        var points = MapSocialSentiment(raw, ticker);
        return days > 0 && points.Count > days ? points.Take(days).ToList() : points;
    }

    /// <summary>
    /// Aggregated view of <see cref="GetSocialSentimentAsync"/> used by the CLI and the web UI:
    /// totals, latest reading, trending rank, momentum against the previous window and a
    /// Bullish/Bearish/Neutral label. The label mirrors <c>Scripts/news_pipeline.py</c>
    /// (|average score| below <see cref="SocialSentimentNeutralBand"/> counts as neutral).
    /// Returns <c>null</c> when achest has no social sentiment for the ticker.
    /// </summary>
    public async Task<AchestSocialSentimentSummary?> GetSocialSentimentSummaryAsync(string ticker, int days = 30, CancellationToken ct = default)
    {
        var points = await GetSocialSentimentAsync(ticker, days, ct).ConfigureAwait(false);
        if (points.Count == 0)
        {
            return null;
        }

        var latest = points[0];
        var averageScore = points.Average(p => p.SentimentScore);
        var totalBullish = points.Sum(p => p.BullishCount);
        var totalBearish = points.Sum(p => p.BearishCount);
        var scoredMentions = totalBullish + totalBearish;

        // Momentum: newest half of the window versus the older half.
        var recentCount = Math.Max(1, points.Count / 2);
        var recent = points.Take(recentCount).ToList();
        var previous = points.Skip(recentCount).ToList();

        var recentScore = recent.Average(p => p.SentimentScore);
        var previousScore = previous.Count > 0 ? previous.Average(p => p.SentimentScore) : recentScore;
        var scoreMomentum = recentScore - previousScore;

        var recentMentions = recent.Average(p => (double)p.MentionCount);
        var previousMentions = previous.Count > 0 ? previous.Average(p => (double)p.MentionCount) : recentMentions;
        var mentionChangePercent = previousMentions > 0
            ? ((recentMentions - previousMentions) / previousMentions) * 100
            : 0;

        return new AchestSocialSentimentSummary
        {
            Ticker = string.IsNullOrWhiteSpace(latest.Ticker) ? ticker : latest.Ticker,
            Source = latest.Source,
            DaysCovered = points.Count,
            FromDate = points[^1].Date,
            ToDate = latest.Date,
            TotalMentions = points.Sum(p => p.MentionCount),
            TotalBullish = totalBullish,
            TotalBearish = totalBearish,
            AverageSentimentScore = averageScore,
            BullishShare = scoredMentions > 0 ? (double)totalBullish / scoredMentions : 0,
            Latest = latest,
            LatestRank = latest.Rank,
            Trending = latest.Rank > 0 && latest.Rank <= SocialSentimentTrendingRankThreshold,
            SentimentLabel = LabelForSocialScore(averageScore),
            SentimentTrend = scoreMomentum > SocialSentimentMomentumBand ? "Rising"
                : scoreMomentum < -SocialSentimentMomentumBand ? "Falling"
                : "Stable",
            MentionTrend = mentionChangePercent > SocialSentimentMentionChangePercent ? "Surging"
                : mentionChangePercent < -SocialSentimentMentionChangePercent ? "Cooling"
                : "Steady",
            ScoreMomentum = scoreMomentum,
            MentionChangePercent = mentionChangePercent,
            Points = points,
        };
    }

    /// <summary>Average sentiment score inside this band is reported as neutral (±0.15).</summary>
    public const double SocialSentimentNeutralBand = 0.15;

    /// <summary>Change in average score between the two halves of a window needed to call a trend.</summary>
    public const double SocialSentimentMomentumBand = 0.05;

    /// <summary>Percentage change in average mentions between window halves needed to call it.</summary>
    public const double SocialSentimentMentionChangePercent = 25;

    /// <summary>Eulerpool ranks tickers by discussion volume; top-100 counts as trending.</summary>
    public const int SocialSentimentTrendingRankThreshold = 100;

    private static string LabelForSocialScore(double score) =>
        score > SocialSentimentNeutralBand ? "Bullish"
        : score < -SocialSentimentNeutralBand ? "Bearish"
        : "Neutral";

    private static bool HasRows(System.Text.Json.JsonElement? element) =>
        element.HasValue &&
        element.Value.ValueKind == JsonValueKind.Array &&
        element.Value.GetArrayLength() > 0;

    /// <summary>
    /// Maps achest social-sentiment rows onto typed points, newest first. Both key spellings
    /// achest exposes are accepted: the Eulerpool passthrough rows
    /// (<c>mention_count</c>/<c>bullish_count</c>/<c>bearish_count</c>/<c>sentiment_score</c>)
    /// and the pandas-normalised variant (<c>mention</c>/<c>positive_mention</c>/<c>score</c>).
    /// </summary>
    private static List<AchestSocialSentimentPoint> MapSocialSentiment(System.Text.Json.JsonElement? raw, string requestedTicker)
    {
        var points = new List<AchestSocialSentimentPoint>();
        if (!raw.HasValue || raw.Value.ValueKind != JsonValueKind.Array)
        {
            return points;
        }

        foreach (var row in raw.Value.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            points.Add(new AchestSocialSentimentPoint
            {
                Id = GetStr(row, "id"),
                Ticker = GetStr(row, "ticker") ?? requestedTicker,
                Source = GetStr(row, "source") ?? "reddit",
                Date = GetStr(row, "date") ?? GetStr(row, "at_time") ?? string.Empty,
                MentionCount = (int)(GetDbl(row, "mention_count") ?? GetDbl(row, "mention") ?? 0),
                BullishCount = (int)(GetDbl(row, "bullish_count") ?? GetDbl(row, "positive_mention") ?? 0),
                BearishCount = (int)(GetDbl(row, "bearish_count") ?? GetDbl(row, "negative_mention") ?? 0),
                SentimentScore = GetDbl(row, "sentiment_score") ?? GetDbl(row, "score") ?? 0,
                Rank = (int)(GetDbl(row, "rank") ?? 0),
                UpdatedAt = GetStr(row, "updated_at"),
            });
        }

        return points
            .OrderByDescending(p => ParseSocialSentimentDate(p.Date))
            .ToList();
    }

    private static DateTime ParseSocialSentimentDate(string? value) =>
        DateTime.TryParse(value, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : DateTime.MinValue;
}

/// <summary>Holds a cached Eulerpool response and its fetch timestamp for TTL checks.</summary>
internal class CachedEulerpoolResult
{
    public System.Text.Json.JsonElement? Result { get; set; }
    public DateTime FetchedAt { get; set; }
    public bool IsExpired => DateTime.UtcNow - FetchedAt >= AchestService.EulerpoolCacheTtl;
}

/// <summary>Analyst consensus recommendation &amp; price targets from achest.</summary>
public class AchestAnalystConsensus
{
    public string? Period { get; set; }
    public int StrongBuy { get; set; }
    public int Buy { get; set; }
    public int Hold { get; set; }
    public int Sell { get; set; }
    public int StrongSell { get; set; }
    public double? TargetMean { get; set; }
    public double? TargetMedian { get; set; }
    public double? TargetHigh { get; set; }
    public double? TargetLow { get; set; }
    public int TotalAnalysts => StrongBuy + Buy + Hold + Sell + StrongSell;
}

/// <summary>Consensus price target from achest.</summary>
public class AchestPriceTarget
{
    public string? Ticker { get; set; }
    public double? TargetHigh { get; set; }
    public double? TargetLow { get; set; }
    public double? TargetMean { get; set; }
    public double? TargetMedian { get; set; }
    public string? LastUpdated { get; set; }
}

/// <summary>Analyst-computed fair value (Eulerpool model) from achest.</summary>
public class AchestFairValue
{
    public double? FairValue { get; set; }
    public double? FairValueIncome { get; set; }
    public double? FairValueRevenue { get; set; }
    public double? FairValueDividend { get; set; }
    public double? LastPrice { get; set; }
    public double? Upside { get; set; }
}

/// <summary>A single analyst grade event from achest.</summary>
public class AchestAnalystGrade
{
    public string? Date { get; set; }
    public string? Company { get; set; }
    public string? PreviousGrade { get; set; }
    public string? NewGrade { get; set; }
    public string? Action { get; set; }
}

/// <summary>
/// One day of Reddit/social sentiment for a ticker, from achest (Eulerpool
/// <c>markets.social_sentiment</c>: <c>GET /api/1/sentiment/social/{ticker}</c>).
/// </summary>
public class AchestSocialSentimentPoint
{
    public string? Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Source { get; set; } = "reddit";

    /// <summary>Observation date (yyyy-MM-dd).</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Number of Reddit/social mentions on <see cref="Date"/>.</summary>
    public int MentionCount { get; set; }

    /// <summary>Bullish (positive) mentions on <see cref="Date"/>.</summary>
    public int BullishCount { get; set; }

    /// <summary>Bearish (negative) mentions on <see cref="Date"/>.</summary>
    public int BearishCount { get; set; }

    /// <summary>Aggregated sentiment score for the day, roughly [-1, 1].</summary>
    public double SentimentScore { get; set; }

    /// <summary>Eulerpool discussion rank for the day (1 = most discussed).</summary>
    public int Rank { get; set; }

    /// <summary>Timestamp Eulerpool last refreshed the row.</summary>
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// Aggregated Reddit/social sentiment for a ticker over a window of days, built from
/// <see cref="AchestSocialSentimentPoint"/> rows returned by achest.
/// </summary>
public class AchestSocialSentimentSummary
{
    public string Ticker { get; set; } = string.Empty;
    public string Source { get; set; } = "reddit";
    public int DaysCovered { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }

    public int TotalMentions { get; set; }
    public int TotalBullish { get; set; }
    public int TotalBearish { get; set; }

    /// <summary>Mean daily sentiment score across the window.</summary>
    public double AverageSentimentScore { get; set; }

    /// <summary>Share of scored mentions that were bullish (0-1).</summary>
    public double BullishShare { get; set; }

    /// <summary>Newest day in the window.</summary>
    public AchestSocialSentimentPoint? Latest { get; set; }

    /// <summary>Eulerpool discussion rank on the newest day (1 = most discussed).</summary>
    public int LatestRank { get; set; }

    /// <summary>True when the newest rank is inside <see cref="AchestService.SocialSentimentTrendingRankThreshold"/>.</summary>
    public bool Trending { get; set; }

    /// <summary>Bullish / Bearish / Neutral, derived from <see cref="AverageSentimentScore"/>.</summary>
    public string SentimentLabel { get; set; } = "Neutral";

    /// <summary>Rising / Falling / Stable — change in score over the window.</summary>
    public string SentimentTrend { get; set; } = "Stable";

    /// <summary>Surging / Cooling / Steady — change in mention volume over the window.</summary>
    public string MentionTrend { get; set; } = "Steady";

    /// <summary>Difference in average score between the newest and oldest half of the window.</summary>
    public double ScoreMomentum { get; set; }

    /// <summary>Percentage change in average mentions between the newest and oldest half of the window.</summary>
    public double MentionChangePercent { get; set; }

    /// <summary>Daily points, newest first.</summary>
    public List<AchestSocialSentimentPoint> Points { get; set; } = new();
}


