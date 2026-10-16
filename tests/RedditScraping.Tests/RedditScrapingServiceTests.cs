using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QuantResearchAgent.Services;
using Xunit;

namespace RedditScraping.Tests;

/// <summary>
/// Covers the two paths through <see cref="RedditScrapingService"/>: the Chocodata proxy
/// (preferred, when a key is configured) and the public Atom RSS fallback.
/// </summary>
public class RedditScrapingServiceTests
{
    private const string TestKey = "test-key-123";

    /// <summary>
    /// Chocodata's <c>reddit/subreddit</c> envelope, using the field names their docs publish
    /// (<c>posts</c>, <c>score</c>, <c>num_comments</c>, <c>upvote_ratio</c>, <c>permalink</c>).
    /// </summary>
    private const string ChocodataSubredditJson = """
{
  "subreddit": "wallstreetbets",
  "sort": "hot",
  "total_results": 2,
  "posts": [
    {
      "id": "nvda1234",
      "title": "NVDA earnings beat sends it to the moon",
      "author": "Personal_Rough6944",
      "subreddit": "wallstreetbets",
      "score": 33,
      "upvote_ratio": 0.971,
      "num_comments": 12,
      "created_utc": 1760544000,
      "permalink": "https://www.reddit.com/r/wallstreetbets/comments/nvda1234/nvda_earnings_beat/",
      "selftext": "NVDA is ripping after hours",
      "link_flair_text": "Discussed"
    },
    {
      "id": "amd5678",
      "title": "AMD follows NVDA higher",
      "author": "/u/amd_bull",
      "subreddit": "wallstreetbets",
      "score": 5,
      "num_comments": 1,
      "permalink": "/r/wallstreetbets/comments/amd5678/amd_follows/"
    }
  ]
}
""";

    /// <summary>The <c>reddit/search</c> envelope: a <c>results</c> array with a result_type per row.</summary>
    private const string ChocodataSearchJson = """
{
  "query": "NVDA",
  "subreddit": "stocks",
  "total_results": 3,
  "results": [
    { "result_type": "post", "id": "nvda9999", "title": "NVDA guidance raised", "author": "stocks_guy",
      "permalink": "https://www.reddit.com/r/stocks/comments/nvda9999/nvda_guidance/", "score": 120, "num_comments": 40 },
    { "result_type": "comment", "id": "c1", "title": "a comment row", "author": "someone", "body": "not a post" },
    { "id": "nvda8888", "title": "Wrapped in a data object", "author": "other",
      "permalink": "https://www.reddit.com/r/stocks/comments/nvda8888/wrapped/", "score": 7, "num_comments": 2,
      "data": { "id": "nvda8888", "title": "Wrapped in a data object", "author": "other",
                "permalink": "https://www.reddit.com/r/stocks/comments/nvda8888/wrapped/", "score": 7, "num_comments": 2 } }
  ]
}
""";

    /// <summary>A minimal Atom feed, shaped like Reddit's <c>/hot/.rss</c> output.</summary>
    private const string RssAtom = """
<?xml version="1.0" encoding="UTF-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <title>r/wallstreetbets</title>
  <entry>
    <title>NVDA earnings beat sends it to the moon</title>
    <author><name>/u/wsb_ape</name></author>
    <link href="https://www.reddit.com/r/wallstreetbets/comments/nvda1234/nvda_earnings_beat/" />
    <id>t3_nvda1234</id>
    <content type="html">&lt;p&gt;NVDA is ripping after hours&lt;/p&gt;</content>
    <updated>2026-10-15T12:00:00+00:00</updated>
  </entry>
</feed>
""";

