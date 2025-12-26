using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

public class OrderBookPlugin
{
    private readonly OrderBookAnalysisService _orderBookService;

    public OrderBookPlugin(OrderBookAnalysisService orderBookService)
    {
        _orderBookService = orderBookService;
    }

    [KernelFunction, Description("Reconstruct and analyze order book for a given symbol")]
    public async Task<string> AnalyzeOrderBook(
        [Description("Stock symbol to analyze")] string symbol,
        [Description("Depth of order book to analyze (default 10)")] int depth = 10)
    {
        try
        {
            var orderBook = await _orderBookService.ReconstructOrderBook(symbol, depth);
            var analysis = _orderBookService.AnalyzeOrderBook(orderBook);

            var result = new
            {
                OrderBook = orderBook,
                Analysis = analysis
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error analyzing order book for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Generate market depth visualization data")]
    public async Task<string> GenerateDepthVisualization(
        [Description("Stock symbol")] string symbol,
        [Description("Number of levels to include (default 10)")] int levels = 10)
    {
        try
        {
            var orderBook = await _orderBookService.ReconstructOrderBook(symbol, levels);
            var visualization = _orderBookService.GenerateDepthVisualization(orderBook, levels);

            return JsonSerializer.Serialize(visualization, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error generating depth visualization for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze liquidity metrics for a symbol")]
    public async Task<string> AnalyzeLiquidity(
        [Description("Stock symbol to analyze")] string symbol)
    {
        try
        {
            var orderBook = await _orderBookService.ReconstructOrderBook(symbol);
            var liquidityMetrics = _orderBookService.AnalyzeLiquidity(orderBook);

            var result = new
            {
                Symbol = symbol,
                LiquidityMetrics = liquidityMetrics,
                Timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error analyzing liquidity for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Calculate bid-ask spread analysis")]
    public async Task<string> CalculateSpreadAnalysis(
        [Description("Stock symbol")] string symbol)
    {
        try
        {
            var orderBook = await _orderBookService.ReconstructOrderBook(symbol);
            var analysis = _orderBookService.AnalyzeOrderBook(orderBook);

            var spreadResult = new
            {
                Symbol = symbol,
                BidAskSpread = analysis.BidAskSpread,
                SpreadPercentage = analysis.SpreadPercentage,
                LiquidityRatio = analysis.LiquidityRatio,
                ImbalanceRatio = analysis.ImbalanceRatio,
                Timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(spreadResult, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error calculating spread analysis for {symbol}: {ex.Message}";
        }
    }
}