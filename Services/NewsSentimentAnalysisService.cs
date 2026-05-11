using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Plugins;
using System.Diagnostics;
using System.IO;
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
        private readonly INewsPipelineService _newsPipelineService;
        private readonly HttpClient _httpClient;
        private readonly ArticleScraperService? _articleScraperService;
        private readonly ChunkGeneratorService? _chunkGeneratorService;
        private readonly EmbeddingService? _embeddingService;
        private readonly VectorStoreService? _vectorStoreService;
        
        
        // Circuit breaker state
        private readonly Dictionary<string, CircuitBreakerState> _circuitBreakers = new();
        private readonly object _circuitBreakerLock = new();

        public NewsSentimentAnalysisService(
            ILogger<NewsSentimentAnalysisService> logger,
            OpenAIService openAIService,
            YFinanceNewsService yfinanceNewsService,
            FinvizNewsService finvizNewsService,
            RedditScrapingService redditScrapingService,
            GoogleWebSearchPlugin googleSearchPlugin,
            INewsPipelineService newsPipelineService,
            HttpClient httpClient,
            ArticleScraperService? articleScraperService = null,
            ChunkGeneratorService? chunkGeneratorService = null,
            EmbeddingService? embeddingService = null,
            VectorStoreService? vectorStoreService = null)
        {
            _logger = logger;
            _openAIService = openAIService;
            _yfinanceNewsService = yfinanceNewsService;
            _finvizNewsService = finvizNewsService;
            _redditScrapingService = redditScrapingService;
            _googleSearchPlugin = googleSearchPlugin;
            _newsPipelineService = newsPipelineService;
            _httpClient = httpClient;
            _articleScraperService = articleScraperService;
            _chunkGeneratorService = chunkGeneratorService;
            _embeddingService = embeddingService;
            _vectorStoreService = vectorStoreService;
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

                // Remove duplicates, filter old news (older than 30 days), and sort by date
                var cutoffDate = DateTime.Now.AddDays(-30);
                var uniqueNews = allNews
                    .Where(n => n.PublishedDate >= cutoffDate) // Filter out old news
                    .GroupBy(n => n.Title.ToLower().Trim())
                    .Select(g => g.OrderByDescending(n => n.PublishedDate).First())
                    .OrderByDescending(n => n.PublishedDate)
                    .Take(newsLimit)
                    .ToList();

                _logger.LogInformation($"After filtering: {uniqueNews.Count} recent news items (within 30 days)");

                // BATCH analyze sentiment for all news items in one call for speed
                var sentimentResults = await AnalyzeBatchSentimentAsync(uniqueNews);

                // Combine results
                for (int i = 0; i < uniqueNews.Count && i < sentimentResults.Count; i++)
                {
                    uniqueNews[i].SentimentScore = sentimentResults[i].Score;
                    uniqueNews[i].SentimentLabel = sentimentResults[i].Label;
                    uniqueNews[i].SentimentConfidence = sentimentResults[i].Confidence;
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
                    PositiveThemes = overallAnalysis.PositiveThemes,
                    NegativeThemes = overallAnalysis.NegativeThemes,
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
        /// Analyze sentiment for a symbol using full article content with vector storage
        /// Fetches news from all 6 sources, scrapes full content, generates chunks, embeddings, and stores in vector DB
            /// Fetches news from all 7 sources, scrapes full content, generates chunks, embeddings, and stores in vector DB
        /// </summary>
        public async Task<SymbolSentimentAnalysis> AnalyzeSymbolSentimentWithFullContentAsync(string symbol, int newsLimit = 10)
        {
            try
            {
                _logger.LogInformation($"Starting enhanced sentiment analysis with full content for {symbol}");

                // Check if enhanced services are available
                if (_articleScraperService == null || _chunkGeneratorService == null || 
                    _embeddingService == null || _vectorStoreService == null)
                {
                    _logger.LogWarning("Enhanced services not available, falling back to standard analysis");
                    return await AnalyzeSymbolSentimentAsync(symbol, newsLimit);
                }

                // Step 1: Fetch news and full article content from the Python pipeline
                var pipelineResult = await _newsPipelineService.SearchNewsAsync(symbol, "All", newsLimit);
                var uniqueNews = pipelineResult.Articles
                    .Where(n => !string.IsNullOrWhiteSpace(n.Title))
                    .GroupBy(n => n.Title.ToLower().Trim())
                    .Select(g => g.OrderByDescending(n => n.PublishedDate).First())
                    .OrderByDescending(n => n.PublishedDate)
                    .Take(newsLimit)
                    .Select(n => new NewsItem
                    {
                        Title = n.Title,
                        Summary = n.Summary,
                        FullContent = string.IsNullOrWhiteSpace(n.FullContent) ? n.Summary : n.FullContent,
                        ContentLength = (string.IsNullOrWhiteSpace(n.FullContent) ? n.Summary : n.FullContent).Length,
                        ContentScraped = n.IsScraped || !string.IsNullOrWhiteSpace(n.FullContent),
                        Publisher = string.IsNullOrWhiteSpace(n.Provider) ? n.Source : n.Provider,
                        PublishedDate = n.PublishedDate,
                        Link = n.Url,
                        Source = string.IsNullOrWhiteSpace(n.Source) ? "PythonPipeline" : n.Source
                    })
                    .ToList();

                if (uniqueNews.Count == 0)
                {
                    _logger.LogWarning($"No news items found for {symbol} from the Python pipeline");
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

                _logger.LogInformation($"Python pipeline returned {uniqueNews.Count} recent news items with full content");

                // Step 3: Generate chunks for each article
                _logger.LogInformation("Generating semantic chunks from articles...");
                var allChunks = new List<ArticleChunk>();
                foreach (var newsItem in uniqueNews)
                {
                    if (string.IsNullOrWhiteSpace(newsItem.FullContent))
                        continue;

                    var articleContent = new ArticleContent
                    {
                        Url = newsItem.Link,
                        Content = newsItem.FullContent,
                        ContentLength = newsItem.ContentLength ?? 0,
                        Success = newsItem.ContentScraped
                    };

                    var chunks = _chunkGeneratorService.GenerateChunks(articleContent, newsItem);
                    allChunks.AddRange(chunks);
                    
                    newsItem.ChunkIds = chunks.Select(c => c.ChunkId).ToList();
                    newsItem.ChunkCount = chunks.Count;
                }

                _logger.LogInformation($"Generated {allChunks.Count} total chunks from {uniqueNews.Count} articles");

                // Step 4: Generate embeddings for all chunks
                if (allChunks.Count > 0)
                {
                    _logger.LogInformation($"Generating embeddings for {allChunks.Count} chunks...");
                    var chunkTexts = allChunks.Select(c => c.Content).ToList();
                    var embeddings = await ExecuteWithCircuitBreaker("EmbeddingService",
                        () => _embeddingService.GenerateEmbeddingsAsync(chunkTexts));

                    // Step 5: Store chunks with embeddings in vector store
                    _logger.LogInformation($"Storing {allChunks.Count} chunks in vector store...");
                    var chunksWithEmbeddings = allChunks.Zip(embeddings, (chunk, embedding) => (chunk, embedding)).ToList();
                    var storedCount = await ExecuteWithCircuitBreaker("VectorStore",
                        () => _vectorStoreService.StoreChunksAsync(chunksWithEmbeddings));

                    _logger.LogInformation($"Stored {storedCount} chunks in vector store");
                }

                // Step 6: Analyze sentiment using full article content
                _logger.LogInformation("Analyzing sentiment with full article content...");
                var sentimentResults = await AnalyzeBatchSentimentAsync(uniqueNews);

                // Combine results
                for (int i = 0; i < uniqueNews.Count && i < sentimentResults.Count; i++)
                {
                    uniqueNews[i].SentimentScore = sentimentResults[i].Score;
                    uniqueNews[i].SentimentLabel = sentimentResults[i].Label;
                    uniqueNews[i].SentimentConfidence = sentimentResults[i].Confidence;
                    uniqueNews[i].KeyTopics = sentimentResults[i].KeyTopics;
                    uniqueNews[i].Impact = sentimentResults[i].Impact;
                }

                // Step 7: Generate overall analysis
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
                    PositiveThemes = overallAnalysis.PositiveThemes,
                    NegativeThemes = overallAnalysis.NegativeThemes,
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
                _logger.LogError(ex, $"Error in enhanced sentiment analysis for {symbol}");
                throw;
            }
        }

        /// <summary>
        /// Search for related articles using semantic search with natural language query
        /// </summary>
        /// <param name="query">Natural language search query</param>
        /// <param name="limit">Maximum number of results to return (default: 10)</param>
        /// <param name="filter">Optional filter criteria for date range, source, and symbol</param>
        /// <returns>List of NewsItem with similarity scores</returns>
        public async Task<List<NewsItem>> SearchRelatedArticlesAsync(
            string query,
            int limit = 10,
            ChunkFilter? filter = null)
        {
            try
            {
                _logger.LogInformation($"Searching for related articles with query: '{query}', limit: {limit}");

                // Check if enhanced services are available
                if (_embeddingService == null || _vectorStoreService == null)
                {
                    _logger.LogWarning("Enhanced services not available for semantic search");
                    return new List<NewsItem>();
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    throw new ArgumentException("Search query cannot be null or empty", nameof(query));
                }

                // Step 1: Generate query embedding from natural language text
                _logger.LogDebug("Generating embedding for search query...");
                var queryEmbedding = await ExecuteWithCircuitBreaker("EmbeddingService",
                    () => _embeddingService.GenerateEmbeddingAsync(query));

                // Step 2: Call VectorStoreService.SearchAsync with filters
                _logger.LogDebug("Performing semantic search in vector store...");
                var searchResults = await ExecuteWithCircuitBreaker("VectorStore",
                    () => _vectorStoreService.SearchAsync(queryEmbedding, limit, filter));

                if (searchResults.Count == 0)
                {
                    _logger.LogInformation("No results found for query: '{Query}'", query);
                    return new List<NewsItem>();
                }

                _logger.LogInformation($"Found {searchResults.Count} matching chunks for query");

                // Step 3: Group chunks by article and aggregate to NewsItem list
                var articleGroups = searchResults
                    .GroupBy(r => r.Chunk.ArticleUrl)
                    .Select(g => new
                    {
                        Url = g.Key,
                        Chunks = g.OrderBy(r => r.Chunk.ChunkIndex).ToList(),
                        MaxSimilarity = g.Max(r => r.SimilarityScore),
                        AvgSimilarity = g.Average(r => r.SimilarityScore)
                    })
                    .OrderByDescending(a => a.MaxSimilarity)
                    .Take(limit)
                    .ToList();

                // Step 4: Convert to NewsItem list with similarity scores
                var newsItems = new List<NewsItem>();
                foreach (var articleGroup in articleGroups)
                {
                    var firstChunk = articleGroup.Chunks.First().Chunk;
                    
                    // Reconstruct full content from chunks
                    var fullContent = string.Join(" ", articleGroup.Chunks.Select(c => c.Chunk.Content));
                    
                    // Create summary from first chunk or truncate full content
                    var summary = firstChunk.Content.Length > 200 
                        ? firstChunk.Content.Substring(0, 200) + "..." 
                        : firstChunk.Content;

                    var newsItem = new NewsItem
                    {
                        Title = firstChunk.Title,
                        Summary = summary,
                        Publisher = firstChunk.Publisher,
                        PublishedDate = firstChunk.PublishedDate,
                        Link = firstChunk.ArticleUrl,
                        Source = firstChunk.Source,
                        FullContent = fullContent,
                        ContentLength = fullContent.Length,
                        ContentScraped = true,
                        ChunkIds = articleGroup.Chunks.Select(c => c.Chunk.ChunkId).ToList(),
                        ChunkCount = articleGroup.Chunks.Count,
                        // Store similarity score in SentimentScore field for now
                        // (could add a dedicated SimilarityScore field to NewsItem in the future)
                        SentimentScore = articleGroup.MaxSimilarity
                    };

                    newsItems.Add(newsItem);
                }

                _logger.LogInformation(
                    $"Returning {newsItems.Count} articles for query '{query}' with similarity scores");

                return newsItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for related articles with query: '{query}'");
                throw;
            }
        }

        /// <summary>
        /// Aggregate news from all 6 sources in parallel
        /// </summary>
        private async Task<List<NewsItem>> AggregateNewsFromAllSourcesAsync(string symbol, int newsLimit)
        {
            var allNews = new List<NewsItem>();
            
            // Fetch more items per source (3x the limit) to ensure we have enough after deduplication
            var perSourceLimit = newsLimit * 3;

            // Google Search for latest news
            var googleTask = Task.Run(async () =>
            {
                try
                {
                    var searchQuery = $"{symbol} stock news latest";
                    var googleResults = await _googleSearchPlugin.SearchAsync(searchQuery, maxResults: perSourceLimit);
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
                    var finvizNews = await _finvizNewsService.GetNewsAsync(symbol, perSourceLimit);
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
                    var redditPosts = await _redditScrapingService.ScrapeSubredditAsync("wallstreetbets", perSourceLimit);
                    var symbolPosts = redditPosts.Where(p => 
                        p.Title.Contains(symbol, StringComparison.OrdinalIgnoreCase) ||
                        p.Content.Contains(symbol, StringComparison.OrdinalIgnoreCase))
                        .Take(perSourceLimit);
                    
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

            var yahooTask = Task.Run(async () =>
            {
                try
                {
                    var yahooNews = await _yfinanceNewsService.GetNewsAsync(symbol, perSourceLimit);
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
                        
                        var titlePattern = new System.Text.RegularExpressions.Regex(@"<h3[^>]*class=""[^""]*article__headline[^""]*""[^>]*>\s*<a[^>]*href=""([^""]*)""[^>]*>([^<]*)</a>");
                        var matches = titlePattern.Matches(html);
                        
                        foreach (System.Text.RegularExpressions.Match match in matches.Take(perSourceLimit))
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

            // Wait for core sources to complete in parallel
            await Task.WhenAll(googleTask, finvizTask, redditTask, yahooTask, marketWatchTask);

            // Log results from each source
            _logger.LogInformation($"Source results - Google: {googleTask.Result.Count}, Finviz: {finvizTask.Result.Count}, Reddit: {redditTask.Result.Count}, Yahoo: {yahooTask.Result.Count}, MarketWatch: {marketWatchTask.Result.Count}");

            allNews.AddRange(googleTask.Result);
            allNews.AddRange(finvizTask.Result);
            allNews.AddRange(redditTask.Result);
            allNews.AddRange(yahooTask.Result);
            allNews.AddRange(marketWatchTask.Result);

            return allNews;
        }

        /// <summary>
        /// Execute an operation with circuit breaker pattern
        /// </summary>
        private async Task<T> ExecuteWithCircuitBreaker<T>(string serviceName, Func<Task<T>> operation)
        {
            CircuitBreakerState state;
            lock (_circuitBreakerLock)
            {
                if (!_circuitBreakers.ContainsKey(serviceName))
                {
                    _circuitBreakers[serviceName] = new CircuitBreakerState();
                }
                state = _circuitBreakers[serviceName];
            }

            // Check if circuit is open
            if (state.IsOpen)
            {
                _logger.LogWarning($"Circuit breaker is OPEN for {serviceName}. Failing fast.");
                throw new InvalidOperationException($"Circuit breaker is open for {serviceName}");
            }

            try
            {
                var result = await operation();
                
                // Success - reset circuit breaker
                lock (_circuitBreakerLock)
                {
                    state.Reset();
                }
                
                return result;
            }
            catch (Exception ex)
            {
                // Record failure
                lock (_circuitBreakerLock)
                {
                    state.RecordFailure();
                    _logger.LogError(ex, 
                        $"Circuit breaker recorded failure for {serviceName}. Failure count: {state.FailureCount}/3");
                }
                
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
                    uniqueNews[i].SentimentConfidence = sentimentResults[i].Confidence;
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
                    newsItem.SentimentConfidence = sentiment.Confidence;
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

        private async Task<List<NewsItemSentiment>> AnalyzeBatchSentimentAsync(List<NewsItem> newsItems)
        {
            try
            {
                if (newsItems.Count == 0)
                    return new List<NewsItemSentiment>();

                _logger.LogInformation($"Scoring {newsItems.Count} pipeline-provided articles without refetching article bodies");

                var payload = new
                {
                    articles = newsItems.Select(n => new
                    {
                        title = n.Title,
                        summary = n.Summary,
                        content = !string.IsNullOrWhiteSpace(n.FullContent)
                            ? n.FullContent
                            : n.Summary,
                        publisher = n.Publisher,
                        source = n.Source,
                        publishedDate = n.PublishedDate,
                        url = n.Link
                    }).ToList()
                };

                var jsonResponse = await RunFastEmbedSentimentAsync(payload);
                return ParseFastEmbedSentimentResults(jsonResponse, newsItems.Count);
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
                var results = await AnalyzeBatchSentimentAsync(new List<NewsItem> { newsItem });
                return results.FirstOrDefault() ?? new NewsItemSentiment
                {
                    Score = 0.0,
                    Label = "Neutral",
                    KeyTopics = new List<string>(),
                    Impact = "Medium"
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
            double GetAverageScore() => newsItems.Count == 0 ? 0.0 : newsItems.Average(n => n.SentimentScore);
            double GetAverageConfidence() => newsItems.Count == 0 ? 0.0 : newsItems.Average(n => n.SentimentConfidence > 0 ? n.SentimentConfidence : Math.Abs(n.SentimentScore));
            double GetVolatility() => newsItems.Count <= 1 ? 0.0 : Math.Sqrt(newsItems.Average(n => Math.Pow(n.SentimentScore - GetAverageScore(), 2)));

            string MapOverallLabel(double score) => score switch
            {
                > 0.45 => "Very Positive",
                > 0.15 => "Positive",
                < -0.45 => "Very Negative",
                < -0.15 => "Negative",
                _ => "Neutral"
            };

            string MapTrend(double score) => score > 0.15 ? "Improving" : score < -0.15 ? "Declining" : "Stable";
            string MapTradingSignal(double score) => score > 0.45 ? "Strong Buy" : score > 0.15 ? "Buy" : score < -0.45 ? "Strong Sell" : score < -0.15 ? "Sell" : "Hold";
            string MapThreeWay(string positive, string neutral, string negative, double score) => score > 0.15 ? positive : score < -0.15 ? negative : neutral;
            string MapVolatility(double volatility) => volatility > 0.35 ? "High" : volatility > 0.18 ? "Medium" : "Low";
            string MapMomentum(double score) => score > 0.45 ? "Strong Positive" : score > 0.15 ? "Positive" : score < -0.45 ? "Strong Negative" : score < -0.15 ? "Negative" : "Neutral";

            var aggregateScore = GetAverageScore();
            var aggregateConfidence = GetAverageConfidence();
            var overallSentiment = MapOverallLabel(aggregateScore);
            var trendDirection = MapTrend(aggregateScore);
            var tradingSignal = MapTradingSignal(aggregateScore);
            var volatilityIndicator = MapVolatility(GetVolatility());
            var priceTargetBias = MapThreeWay("Bullish", "Neutral", "Bearish", aggregateScore);
            var institutionalSentiment = MapThreeWay("Positive", "Neutral", "Negative", aggregateScore);
            var retailSentiment = MapThreeWay("Positive", "Neutral", "Negative", aggregateScore);
            var analystConsensus = MapThreeWay("Buy", "Hold", "Sell", aggregateScore);
            var earningsImpact = MapThreeWay("Positive", "Neutral", "Negative", aggregateScore);
            var sectorComparison = MapThreeWay("Outperform", "Inline", "Underperform", aggregateScore);
            var momentumSignal = MapMomentum(aggregateScore);

            var articleReferences = string.Join("\n\n", newsItems.Select((n, idx) =>
                $"[{idx + 1}] {n.Title}\n" +
                $"    Source: {n.Source} | Published: {n.PublishedDate:yyyy-MM-dd}\n" +
                $"    FastEmbed Sentiment: {n.SentimentLabel} (Score: {n.SentimentScore:F2}, Confidence: {n.SentimentConfidence:F2})\n" +
                $"    Key Topics: {string.Join(", ", n.KeyTopics ?? new List<string>())}\n" +
                $"    Impact Level: {n.Impact}\n" +
                $"    URL: {n.Link}\n" +
                $"    Summary: {(string.IsNullOrEmpty(n.Summary) ? "N/A" : (n.Summary.Length > 300 ? n.Summary.Substring(0, 300) + "..." : n.Summary))}"
            ));

                        var prompt = $$"""
You are writing a concise, readable institutional summary for {{symbol}}.
The FastEmbed model already scored the articles. Do not recalculate sentiment or change the labels.

FASTEMBED AGGREGATE:
- Overall label: {{overallSentiment}}
- Average score: {{aggregateScore:F4}}
- Average confidence: {{aggregateConfidence:F4}}
- Trend: {{trendDirection}}
- Trading signal: {{tradingSignal}}

ARTICLES:
{{articleReferences}}

Return JSON with this shape:
{
    "summary": "clear plain-English summary",
    "keyThemes": ["theme1", "theme2", "theme3"],
    "riskFactors": ["risk1", "risk2"],
    "positiveThemes": [
        {"theme":"","evidence":"","citations":[1],"impact":"High|Medium|Low"}
    ],
    "negativeThemes": [
        {"theme":"","evidence":"","citations":[1],"impact":"High|Medium|Low"}
    ]
}

Make the summary more discernible and specific, but do not include sentiment scores or confidence values.
""";

            var jsonResponse = await _openAIService.GetChatCompletionAsync(prompt);
            var jsonClean = ExtractJsonFromResponse(jsonResponse);
            var data = JsonSerializer.Deserialize<JsonElement>(jsonClean);

            return new OverallSentimentResult
            {
                OverallSentiment = overallSentiment,
                SentimentScore = aggregateScore,
                Confidence = aggregateConfidence,
                TrendDirection = trendDirection,
                KeyThemes = data.TryGetProperty("keyThemes", out var keyThemes) && keyThemes.ValueKind == JsonValueKind.Array
                    ? keyThemes.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
                    : new List<string>(),
                TradingSignal = tradingSignal,
                RiskFactors = data.TryGetProperty("riskFactors", out var riskFactors) && riskFactors.ValueKind == JsonValueKind.Array
                    ? riskFactors.EnumerateArray().Select(r => r.GetString() ?? "").Where(r => !string.IsNullOrWhiteSpace(r)).ToList()
                    : new List<string>(),
                Summary = data.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "" : "",
                PositiveThemes = data.TryGetProperty("positiveThemes", out var posThemes)
                    ? posThemes.EnumerateArray().Select(t => new SentimentTheme
                    {
                        Theme = t.GetProperty("theme").GetString() ?? "",
                        Evidence = t.GetProperty("evidence").GetString() ?? "",
                        Citations = t.TryGetProperty("citations", out var cits)
                            ? cits.EnumerateArray().Select(c => c.GetInt32()).ToList()
                            : new List<int>(),
                        Impact = t.GetProperty("impact").GetString() ?? "Medium"
                    }).ToList()
                    : new List<SentimentTheme>(),
                NegativeThemes = data.TryGetProperty("negativeThemes", out var negThemes)
                    ? negThemes.EnumerateArray().Select(t => new SentimentTheme
                    {
                        Theme = t.GetProperty("theme").GetString() ?? "",
                        Evidence = t.GetProperty("evidence").GetString() ?? "",
                        Citations = t.TryGetProperty("citations", out var cits)
                            ? cits.EnumerateArray().Select(c => c.GetInt32()).ToList()
                            : new List<int>(),
                        Impact = t.GetProperty("impact").GetString() ?? "Medium"
                    }).ToList()
                    : new List<SentimentTheme>(),
                VolatilityIndicator = volatilityIndicator,
                PriceTargetBias = priceTargetBias,
                InstitutionalSentiment = institutionalSentiment,
                RetailSentiment = retailSentiment,
                AnalystConsensus = analystConsensus,
                EarningsImpact = earningsImpact,
                SectorComparison = sectorComparison,
                MomentumSignal = momentumSignal
            };
        }

        private async Task<MarketSentimentResult> GenerateMarketSentimentAnalysisAsync(List<NewsItem> newsItems)
        {
            double GetAverageScore() => newsItems.Count == 0 ? 0.0 : newsItems.Average(n => n.SentimentScore);
            double GetAverageConfidence() => newsItems.Count == 0 ? 0.0 : newsItems.Average(n => n.SentimentConfidence > 0 ? n.SentimentConfidence : Math.Abs(n.SentimentScore));
            double GetVolatility() => newsItems.Count <= 1 ? 0.0 : Math.Sqrt(newsItems.Average(n => Math.Pow(n.SentimentScore - GetAverageScore(), 2)));

            string MapOverallLabel(double score) => score switch
            {
                > 0.45 => "Very Positive",
                > 0.15 => "Positive",
                < -0.45 => "Very Negative",
                < -0.15 => "Negative",
                _ => "Neutral"
            };

            string MapMarketMood(double score) => score switch
            {
                > 0.55 => "Euphoria",
                > 0.15 => "Optimism",
                < -0.55 => "Fear",
                < -0.15 => "Caution",
                _ => "Neutral"
            };

            string MapRiskLevel(double volatility) => volatility > 0.35 ? "High" : volatility > 0.18 ? "Medium" : "Low";

            var aggregateScore = GetAverageScore();
            var aggregateConfidence = GetAverageConfidence();
            var overallSentiment = MapOverallLabel(aggregateScore);
            var marketMood = MapMarketMood(aggregateScore);
            var riskLevel = MapRiskLevel(GetVolatility());

                        var prompt = $$"""
Summarize the market implications of these FastEmbed-scored articles.
Do not recalculate sentiment or assign numeric scores.

FASTEMBED SNAPSHOT:
- Overall label: {{overallSentiment}}
- Average score: {{aggregateScore:F4}}
- Average confidence: {{aggregateConfidence:F4}}
- Market mood: {{marketMood}}

ARTICLES:
{{string.Join("\n\n", newsItems.Take(15).Select((n, idx) => $"[{idx + 1}] {n.Title}\nSentiment: {n.SentimentLabel} ({n.SentimentScore:F2}, conf {n.SentimentConfidence:F2})\nTopics: {string.Join(", ", n.KeyTopics ?? new List<string>())}\nSummary: {(string.IsNullOrWhiteSpace(n.Summary) ? "N/A" : n.Summary)}"))}}

Return JSON with this shape:
{
    "summary": "2-3 sentence market summary",
    "keyThemes": ["theme1", "theme2", "theme3"],
    "marketMood": "<Fear|Caution|Neutral|Optimism|Euphoria>"
}

Make the wording plain, specific, and easy to understand.
""";

            var jsonResponse = await _openAIService.GetChatCompletionAsync(prompt);
            var jsonClean = ExtractJsonFromResponse(jsonResponse);
            var data = JsonSerializer.Deserialize<JsonElement>(jsonClean);

            var sectorSentiments = new Dictionary<string, double>
            {
                ["technology"] = aggregateScore,
                ["finance"] = aggregateScore,
                ["healthcare"] = aggregateScore
            };

            return new MarketSentimentResult
            {
                OverallSentiment = overallSentiment,
                SentimentScore = aggregateScore,
                Confidence = aggregateConfidence,
                MarketMood = data.TryGetProperty("marketMood", out var mood) ? mood.GetString() ?? marketMood : marketMood,
                SectorSentiments = sectorSentiments,
                KeyThemes = data.TryGetProperty("keyThemes", out var keyThemes) && keyThemes.ValueKind == JsonValueKind.Array
                    ? keyThemes.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
                    : new List<string>(),
                RiskLevel = riskLevel,
                Summary = data.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "" : ""
            };
        }

        private async Task<string> RunFastEmbedSentimentAsync(object payload)
        {
            var scriptPath = ResolveScriptPath();
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"Unable to locate Python pipeline script at {scriptPath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python3",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("--sentiment");

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start python3 process");
            var payloadJson = JsonSerializer.Serialize(payload);
            await process.StandardInput.WriteAsync(payloadJson);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Python sentiment pipeline failed with exit code {process.ExitCode}: {stderr}");
            }

            return stdout;
        }

        private static string ResolveScriptPath()
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "news_pipeline.py")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Scripts", "news_pipeline.py")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Scripts", "news_pipeline.py"))
            };

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        private List<NewsItemSentiment> ParseFastEmbedSentimentResults(string json, int expectedCount)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var items = new List<NewsItemSentiment>();

            JsonElement? articles = null;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("articles", out var articlesElement))
            {
                articles = articlesElement;
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                articles = root;
            }

            if (articles.HasValue && articles.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in articles.Value.EnumerateArray())
                {
                    var score = 0.0;
                    if (item.TryGetProperty("score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number)
                    {
                        scoreProp.TryGetDouble(out score);
                    }
                    else if (item.TryGetProperty("polarity", out var polarityProp) && polarityProp.ValueKind == JsonValueKind.Number)
                    {
                        polarityProp.TryGetDouble(out score);
                    }

                    var label = item.TryGetProperty("label", out var labelProp)
                        ? labelProp.GetString() ?? "Neutral"
                        : item.TryGetProperty("sentiment", out var sentimentProp)
                            ? sentimentProp.GetString() ?? "Neutral"
                            : "Neutral";

                    items.Add(new NewsItemSentiment
                    {
                        Score = score,
                        Confidence = item.TryGetProperty("confidence", out var confidenceProp) && confidenceProp.ValueKind == JsonValueKind.Number
                            ? confidenceProp.GetDouble()
                            : Math.Abs(score),
                        Label = NormalizeSentimentLabel(label),
                        KeyTopics = item.TryGetProperty("keyTopics", out var topicsProp) && topicsProp.ValueKind == JsonValueKind.Array
                            ? topicsProp.EnumerateArray().Select(topic => topic.GetString() ?? string.Empty).Where(topic => !string.IsNullOrWhiteSpace(topic)).ToList()
                            : new List<string>(),
                        Impact = item.TryGetProperty("impact", out var impactProp)
                            ? impactProp.GetString() ?? "Medium"
                            : "Medium"
                    });
                }
            }

            while (items.Count < expectedCount)
            {
                items.Add(new NewsItemSentiment
                {
                    Score = 0.0,
                    Confidence = 0.0,
                    Label = "Neutral",
                    KeyTopics = new List<string>(),
                    Impact = "Medium"
                });
            }

            return items;
        }

        private static string NormalizeSentimentLabel(string label)
        {
            return label.Trim().ToUpperInvariant() switch
            {
                "POSITIVE" or "VERY POSITIVE" => "Positive",
                "NEGATIVE" or "VERY NEGATIVE" => "Negative",
                "BULLISH" => "Positive",
                "BEARISH" => "Negative",
                _ => "Neutral"
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
        public List<SentimentTheme> PositiveThemes { get; set; } = new();
        public List<SentimentTheme> NegativeThemes { get; set; } = new();
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
        public string? ImageUrl { get; set; }
        public string Source { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public string SentimentLabel { get; set; } = string.Empty;
        public double SentimentConfidence { get; set; }
        public List<string> KeyTopics { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
        
        // Enhanced fields for full content analysis
        public string? FullContent { get; set; }
        public int? ContentLength { get; set; }
        public bool ContentScraped { get; set; }
        public List<string>? ChunkIds { get; set; }
        public int? ChunkCount { get; set; }
    }

    public class NewsItemSentiment
    {
        public double Score { get; set; }
        public double Confidence { get; set; }
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
        public List<SentimentTheme> PositiveThemes { get; set; } = new();
        public List<SentimentTheme> NegativeThemes { get; set; } = new();
        public string VolatilityIndicator { get; set; } = string.Empty;
        public string PriceTargetBias { get; set; } = string.Empty;
        public string InstitutionalSentiment { get; set; } = string.Empty;
        public string RetailSentiment { get; set; } = string.Empty;
        public string AnalystConsensus { get; set; } = string.Empty;
        public string EarningsImpact { get; set; } = string.Empty;
        public string SectorComparison { get; set; } = string.Empty;
        public string MomentumSignal { get; set; } = string.Empty;
    }

    public class SentimentTheme
    {
        public string Theme { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public List<int> Citations { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
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

    /// <summary>
    /// Circuit breaker state for external API calls
    /// </summary>
    internal class CircuitBreakerState
    {
        public int FailureCount { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public bool IsOpen => FailureCount >= 3 && LastFailureTime.HasValue && 
                              (DateTime.UtcNow - LastFailureTime.Value).TotalSeconds < 60;
        
        public void RecordFailure()
        {
            FailureCount++;
            LastFailureTime = DateTime.UtcNow;
        }
        
        public void Reset()
        {
            FailureCount = 0;
            LastFailureTime = null;
        }
    }
}