    /// <summary>
    /// A verbatim capture from the live API (r/wallstreetbets, hot). It differs from the docs in two
    /// ways that matter: <c>id</c> carries the <c>t3_</c> prefix, and <c>created</c> is an ISO-8601
    /// string with a <c>+0000</c> offset rather than <c>created_utc</c> in Unix seconds.
    /// </summary>
    private const string ChocodataLiveSubredditJson = """
{"subreddit":"wallstreetbets","sort":"hot","t":null,"after_cursor":"dDNfMXg3NThocg==","total_results":2,"posts":[
{"id":"t3_1x7chad","title":"+$16,000,000 on AMD and SOXL. Sold it all.","score":2591,"num_comments":428,
 "upvote_ratio":0.9507959479015919,"awards":8,"author":"DubiousChemistry","author_id":"t2_7lbg2in9m",
 "subreddit":"wallstreetbets","permalink":"https://www.reddit.com/r/wallstreetbets/comments/1x7chad/16000000_on_amd_and_soxl_sold_it_all/",
 "external_url":null,"domain":"reddit.com","created":"2026-10-16T10:19:25.796000+0000"},
{"id":"t3_1x73hxs","title":"Tesla has officially lost the plot..","score":16651,"num_comments":6427,
 "upvote_ratio":0.8749711649365629,"author":"Boring_Board7634","subreddit":"wallstreetbets",
 "permalink":"https://www.reddit.com/r/wallstreetbets/comments/1x73hxs/tesla_has_officially_lost_the_plot/",
 "external_url":"https://v.redd.it/nevcp12igqvh1","domain":"v.redd.it","created":"2026-10-16T01:35:00.570000+0000"}
],"_source":"shreddit"}
""";

    /// <summary>The error envelope Chocodata returns for every non-2xx response.</summary>
    private static string ChocodataError(string code) =>
        $$"""{ "error": "{{code}}", "message": "explanation for {{code}}", "request_id": "req_9f2c8a1b3e4d" }""";

