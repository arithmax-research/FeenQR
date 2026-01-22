using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Plugins;
using System.Text;
using System.Text.Json;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Advanced news sentiment analysis service that analyzes financial news 
    /// from multiple sources and provides comprehensive sentiment insights
    /// </summary>
    public class NewsSentimentAnalysisService
    {
        private readonly ILogger<NewsSentimentAnalysisService> _logger;
        private readonly OpenAIService _openAIService;
        private readonly YFinanceNewsService _yfinanceNewsService;
        private readonly FinvizNewsService _finvizNewsService;
        private readonly RedditScrapingService _redditScrapingService;
        private readonly GoogleWebSearchPlugin _googleSearchPlugin;
        private readonly HttpClient _httpClient;

        public NewsSentimentAnalysisService(
            ILogger<NewsSentimentAnalysisService> logger,
            OpenAIService openAIService,
            YFinanceNewsService yfinanceNewsService,
            FinvizNewsService finvizNewsService,
            RedditScrapingService redditScrapingService,
            GoogleWebSearchPlugin googleSearchPlugin,
            HttpClient httpClient)
        {
            _logger = logger;
            _openAIService = openAIService;
            _yfinanceNewsService = yfinanceNewsService;
            _finvizNewsService = finvizNewsService;
            _redditScrapingService = redditScrapingService;
            _googleSearchPlugin = googleSearchPlugin;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Analyze sentiment for a specific symbol using multi-source news
        /// </summary>
        public async Task<SymbolSentimentAnalysis> AnalyzeSymbolSentimentAsync(string symbol, int newsLimit = 10)
        {
            try
            {
                _logger.LogInformation($"Starting sentiment analysis for {symbol}");

                // Gather news from multiple sources IN PARALLEL for speed
                var allNews = new List<NewsItem>();
                
                // Google Search for latest news
                var googleTask = Task.Run(async () =>
                {
                    try
                    {
                        var searchQuery = $"{symbol} stock news latest";
                        var googleResults = await _googleSearchPlugin.SearchAsync(searchQuery, maxResults: newsLimit);
                        var items = googleResults.Select(gr => new NewsItem
                        {
                            Title = gr.Title ?? "",
                            Summary = gr.Snippet ?? "",
                            Publisher = "Google Search",
                            PublishedDate = DateTime.Now,
                            Link = gr.Url ?? "",
                            Source = "Google Search"
                        }).ToList();
                        _logger.LogInformation($"Retrieved {items.Count} news items from Google Search");
                        return items;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to get Google Search results for {symbol}");
                        return new List<NewsItem>();
                    }
                });

                var finvizTask = Task.Run(async () =>
                {
                    try
                    {
                        var finvizNews = await _finvizNewsService.GetNewsAsync(symbol, newsLimit);
                        var items = finvizNews.Select(fn => new NewsItem
                        {
                            Title = fn.Title,
                            Summary = fn.Summary,
                            Publisher = fn.Publisher,
                            PublishedDate = fn.PublishedDate,
                            Link = fn.Link,
                            Source = "Finviz"
                        }).ToList();
                        _logger.LogInformation($"Retrieved {items.Count} news items from Finviz");
                        return items;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to get Finviz news for {symbol}");
                        return new List<NewsItem>();
                    }
                });

                var redditTask = Task.Run(async () =>
                {
                    try
                    {
                        var redditPosts = await _redditScrapingService.ScrapeSubredditAsync("wallstreetbets", newsLimit);
                        var symbolPosts = redditPosts.Where(p => 
                            p.Title.Contains(symbol, StringComparison.OrdinalIgnoreCase) ||
                            p.Content.Contains(symbol, StringComparison.OrdinalIgnoreCase))
                            .Take(newsLimit / 2);
                        
                        var items = symbolPosts.Select(rp => new NewsItem
                        {
                            Title = rp.Title,
                            Summary = rp.Content.Length > 200 ? rp.Content.Substring(0, 200) + "..." : rp.Content,
                            Publisher = $"u/{rp.Author}",
                            PublishedDate = rp.CreatedUtc,
                            Link = rp.Url,
                            Source = "Reddit"
                        }).ToList();
                        _logger.LogInformation($"Retrieved {items.Count} relevant posts from Reddit");
                        return items;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to get Reddit posts for {symbol}");
                        return new List<NewsItem>();
                    }
                });

                // Yahoo Finance news
                var yahooTask = Task.Run(async () =>
                {
                    try
                    {
                        var yahooNews = await _yfinanceNewsService.GetNewsAsync(symbol, newsLimit);
                        var items = yahooNews.Select(yn => new NewsItem
                        {
                            Title = yn.Title,
                            Summary = yn.Summary,
                            Publisher = yn.Publisher,
                            PublishedDate = yn.PublishedDate,
                            Link = yn.Link,
                            Source = "Yahoo Finance"
                        }).ToList();
                        _logger.LogInformation($"Retrieved {items.Count} news items from Yahoo Finance");
                        return items;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to get Yahoo Finance news for {symbol}");
                        return new List<NewsItem>();
                    }
                });

                // MarketWatch news scraping
                var marketWatchTask = Task.Run(async () =>
                {
                    try
                    {
                        var url = $"https://www.marketwatch.com/search?q={symbol}&ts=0&tab=All%20News";
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var html = await response.Content.ReadAsStringAsync();
                            var items = new List<NewsItem>();
                            
                            // Simple regex parsing for MarketWatch articles
                            var titlePattern = new System.Text.RegularExpressions.Regex(@"<h3[^>]*class=""[^""]*article__headline[^""]*""[^>]*>\s*<a[^>]*href=""([^""]*)""[^>]*>([^<]*)</a>");
                            var matches = titlePattern.Matches(html);
                            
                            foreach (System.Text.RegularExpressions.Match match in matches.Take(newsLimit))
                            {
                                if (match.Groups.Count >= 3)
                                {
                                    items.Add(new NewsItem
                                    {
                                        Title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim()),
                                        Summary = "",
                                        Publisher = "MarketWatch",
                                        PublishedDate = DateTime.Now,
                                        Link = match.Groups[1].Value.StartsWith("http") ? match.Groups[1].Value : $"https://www.marketwatch.com{match.Groups[1].Value}",
                                        Source = "MarketWatch"
                                    });
                                }
                            }
                            _logger.LogInformation($"Retrieved {items.Count} news items from MarketWatch");
                            return items;
                        }
                        return new List<NewsItem>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to scrape MarketWatch news for {symbol}");
                        return new List<NewsItem>();
                    }
                });

                // Wait for all sources to complete in parallel
                await Task.WhenAll(googleTask, finvizTask, redditTask, yahooTask, marketWatchTask);
                
                allNews.AddRange(googleTask.Result);
                allNews.AddRange(finvizTask.Result);
                allNews.AddRange(redditTask.Result);
                allNews.AddRange(yahooTask.Result);
                allNews.AddRange(marketWatchTask.Result);

                if (allNews.Count == 0)
                {
                    _logger.LogWarning($"No news items found for {symbol} from any source");
                    return new SymbolSentimentAnalysis
                    {
                        Symbol = symbol,
                        AnalysisDate = DateTime.Now,
                        NewsItems = new List<NewsItem>(),
                        OverallSentiment = "Neutral",
                        SentimentScore = 0,
                        Confidence = 0,
                        Summary = "No news data available for analysis"
                    };
                }

                _logger.LogInformation($"Total news items collected: {allNews.Count} from multiple sources");

                // Remove duplicates and sort by date
                var uniqueNews = allNews
                    .GroupBy(n => n.Title.ToLower().Trim())
                    .Select(g => g.OrderByDescending(n => n.PublishedDate).First())
                    .OrderByDescending(n => n.PublishedDate)
                    .Take(newsLimit)
                    .ToList();

                // BATCH analyze sentiment for all news items in one call for speed
                var sentimentResults = await AnalyzeBatchSentimentAsync(uniqueNews);

                // Combine results
                for (int i = 0; i < uniqueNews.Count && i < sentimentResults.Count; i++)
                {
                    uniqueNews[i].SentimentScore = sentimentResults[i].Score;
                    uniqueNews[i].SentimentLabel = sentimentResults[i].Label;
                    uniqueNews[i].KeyTopics = sentimentResults[i].KeyTopics;
                    uniqueNews[i].Impact = sentimentResults[i].Impact;
                }

                // Generate overall analysis
                var overallAnalysis = await GenerateOverallSentimentAnalysisAsync(symbol, uniqueNews);

                return new SymbolSentimentAnalysis
                {
                    Symbol = symbol,
                    AnalysisDate = DateTime.Now,
                    NewsItems = uniqueNews,
                    OverallSentiment = overallAnalysis.OverallSentiment,
                    SentimentScore = overallAnalysis.SentimentScore,
                    Confidence = overallAnalysis.Confidence,
                    TrendDirection = overallAnalysis.TrendDirection,
                    KeyThemes = overallAnalysis.KeyThemes,
                    TradingSignal = overallAnalysis.TradingSignal,
                    RiskFactors = overallAnalysis.RiskFactors,
                    Summary = overallAnalysis.Summary,
                    VolatilityIndicator = overallAnalysis.VolatilityIndicator,
                    PriceTargetBias = overallAnalysis.PriceTargetBias,
                    InstitutionalSentiment = overallAnalysis.InstitutionalSentiment,
                    RetailSentiment = overallAnalysis.RetailSentiment,
                    AnalystConsensus = overallAnalysis.AnalystConsensus,
                    EarningsImpact = overallAnalysis.EarningsImpact,
                    SectorComparison = overallAnalysis.SectorComparison,
                    MomentumSignal = overallAnalysis.MomentumSignal
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing sentiment for {symbol}");
                throw;
            }
        }

        /// <summary>
        /// Analyze market-wide sentiment using general financial news
        /// </summary>
        public async Task<MarketSentimentAnalysis> AnalyzeMarketSentimentAsync(int newsLimit = 20)
        {
            try
            {
                _logger.LogInformation("Starting market-wide sentiment analysis");

                // Get market news from both sources
                var yahooNewsTask = _yfinanceNewsService.GetMarketNewsAsync(newsLimit);
                var finvizNewsTask = _finvizNewsService.GetMarketNewsAsync(newsLimit);

                await Task.WhenAll(yahooNewsTask, finvizNewsTask);

                var yahooNews = yahooNewsTask.Result;
                var finvizNews = finvizNewsTask.Result;

                // Combine all news
                var allNews = new List<NewsItem>();
                
                allNews.AddRange(yahooNews.Select(yn => new NewsItem
                {
                    Title = yn.Title,
                    Summary = yn.Summary,
                    Publisher = yn.Publisher,
                    PublishedDate = yn.PublishedDate,
                    Link = yn.Link,
                    Source = "Yahoo Finance"
                }));

                allNews.AddRange(finvizNews.Select(fn => new NewsItem
                {
                    Title = fn.Title,
                    Summary = fn.Summary,
                    Publisher = fn.Publisher,
                    PublishedDate = fn.PublishedDate,
                    Link = fn.Link,
                    Source = "Finviz"
                }));

                // Remove duplicates and analyze sentiment
                var uniqueNews = allNews
                    .GroupBy(n => n.Title.ToLower().Trim())
                    .Select(g => g.First())
                    .OrderByDescending(n => n.PublishedDate)
                    .Take(newsLimit)
                    .ToList();

                // Analyze sentiment for each news item
                var sentimentTasks = uniqueNews.Select(AnalyzeNewsItemSentimentAsync).ToList();
                var sentimentResults = await Task.WhenAll(sentimentTasks);

                for (int i = 0; i < uniqueNews.Count; i++)
                {
                    uniqueNews[i].SentimentScore = sentimentResults[i].Score;
                    uniqueNews[i].SentimentLabel = sentimentResults[i].Label;
                    uniqueNews[i].KeyTopics = sentimentResults[i].KeyTopics;
                    uniqueNews[i].Impact = sentimentResults[i].Impact;
                }

                // Generate market analysis
                var marketAnalysis = await GenerateMarketSentimentAnalysisAsync(uniqueNews);

                return new MarketSentimentAnalysis
                {
                    AnalysisDate = DateTime.Now,
                    NewsItems = uniqueNews,
                    OverallSentiment = marketAnalysis.OverallSentiment,
                    SentimentScore = marketAnalysis.SentimentScore,
                    Confidence = marketAnalysis.Confidence,
                    MarketMood = marketAnalysis.MarketMood,
                    SectorSentiments = marketAnalysis.SectorSentiments,
                    KeyThemes = marketAnalysis.KeyThemes,
                    RiskLevel = marketAnalysis.RiskLevel,
                    Summary = marketAnalysis.Summary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing market sentiment");
                throw;
            }
        }

        /// <summary>
        /// Get sentiment-analyzed news for a specific symbol
        /// </summary>
        public async Task<List<NewsItem>> GetSentimentAnalyzedNewsAsync(string symbol, int limit = 10)
        {
            try
            {
                _logger.LogInformation($"Getting sentiment-analyzed news for {symbol}");

                // Get news from multiple sources
                var yahooNewsTask = _yfinanceNewsService.GetNewsAsync(symbol, limit);
                var finvizNewsTask = _finvizNewsService.GetNewsAsync(symbol, limit);

                await Task.WhenAll(yahooNewsTask, finvizNewsTask);

                var yahooNews = yahooNewsTask.Result;
                var finvizNews = finvizNewsTask.Result;

                // Convert to common NewsItem type and combine
                var yahooNewsItems = yahooNews.Select(yn => new NewsItem
                {
                    Title = yn.Title,
                    Summary = yn.Summary,
                    Publisher = yn.Publisher,
                    Link = yn.Link,
                    PublishedDate = yn.PublishedDate,
                    Source = "Yahoo Finance"
                }).ToList();

                var finvizNewsItems = finvizNews.Select(fn => new NewsItem
                {
                    Title = fn.Title,
                    Summary = fn.Summary,
                    Publisher = fn.Publisher,
                    Link = fn.Link,
                    PublishedDate = fn.PublishedDate,
                    Source = "Finviz"
                }).ToList();

                // Combine and deduplicate news
                var allNews = yahooNewsItems.Concat(finvizNewsItems).ToList();
                var uniqueNews = allNews
                    .GroupBy(n => n.Title)
                    .Select(g => g.First())
                    .Take(limit)
                    .ToList();

                // Analyze sentiment for each news item
                foreach (var newsItem in uniqueNews)
                {
                    var sentiment = await AnalyzeNewsItemSentimentAsync(newsItem);
                    newsItem.SentimentScore = sentiment.Score;
                    newsItem.SentimentLabel = sentiment.Label;
                    newsItem.KeyTopics = sentiment.KeyTopics;
                    newsItem.Impact = sentiment.Impact;
                }

                return uniqueNews;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting sentiment-analyzed news for {symbol}");
                return new List<NewsItem>();
            }
        }

        /// <summary>
        /// Fetch full article content from URL
        /// </summary>
        private async Task<string?> FetchArticleContentAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var html = await response.Content.ReadAsStringAsync();
                
                // Extract main content using simple heuristics
                // Remove script and style tags
                html = System.Text.RegularExpressions.Regex.Replace(html, @"<script[^>]*>.*?</script>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                html = System.Text.RegularExpressions.Regex.Replace(html, @"<style[^>]*>.*?</style>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                // Try to find main article content (common patterns)
                var articlePatterns = new[] { 
                    @"<article[^>]*>(.*?)</article>",
                    @"<div[^>]*class=[""'][^""']*article[^""']*[""'][^>]*>(.*?)</div>",
                    @"<div[^>]*class=[""'][^""']*content[^""']*[""'][^>]*>(.*?)</div>",
                    @"<div[^>]*class=[""'][^""']*story[^""']*[""'][^>]*>(.*?)</div>"
                };
                
                string? articleContent = null;
                foreach (var pattern in articlePatterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(html, pattern, System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        articleContent = match.Groups[1].Value;
                        break;
                    }
                }
                
                // If no specific article tag found, use body content
                if (string.IsNullOrEmpty(articleContent))
                {
                    var bodyMatch = System.Text.RegularExpressions.Regex.Match(html, @"<body[^>]*>(.*?)</body>", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    articleContent = bodyMatch.Success ? bodyMatch.Groups[1].Value : html;
                }
                
                // Strip all HTML tags
                articleContent = System.Text.RegularExpressions.Regex.Replace(articleContent, @"<[^>]+>", " ");
                
                // Decode HTML entities
                articleContent = System.Net.WebUtility.HtmlDecode(articleContent);
                
                // Clean up whitespace
                articleContent = System.Text.RegularExpressions.Regex.Replace(articleContent, @"\s+", " ");
                articleContent = articleContent.Trim();
                
                // Limit to first 2000 chars to avoid token limits while getting substantial content
                if (articleContent.Length > 2000)
                    articleContent = articleContent.Substring(0, 2000) + "...";
                
                return string.IsNullOrWhiteSpace(articleContent) ? null : articleContent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to fetch article content from {url}");
                return null;
            }
        }

        private async Task<List<NewsItemSentiment>> AnalyzeBatchSentimentAsync(List<NewsItem> newsItems)
        {
            try
            {
                if (newsItems.Count == 0)
                    return new List<NewsItemSentiment>();

                _logger.LogInformation($"Fetching full article content for {newsItems.Count} items...");
                
                // Fetch article content in parallel (with timeout per article)
                var contentTasks = newsItems.Select(async n => 
                {
                    if (string.IsNullOrEmpty(n.Link))
                        return (n, (string?)null);
                    
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        var content = await FetchArticleContentAsync(n.Link);
                        return (n, content);
                    }
                    catch
                    {
                        return (n, (string?)null);
                    }
                }).ToList();
                
                var contentResults = await Task.WhenAll(contentTasks);
                var articleContents = contentResults.ToDictionary(r => r.Item1, r => r.Item2);
                
                var fetchedCount = articleContents.Count(kv => !string.IsNullOrEmpty(kv.Value));
                _logger.LogInformation($"Successfully fetched {fetchedCount}/{newsItems.Count} full articles");

                var newsItemsJson = newsItems.Select((n, i) =>
                {
                    var content = articleContents.TryGetValue(n, out var c) && !string.IsNullOrEmpty(c) 
                        ? c 
                        : n.Summary;
                    
                    return $@"
Item {i + 1}:
Title: {n.Title}
Publisher: {n.Publisher}
Date: {n.PublishedDate:yyyy-MM-dd}
Content: {content}
Source URL: {n.Link}";
                }).ToList();

                var prompt = $@"
Analyze the financial sentiment of these {newsItems.Count} news articles in a SINGLE batch response.
Each article includes the FULL CONTENT (not just summary) for comprehensive analysis.

{string.Join("\n\n", newsItemsJson)}

Provide DEEP analysis for ALL articles based on the full content in this exact JSON array format:
[
    {{
        ""score"": <number between -1.0 and 1.0>,
        ""label"": ""<Very Negative|Negative|Neutral|Positive|Very Positive>"",
        ""keyTopics"": [""topic1"", ""topic2"", ""topic3""],
        ""impact"": ""<Low|Medium|High>"",
        ""reasoning"": ""<comprehensive explanation based on full article analysis>""
    }},
    ... (repeat for all {newsItems.Count} items in order)
]

Perform COMPREHENSIVE analysis considering:
- Full article context and narrative flow
- Financial metrics and quantitative data mentioned
- Expert quotes and analyst opinions with specific details
- Market impact indicators (earnings beats/misses, guidance, partnerships)
- Forward-looking statements and growth projections
- Risk factors and challenges discussed
- Competitive positioning and market share insights
- Regulatory, legal, or compliance issues
- Sentiment shifts throughout the article
- Technical language indicating bullish/bearish positioning

Provide nuanced scores reflecting the depth of content, not just headline sentiment.
";

                var jsonResponse = await _openAIService.GetChatCompletionAsync(prompt);
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                
                if (string.IsNullOrWhiteSpace(cleanJson))
                {
                    _logger.LogWarning("No JSON extracted from OpenAI response");
                    throw new Exception("Failed to extract JSON from response");
                }
                
                _logger.LogInformation($"Extracted complete JSON response: {cleanJson}");
                
                var sentimentArray = JsonSerializer.Deserialize<JsonElement>(cleanJson);

                var results = new List<NewsItemSentiment>();
                if (sentimentArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in sentimentArray.EnumerateArray())
                    {
                        results.Add(new NewsItemSentiment
                        {
                            Score = item.TryGetProperty("score", out var score) ? score.GetDouble() : 0.0,
                            Label = item.TryGetProperty("label", out var label) ? label.GetString() ?? "Neutral" : "Neutral",
                            KeyTopics = item.TryGetProperty("keyTopics", out var topics) 
                                ? topics.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList()
                                : new List<string>(),
                            Impact = item.TryGetProperty("impact", out var impact) ? impact.GetString() ?? "Medium" : "Medium"
                        });
                    }
                }

                // Fill in any missing results with neutral sentiment
                while (results.Count < newsItems.Count)
                {
                    results.Add(new NewsItemSentiment
                    {
                        Score = 0.0,
                        Label = "Neutral",
                        KeyTopics = new List<string>(),
                        Impact = "Medium"
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in batch sentiment analysis, falling back to neutral");
                return newsItems.Select(_ => new NewsItemSentiment
                {
                    Score = 0.0,
                    Label = "Neutral",
                    KeyTopics = new List<string>(),
                    Impact = "Medium"
                }).ToList();
            }
        }

        private async Task<NewsItemSentiment> AnalyzeNewsItemSentimentAsync(NewsItem newsItem)
        {
            try
            {
                var prompt = $@"
Analyze the financial sentiment of this news article:

Title: {newsItem.Title}
Summary: {newsItem.Summary}
Publisher: {newsItem.Publisher}
Date: {newsItem.PublishedDate:yyyy-MM-dd}

Provide analysis in this exact JSON format:
{{
    ""score"": <number between -1.0 and 1.0>,
    ""label"": ""<Very Negative|Negative|Neutral|Positive|Very Positive>"",
    ""keyTopics"": [""topic1"", ""topic2"", ""topic3""],
    ""impact"": ""<Low|Medium|High>"",
    ""reasoning"": ""<brief explanation>""
}}

Consider:
- Financial keywords (earnings, revenue, growth, losses, etc.)
- Market impact words (surge, plummet, rise, fall, etc.)
- Forward-looking statements
- Analyst opinions and price targets
- Company performance indicators
";


                var jsonResponse = await _openAIService.GetChatCompletionAsync(prompt);

                // Clean and parse JSON
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                var sentimentData = JsonSerializer.Deserialize<JsonElement>(cleanJson);

                return new NewsItemSentiment
                {
                    Score = sentimentData.GetProperty("score").GetDouble(),
                    Label = sentimentData.GetProperty("label").GetString() ?? "Neutral",
                    KeyTopics = sentimentData.GetProperty("keyTopics").EnumerateArray()
                        .Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Impact = sentimentData.GetProperty("impact").GetString() ?? "Medium"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error analyzing sentiment for news item: {newsItem.Title}");
                return new NewsItemSentiment
                {
                    Score = 0.0,
                    Label = "Neutral",
                    KeyTopics = new List<string>(),
                    Impact = "Medium"
                };
            }
        }

        private async Task<OverallSentimentResult> GenerateOverallSentimentAnalysisAsync(string symbol, List<NewsItem> newsItems)
        {
            var prompt = $@"
As a quantitative financial analyst, provide comprehensive sentiment analysis for {symbol} based on these news articles:

{string.Join("\n\n", newsItems.Take(10).Select(n => $"Title: {n.Title}\nSentiment: {n.SentimentLabel} ({n.SentimentScore:F2})\nTopics: {string.Join(", ", n.KeyTopics)}\nImpact: {n.Impact}"))}

Provide QUANTITATIVE analysis in this JSON format:
{{
    ""overallSentiment"": ""<Very Negative|Negative|Neutral|Positive|Very Positive>"",
    ""sentimentScore"": <-1.0 to 1.0>,
    ""confidence"": <0.0 to 1.0>,
    ""trendDirection"": ""<Improving|Stable|Declining>"",
    ""keyThemes"": [""theme1"", ""theme2"", ""theme3""],
    ""tradingSignal"": ""<Strong Buy|Buy|Hold|Sell|Strong Sell>"",
    ""riskFactors"": [""risk1"", ""risk2""],
    ""summary"": ""<detailed quantitative summary>"",
    ""volatilityIndicator"": ""<Low|Medium|High>"",
    ""priceTargetBias"": ""<Bullish|Neutral|Bearish>"",
    ""institutionalSentiment"": ""<Positive|Neutral|Negative>"",
    ""retailSentiment"": ""<Positive|Neutral|Negative>"",
    ""analystConsensus"": ""<Buy|Hold|Sell>"",
    ""earningsImpact"": ""<Positive|Neutral|Negative>"",
    ""sectorComparison"": ""<Outperform|Inline|Underperform>"",
    ""momentumSignal"": ""<Strong Positive|Positive|Neutral|Negative|Strong Negative>""
}}

Focus on:
- Quantitative metrics and financial ratios mentioned
- Analyst price targets and revisions
- Earnings estimates and guidance changes
- Institutional activity (insider trading, institutional buying/selling)
- Technical indicators mentioned in news
- Sector rotation and relative performance
- Options flow and volatility expectations
- Macroeconomic factors affecting the stock
- Revenue/profit margin trends
- Market share and competitive positioning";

            var jsonResponse = await _openAIService.GetChatCompletionAsync(prompt);
            var jsonClean = ExtractJsonFromResponse(jsonResponse);
            var data = JsonSerializer.Deserialize<JsonElement>(jsonClean);

            return new OverallSentimentResult
            {
                OverallSentiment = data.GetProperty("overallSentiment").GetString() ?? "Neutral",
                SentimentScore = data.GetProperty("sentimentScore").GetDouble(),
                Confidence = data.GetProperty("confidence").GetDouble(),
                TrendDirection = data.GetProperty("trendDirection").GetString() ?? "Stable",
                KeyThemes = data.GetProperty("keyThemes").EnumerateArray()
                    .Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList(),
                TradingSignal = data.GetProperty("tradingSignal").GetString() ?? "Hold",
                RiskFactors = data.GetProperty("riskFactors").EnumerateArray()
                    .Select(r => r.GetString() ?? "").Where(r => !string.IsNullOrEmpty(r)).ToList(),
                Summary = data.GetProperty("summary").GetString() ?? "",
                VolatilityIndicator = data.TryGetProperty("volatilityIndicator", out var vol) ? vol.GetString() ?? "Medium" : "Medium",
                PriceTargetBias = data.TryGetProperty("priceTargetBias", out var pt) ? pt.GetString() ?? "Neutral" : "Neutral",
                InstitutionalSentiment = data.TryGetProperty("institutionalSentiment", out var inst) ? inst.GetString() ?? "Neutral" : "Neutral",
                RetailSentiment = data.TryGetProperty("retailSentiment", out var retail) ? retail.GetString() ?? "Neutral" : "Neutral",
                AnalystConsensus = data.TryGetProperty("analystConsensus", out var analyst) ? analyst.GetString() ?? "Hold" : "Hold",
                EarningsImpact = data.TryGetProperty("earningsImpact", out var earnings) ? earnings.GetString() ?? "Neutral" : "Neutral",
                SectorComparison = data.TryGetProperty("sectorComparison", out var sector) ? sector.GetString() ?? "Inline" : "Inline",
                MomentumSignal = data.TryGetProperty("momentumSignal", out var momentum) ? momentum.GetString() ?? "Neutral" : "Neutral"
            };
        }

        private async Task<MarketSentimentResult> GenerateMarketSentimentAnalysisAsync(List<NewsItem> newsItems)
        {
            var prompt = $@"
Analyze overall market sentiment based on these financial news articles:

{string.Join("\n\n", newsItems.Take(15).Select(n => $"Title: {n.Title}\nSentiment: {n.SentimentLabel} ({n.SentimentScore:F2})"))}

Provide analysis in this JSON format:
{{
    ""overallSentiment"": ""<Very Negative|Negative|Neutral|Positive|Very Positive>"",
    ""sentimentScore"": <-1.0 to 1.0>,
    ""confidence"": <0.0 to 1.0>,
    ""marketMood"": ""<Fear|Caution|Neutral|Optimism|Euphoria>"",
    ""sectorSentiments"": {{
        ""technology"": <-1.0 to 1.0>,
        ""finance"": <-1.0 to 1.0>,
        ""healthcare"": <-1.0 to 1.0>
    }},
    ""keyThemes"": [""theme1"", ""theme2"", ""theme3""],
    ""riskLevel"": ""<Low|Medium|High>"",
    ""summary"": ""<2-3 sentence market summary>""
}}";

            var jsonResponse = await _openAIService.GetChatCompletionAsync(prompt);
            var jsonClean = ExtractJsonFromResponse(jsonResponse);
            var data = JsonSerializer.Deserialize<JsonElement>(jsonClean);

            var sectorSentiments = new Dictionary<string, double>();
            if (data.TryGetProperty("sectorSentiments", out var sectors))
            {
                foreach (var sector in sectors.EnumerateObject())
                {
                    sectorSentiments[sector.Name] = sector.Value.GetDouble();
                }
            }

            return new MarketSentimentResult
            {
                OverallSentiment = data.GetProperty("overallSentiment").GetString() ?? "Neutral",
                SentimentScore = data.GetProperty("sentimentScore").GetDouble(),
                Confidence = data.GetProperty("confidence").GetDouble(),
                MarketMood = data.GetProperty("marketMood").GetString() ?? "Neutral",
                SectorSentiments = sectorSentiments,
                KeyThemes = data.GetProperty("keyThemes").EnumerateArray()
                    .Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList(),
                RiskLevel = data.GetProperty("riskLevel").GetString() ?? "Medium",
                Summary = data.GetProperty("summary").GetString() ?? ""
            };
        }

        private string ExtractJsonFromResponse(string response)
        {
            // Extract JSON from AI response, handling markdown code blocks and both objects and arrays
            var lines = response.Split('\n');
            var jsonLines = new List<string>();
            bool inJson = false;
            char jsonStart = ' ';

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Check for start of JSON object or array
                if ((trimmed.StartsWith("{") || trimmed.StartsWith("[")) && !inJson)
                {
                    inJson = true;
                    jsonStart = trimmed[0];
                    jsonLines.Add(line);
                }
                else if (inJson)
                {
                    jsonLines.Add(line);
                    
                    // Check for end of JSON
                    var jsonSoFar = string.Join("\n", jsonLines);
                    if (jsonStart == '{' && trimmed.EndsWith("}") && CountBraces(jsonSoFar) == 0)
                    {
                        break;
                    }
                    else if (jsonStart == '[' && trimmed.EndsWith("]") && CountBrackets(jsonSoFar) == 0)
                    {
                        break;
                    }
                }
            }

            return string.Join("\n", jsonLines);
        }

        private int CountBraces(string json)
        {
            int count = 0;
            foreach (char c in json)
            {
                if (c == '{') count++;
                else if (c == '}') count--;
            }
            return count;
        }
        
        private int CountBrackets(string json)
        {
            int count = 0;
            foreach (char c in json)
            {
                if (c == '[') count++;
                else if (c == ']') count--;
            }
            return count;
        }
    }

    // Data models for sentiment analysis
    public class SymbolSentimentAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime AnalysisDate { get; set; }
        public List<NewsItem> NewsItems { get; set; } = new();
        public string OverallSentiment { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public double Confidence { get; set; }
        public string TrendDirection { get; set; } = string.Empty;
        public List<string> KeyThemes { get; set; } = new();
        public string TradingSignal { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public string VolatilityIndicator { get; set; } = string.Empty;
        public string PriceTargetBias { get; set; } = string.Empty;
        public string InstitutionalSentiment { get; set; } = string.Empty;
        public string RetailSentiment { get; set; } = string.Empty;
        public string AnalystConsensus { get; set; } = string.Empty;
        public string EarningsImpact { get; set; } = string.Empty;
        public string SectorComparison { get; set; } = string.Empty;
        public string MomentumSignal { get; set; } = string.Empty;
    }

    public class MarketSentimentAnalysis
    {
        public DateTime AnalysisDate { get; set; }
        public List<NewsItem> NewsItems { get; set; } = new();
        public string OverallSentiment { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public double Confidence { get; set; }
        public string MarketMood { get; set; } = string.Empty;
        public Dictionary<string, double> SectorSentiments { get; set; } = new();
        public List<string> KeyThemes { get; set; } = new();
        public string RiskLevel { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public class NewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public DateTime PublishedDate { get; set; }
        public string Link { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public string SentimentLabel { get; set; } = string.Empty;
        public List<string> KeyTopics { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
    }

    public class NewsItemSentiment
    {
        public double Score { get; set; }
        public string Label { get; set; } = string.Empty;
        public List<string> KeyTopics { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
    }

    public class OverallSentimentResult
    {
        public string OverallSentiment { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public double Confidence { get; set; }
        public string TrendDirection { get; set; } = string.Empty;
        public List<string> KeyThemes { get; set; } = new();
        public string TradingSignal { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public string VolatilityIndicator { get; set; } = string.Empty;
        public string PriceTargetBias { get; set; } = string.Empty;
        public string InstitutionalSentiment { get; set; } = string.Empty;
        public string RetailSentiment { get; set; } = string.Empty;
        public string AnalystConsensus { get; set; } = string.Empty;
        public string EarningsImpact { get; set; } = string.Empty;
        public string SectorComparison { get; set; } = string.Empty;
        public string MomentumSignal { get; set; } = string.Empty;
    }

    public class MarketSentimentResult
    {
        public string OverallSentiment { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public double Confidence { get; set; }
        public string MarketMood { get; set; } = string.Empty;
        public Dictionary<string, double> SectorSentiments { get; set; } = new();
        public List<string> KeyThemes { get; set; } = new();
        public string RiskLevel { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
