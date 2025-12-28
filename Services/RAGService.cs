using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using QuantResearchAgent.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuantResearchAgent.Core;

/// <summary>
/// RAG (Retrieval-Augmented Generation) Service for enhanced content analysis and trading insights
/// </summary>
public class RAGService
{
    private readonly ILogger<RAGService> _logger;
    private readonly Kernel _kernel;
    private readonly ISemanticTextMemory _memory;
    private readonly IConfiguration _configuration;
    private readonly YouTubeAnalysisService _youtubeService;
    private readonly MarketDataService _marketDataService;
    private readonly WebIntelligenceService _webIntelligenceService;

    public RAGService(
        ILogger<RAGService> logger,
        Kernel kernel,
        ISemanticTextMemory memory,
        IConfiguration configuration,
        YouTubeAnalysisService youtubeService,
        MarketDataService marketDataService,
        WebIntelligenceService webIntelligenceService)
    {
        _logger = logger;
        _kernel = kernel;
        _memory = memory;
        _configuration = configuration;
        _youtubeService = youtubeService;
        _marketDataService = marketDataService;
        _webIntelligenceService = webIntelligenceService;
    }

    /// <summary>
    /// Analyze content with RAG enhancement - retrieves relevant context and generates insights
    /// </summary>
    public async Task<RAGAnalysisResult> AnalyzeWithRAGAsync(string query, string contentType = "general")
    {
        try
        {
            // Step 1: Generate embeddings and search for relevant context
            var relevantMemories = new List<MemoryQueryResult>();
            await foreach (var memory in _memory.SearchAsync(
                collection: contentType,
                query: query,
                limit: 10,
                minRelevanceScore: 0.7
            ))
            {
                relevantMemories.Add(memory);
            }

            // Step 2: Build context from retrieved memories
            var context = BuildContextFromMemories(relevantMemories);

            // Step 3: Generate enhanced analysis using retrieved context
            var enhancedAnalysis = await GenerateRAGAnalysisAsync(query, context, contentType);

            // Step 4: Extract and validate trading signals
            var tradingSignals = await ExtractTradingSignalsWithContextAsync(query, enhancedAnalysis, context);

            return new RAGAnalysisResult
            {
                Query = query,
                Context = context,
                Analysis = enhancedAnalysis,
                TradingSignals = tradingSignals,
                ConfidenceScore = CalculateConfidenceScore(relevantMemories),
                Sources = relevantMemories.Select(m => m.Metadata.Id).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG analysis failed for query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Store analyzed content in vector memory for future retrieval
    /// </summary>
    public async Task StoreContentAsync(string content, string contentType, Dictionary<string, string> metadata)
    {
        try
        {
            var id = Guid.NewGuid().ToString();
            await _memory.SaveInformationAsync(
                collection: contentType,
                text: content,
                id: id,
                description: metadata.GetValueOrDefault("description", "Analyzed content")
            );

            _logger.LogInformation("Stored content in RAG memory: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store content in RAG memory", ex);
        }
    }

    /// <summary>
    /// Analyze YouTube video with RAG enhancement
    /// </summary>
    public async Task<RAGAnalysisResult> AnalyzeYouTubeVideoWithRAGAsync(string videoUrl)
    {
        // Get basic analysis
        var basicAnalysis = await _youtubeService.AnalyzeVideoAsync(videoUrl);

        // Enhance with RAG context
        var query = $"Trading insights from video: {basicAnalysis.Name}";
        var content = $"{basicAnalysis.Name}\n{basicAnalysis.Description}\n{basicAnalysis.Transcript}";

        // Store video content for future retrieval
        await StoreContentAsync(content, "youtube_videos", new Dictionary<string, string>
        {
            ["title"] = basicAnalysis.Name,
            ["url"] = videoUrl,
            ["published"] = basicAnalysis.PublishedDate.ToString(),
            ["sentiment"] = basicAnalysis.SentimentScore.ToString()
        });

        // Perform RAG analysis
        return await AnalyzeWithRAGAsync(query, "youtube_videos");
    }

    /// <summary>
    /// Analyze market trends with RAG enhancement
    /// </summary>
    public async Task<RAGAnalysisResult> AnalyzeMarketTrendsWithRAGAsync(string symbol, string timeframe = "1D")
    {
        // Get market data
        var marketData = await _marketDataService.GetMarketDataAsync(symbol);

        // Get web intelligence (check if method exists)
        var webInsights = ""; // TODO: Fix when WebIntelligenceService has GetMarketIntelligenceAsync

        // Combine and analyze with RAG
        var content = $"Market analysis for {symbol}: {marketData?.ToString() ?? "No data"}\n{webInsights}";
        var query = $"Market trend analysis and trading signals for {symbol}";

        // Store market data
        await StoreContentAsync(content, "market_data", new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["timeframe"] = timeframe,
            ["date"] = DateTime.UtcNow.ToString()
        });

        return await AnalyzeWithRAGAsync(query, "market_data");
    }

    private string BuildContextFromMemories(IEnumerable<MemoryQueryResult> memories)
    {
        return string.Join("\n\n", memories.Select(m =>
            $"Source: {m.Metadata.Id}\nContent: {m.Metadata.Text}"));
    }

    private async Task<string> GenerateRAGAnalysisAsync(string query, string context, string contentType)
    {
        var prompt = $@"
Analyze the following query using the provided context from previous analyses:

Query: {query}
Content Type: {contentType}

Context from previous analyses:
{context}

Please provide a comprehensive analysis that:
1. Synthesizes information from the context
2. Identifies key patterns and insights
3. Considers market implications
4. Provides actionable recommendations

Analysis:";

        var result = await _kernel.InvokePromptAsync(prompt);
        return result.ToString();
    }

    private async Task<List<string>> ExtractTradingSignalsWithContextAsync(string query, string analysis, string context)
    {
        var prompt = $@"
Based on the following analysis and context, extract specific trading signals:

Analysis: {analysis}

Context: {context}

Extract 3-5 specific, actionable trading signals with:
- Direction (Buy/Sell/Hold)
- Symbol/Asset
- Entry/exit points if applicable
- Risk management considerations
- Timeframe

Trading Signals:";

        var result = await _kernel.InvokePromptAsync(prompt);
        var signals = result.ToString().Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList();

        return signals;
    }

    private double CalculateConfidenceScore(IEnumerable<MemoryQueryResult> memories)
    {
        if (!memories.Any()) return 0.0;

        // Since RelevanceScore property is not available in current API, use a default calculation
        var count = memories.Count();
        return Math.Min(count / 10.0, 1.0); // Confidence based on number of relevant memories
    }
}

/// <summary>
/// Result of RAG analysis
/// </summary>
public class RAGAnalysisResult
{
    public string Query { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Analysis { get; set; } = string.Empty;
    public List<string> TradingSignals { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public List<string> Sources { get; set; } = new();
}