    /// <summary>
    /// Builds the service with an in-memory configuration. The key is only set when a test wants
    /// the Chocodata path, so the default setup exercises the RSS fallback.
    /// </summary>
    private static RedditScrapingService CreateService(FakeHttpMessageHandler handler, string chocodataKey = null)
    {
        var settings = new Dictionary<string, string>();
        if (chocodataKey != null)
        {
            settings["CHOCODATA_API_KEY"] = chocodataKey;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        return new RedditScrapingService(NullLogger<RedditScrapingService>.Instance, httpClient, configuration);
    }

    // ── Chocodata: the preferred source ──────────────────────

    [Fact]
    public async Task ScrapeSubredditAsync_uses_chocodata_when_a_key_is_configured()
    {
        var handler = new FakeHttpMessageHandler(CannedResponse.Json(ChocodataSubredditJson));
        var service = CreateService(handler, TestKey);

        var posts = await service.ScrapeSubredditAsync("wallstreetbets", 25);

        var request = Assert.Single(handler.RequestUris);
        Assert.StartsWith("https://api.chocodata.com/api/v1/reddit/subreddit?", request);
        Assert.Contains($"api_key={TestKey}", request);
        Assert.Contains("subreddit=wallstreetbets", request);
        Assert.Contains("sort=hot", request);
        Assert.Contains("limit=25", request);

        Assert.Equal(2, posts.Count);

        var top = posts[0];
        Assert.Equal("nvda1234", top.Id);
        Assert.Equal("NVDA earnings beat sends it to the moon", top.Title);
        Assert.Equal("Personal_Rough6944", top.Author);
        Assert.Equal("wallstreetbets", top.Subreddit);
        Assert.Equal("Discussed", top.Flair);
        Assert.Equal("NVDA is ripping after hours", top.Content);
        Assert.Equal("https://www.reddit.com/r/wallstreetbets/comments/nvda1234/nvda_earnings_beat/", top.Url);

        // The engagement counters are the whole point of using the proxy: RSS reports zeros.
        Assert.Equal(33, top.Score);
        Assert.Equal(12, top.Comments);

        // score 33 with upvote_ratio 0.971 inverts to 34 up / 1 down.
        Assert.Equal(34, top.Upvotes);
        Assert.Equal(1, top.Downvotes);

        // created_utc is a real UTC instant rather than "now".
        Assert.Equal(DateTimeKind.Utc, top.CreatedUtc.Kind);
        Assert.Equal(new DateTime(2025, 10, 15, 16, 0, 0, DateTimeKind.Utc), top.CreatedUtc);

        // The second row carries no upvote_ratio, so the split is skipped and only Score survives.
        var second = posts[1];
        Assert.Equal("amd5678", second.Id);
        Assert.Equal("amd_bull", second.Author);                        // "/u/" prefix stripped
        Assert.Equal(5, second.Score);
        Assert.Equal(0, second.Upvotes);
        Assert.Equal(0, second.Downvotes);
        // A path-only permalink is expanded against the www host.
        Assert.Equal("https://www.reddit.com/r/wallstreetbets/comments/amd5678/amd_follows/", second.Url);
    }

    // ── Falling back to RSS ──────────────────────────────────

    [Fact]
    public async Task ScrapeSubredditAsync_falls_back_to_rss_when_chocodata_rejects_the_key()
    {
        // 401 unauthorized is documented as permanent: retrying it would burn three timeouts.
        var handler = new FakeHttpMessageHandler(
            CannedResponse.Json(ChocodataError("unauthorized")) with { StatusCode = HttpStatusCode.Unauthorized },
            CannedResponse.Xml(RssAtom));
        var service = CreateService(handler, TestKey);

        var posts = await service.ScrapeSubredditAsync("wallstreetbets", 25);

        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("/reddit/subreddit", handler.RequestUris[0]);
        Assert.Contains("wallstreetbets/hot/.rss", handler.RequestUris[1]);
        Assert.Equal(1, handler.CountRequestsContaining("chocodata"));   // not retried

        // The RSS row still comes back, and still has no engagement counters.
        var post = Assert.Single(posts);
        Assert.Equal("nvda1234", post.Id);
        Assert.Equal("wsb_ape", post.Author);
        Assert.Equal(0, post.Score);
    }

    [Fact]
    public async Task ScrapeSubredditAsync_retries_a_transient_chocodata_error_then_succeeds()
    {
        // 408 upstream_timeout is retryable, and the documented backoff for it is only 2s.
        var handler = new FakeHttpMessageHandler(
            CannedResponse.Json(ChocodataError("upstream_timeout")) with { StatusCode = HttpStatusCode.RequestTimeout },
            CannedResponse.Json(ChocodataSubredditJson));
        var service = CreateService(handler, TestKey);

        var posts = await service.ScrapeSubredditAsync("wallstreetbets", 25);

        Assert.Equal(2, handler.RequestCount);                            // retried once
        Assert.Equal(2, handler.CountRequestsContaining("chocodata"));
        Assert.DoesNotContain(handler.RequestUris, uri => uri.Contains(".rss"));
        Assert.Equal(33, posts[0].Score);
    }

    [Fact]
    public async Task ScrapeSubredditAsync_falls_back_to_rss_when_chocodata_replies_with_unparseable_json()
    {
        // A 2xx that is not JSON has already been billed, so retrying would pay twice; the RSS feed
        // is used instead.
        var handler = new FakeHttpMessageHandler(
            CannedResponse.Json("<html>not json at all</html>"),
            CannedResponse.Xml(RssAtom));
        var service = CreateService(handler, TestKey);

        var posts = await service.ScrapeSubredditAsync("wallstreetbets", 25);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(1, handler.CountRequestsContaining("chocodata"));   // a 2xx is never retried
        Assert.Contains("wallstreetbets/hot/.rss", handler.RequestUris[1]);
        Assert.Single(posts);
    }

    [Fact]
    public async Task ScrapeSubredditAsync_uses_only_rss_when_no_key_is_configured()
    {
        var handler = new FakeHttpMessageHandler(CannedResponse.Xml(RssAtom));
        var service = CreateService(handler);

        var posts = await service.ScrapeSubredditAsync("wallstreetbets", 25);

        var request = Assert.Single(handler.RequestUris);
        Assert.Contains("www.reddit.com/r/wallstreetbets/hot/.rss", request);
        Assert.DoesNotContain("chocodata", request);
        Assert.Single(posts);
    }

    // ── Search ──────────────────────────────────────────────

    [Fact]
    public async Task SearchSubredditAsync_uses_the_search_endpoint_when_a_key_is_configured()
    {
        var handler = new FakeHttpMessageHandler(CannedResponse.Json(ChocodataSearchJson));
        var service = CreateService(handler, TestKey);

        var posts = await service.SearchSubredditAsync("stocks", "NVDA", 5);

        var request = Assert.Single(handler.RequestUris);
        Assert.StartsWith("https://api.chocodata.com/api/v1/reddit/search?", request);
        Assert.Contains("q=NVDA", request);
        Assert.Contains("subreddit=stocks", request);
        Assert.Contains("sort=relevance", request);
        Assert.Contains("t=day", request);
        Assert.Contains("limit=5", request);

        // The comment row is dropped and the data-wrapped post is unwrapped, leaving both posts.
        Assert.Equal(2, posts.Count);
        Assert.Equal("nvda9999", posts[0].Id);
        Assert.Equal(120, posts[0].Score);
        Assert.Equal(40, posts[0].Comments);
        Assert.Equal("nvda8888", posts[1].Id);
        Assert.Equal(7, posts[1].Score);
    }

    // ── Envelope handling ────────────────────────────────────

    [Fact]
    public void ParseChocodataResponse_maps_the_live_subreddit_payload()
    {
        var posts = RedditScrapingService.ParseChocodataResponse(ChocodataLiveSubredditJson, "wallstreetbets");

        Assert.Equal(2, posts.Count);

        var top = posts[0];
        Assert.Equal("1x7chad", top.Id);                       // the "t3_" fullname prefix is stripped
        Assert.Equal("+$16,000,000 on AMD and SOXL. Sold it all.", top.Title);
        Assert.Equal("DubiousChemistry", top.Author);
        Assert.Equal(2591, top.Score);
        Assert.Equal(428, top.Comments);
        Assert.Equal("", top.Content);                         // the listing carries no selftext

        // The up/down split must stay consistent with the score and the published ratio.
        Assert.Equal(top.Score, top.Upvotes - top.Downvotes);
        Assert.Equal(0.95, (double)top.Upvotes / (top.Upvotes + top.Downvotes), 2);

        // "2026-10-16T10:19:25.796000+0000" has to land as that instant in UTC rather than "now".
        Assert.Equal(new DateTime(2026, 10, 16, 10, 19, 25, 796, DateTimeKind.Utc), top.CreatedUtc);
        Assert.Equal(new DateTime(2026, 10, 16, 1, 35, 0, 570, DateTimeKind.Utc), posts[1].CreatedUtc);

        // external_url and domain are ignored: the thread permalink is what callers want.
        Assert.Equal("https://www.reddit.com/r/wallstreetbets/comments/1x73hxs/tesla_has_officially_lost_the_plot/", posts[1].Url);
    }

    [Fact]
    public void ParseChocodataResponse_handles_reddits_native_children_shape()
    {
        const string json = """
{ "data": { "children": [ { "kind": "t3", "data": {
    "id": "t3_nvda1234", "title": "Native shape", "author": "ape", "subreddit": "wallstreetbets",
    "score": 9, "num_comments": 3, "permalink": "/r/wallstreetbets/comments/nvda1234/native/" } } ] } }
""";

        var post = Assert.Single(RedditScrapingService.ParseChocodataResponse(json, "wallstreetbets"));

        Assert.Equal("nvda1234", post.Id);                             // t3_ stripped, not the "children" key
        Assert.Equal(9, post.Score);
        Assert.Equal(3, post.Comments);
        Assert.Equal("https://www.reddit.com/r/wallstreetbets/comments/nvda1234/native/", post.Url);
    }

    [Fact]
    public void ParseChocodataResponse_returns_empty_for_an_unrecognised_shape()
    {
        Assert.Empty(RedditScrapingService.ParseChocodataResponse("""{ "unexpected": true }""", "wallstreetbets"));
        Assert.Empty(RedditScrapingService.ParseChocodataResponse("", "wallstreetbets"));
    }

    [Fact]
    public void ParseChocodataResponse_skips_the_vote_split_when_the_ratio_cannot_be_inverted()
    {
        // up = score × ratio / (2 × ratio − 1) has a non-positive denominator at or below 0.5.
        const string json = """
{ "posts": [ { "id": "abc123", "title": "Controversial post", "score": -3, "upvote_ratio": 0.42, "num_comments": 8 } ] }
""";

        var post = Assert.Single(RedditScrapingService.ParseChocodataResponse(json, "wallstreetbets"));

        Assert.Equal(-3, post.Score);
        Assert.Equal(8, post.Comments);
        Assert.Equal(0, post.Upvotes);
        Assert.Equal(0, post.Downvotes);
    }
}
