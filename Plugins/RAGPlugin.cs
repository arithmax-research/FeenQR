using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for RAG (Retrieval-Augmented Generation) capabilities
/// </summary>
public class RAGPlugin
{
    private readonly RAGService _ragService;

    public RAGPlugin(RAGService ragService)
    {
        _ragService = ragService;
    }

    [KernelFunction, Description("Perform RAG analysis on a query with context retrieval")]
    public async Task<string> AnalyzeWithRAGAsync(
        [Description("The query to analyze")] string query,
        [Description("Type of content to search (youtube_videos, market_data, general)")] string contentType = "general")
    {
        try
        {
            var result = await _ragService.AnalyzeWithRAGAsync(query, contentType);

            return FormatRAGResult(result);
        }
        catch (Exception ex)
        {
            return $"RAG Analysis Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze YouTube video with RAG enhancement for trading insights")]
    public async Task<string> AnalyzeYouTubeVideoWithRAGAsync(
        [Description("YouTube video URL to analyze")] string videoUrl)
    {
        try
        {
            var result = await _ragService.AnalyzeYouTubeVideoWithRAGAsync(videoUrl);

            return FormatRAGResult(result);
        }
        catch (Exception ex)
        {
            return $"YouTube RAG Analysis Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze market trends with RAG enhancement")]
    public async Task<string> AnalyzeMarketTrendsWithRAGAsync(
        [Description("Stock symbol to analyze")] string symbol,
        [Description("Timeframe for analysis (1D, 1W, 1M)")] string timeframe = "1D")
    {
        try
        {
            var result = await _ragService.AnalyzeMarketTrendsWithRAGAsync(symbol, timeframe);

            return FormatRAGResult(result);
        }
        catch (Exception ex)
        {
            return $"Market RAG Analysis Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Store content in RAG memory for future retrieval")]
    public async Task<string> StoreContentInMemoryAsync(
        [Description("Content to store")] string content,
        [Description("Type/category of content")] string contentType,
        [Description("Optional description")] string description = "")
    {
        try
        {
            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(description))
            {
                metadata["description"] = description;
            }

            await _ragService.StoreContentAsync(content, contentType, metadata);

            return $"Content stored successfully in RAG memory under category: {contentType}";
        }
        catch (Exception ex)
        {
            return $"Failed to store content: {ex.Message}";
        }
    }

    [KernelFunction, Description("Generate comprehensive trading signals using RAG context")]
    public async Task<string> GenerateRAGTradingSignalsAsync(
        [Description("Trading query or market condition")] string query)
    {
        try
        {
            var result = await _ragService.AnalyzeWithRAGAsync(query, "trading_signals");

            if (result.TradingSignals.Any())
            {
                return $"RAG-Enhanced Trading Signals (Confidence: {result.ConfidenceScore:P1}):\n\n" +
                       string.Join("\n\n", result.TradingSignals.Select((signal, i) =>
                           $"Signal {i + 1}:\n{signal}"));
            }
            else
            {
                return "No specific trading signals generated from RAG analysis.";
            }
        }
        catch (Exception ex)
        {
            return $"Trading Signal Generation Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get market intelligence with RAG context")]
    public async Task<string> GetMarketIntelligenceWithRAGAsync(
        [Description("Market sector or specific query")] string query)
    {
        try
        {
            var result = await _ragService.AnalyzeWithRAGAsync(
                $"Market intelligence for: {query}",
                "market_intelligence");

            return $"Market Intelligence Analysis:\n\n{result.Analysis}\n\n" +
                   $"Sources: {string.Join(", ", result.Sources)}\n" +
                   $"Confidence: {result.ConfidenceScore:P1}";
        }
        catch (Exception ex)
        {
            return $"Market Intelligence Error: {ex.Message}";
        }
    }

    private string FormatRAGResult(RAGAnalysisResult result)
    {
        var output = $"RAG Analysis Result (Confidence: {result.ConfidenceScore:P1})\n\n";

        output += $"Query: {result.Query}\n\n";

        if (!string.IsNullOrEmpty(result.Analysis))
        {
            output += $"Analysis:\n{result.Analysis}\n\n";
        }

        if (result.TradingSignals.Any())
        {
            output += $"Trading Signals:\n";
            for (int i = 0; i < result.TradingSignals.Count; i++)
            {
                output += $"{i + 1}. {result.TradingSignals[i]}\n";
            }
            output += "\n";
        }

        if (result.Sources.Any())
        {
            output += $"Sources: {string.Join(", ", result.Sources)}\n";
        }

        return output;
    }
}