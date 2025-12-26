using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;

namespace QuantResearchAgent.Services;

public class OrderBookAnalysisService
{
    private readonly ILogger<OrderBookAnalysisService> _logger;
    private readonly AlpacaService _alpacaService;
    private readonly ILLMService _llmService;

    public OrderBookAnalysisService(
        ILogger<OrderBookAnalysisService> logger,
        AlpacaService alpacaService,
        ILLMService llmService)
    {
        _logger = logger;
        _alpacaService = alpacaService;
        _llmService = llmService;
    }

    public class OrderBookLevel
    {
        public double Price { get; set; }
        public double Quantity { get; set; }
        public int OrderCount { get; set; }
    }

    public class OrderBook
    {
        public List<OrderBookLevel> Bids { get; set; } = new();
        public List<OrderBookLevel> Asks { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; } = string.Empty;
    }

    public class OrderBookAnalysis
    {
        public double BidAskSpread { get; set; }
        public double SpreadPercentage { get; set; }
        public double MarketDepth { get; set; }
        public double LiquidityRatio { get; set; }
        public double ImbalanceRatio { get; set; }
        public Dictionary<string, double> DepthMetrics { get; set; } = new();
    }

    /// <summary>
    /// Reconstructs order book from market data
    /// </summary>
    public async Task<OrderBook> ReconstructOrderBook(string symbol, int depth = 10)
    {
        try
        {
            _logger.LogInformation($"Reconstructing order book for {symbol}");

            // Get current market data
            var quote = await _alpacaService.GetLatestQuoteAsync(symbol);
            if (quote == null)
                throw new Exception($"No quote data available for {symbol}");

            // For demonstration, create a synthetic order book
            // In production, this would use Level 2 data from exchanges
            var orderBook = new OrderBook
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow
            };

            // Generate synthetic bid levels
            double currentBid = (double)quote.BidPrice;
            for (int i = 0; i < depth; i++)
            {
                orderBook.Bids.Add(new OrderBookLevel
                {
                    Price = currentBid - (i * 0.01),
                    Quantity = 100 + (i * 50), // Decreasing quantity
                    OrderCount = 3 + i
                });
            }

            // Generate synthetic ask levels
            double currentAsk = (double)quote.AskPrice;
            for (int i = 0; i < depth; i++)
            {
                orderBook.Asks.Add(new OrderBookLevel
                {
                    Price = currentAsk + (i * 0.01),
                    Quantity = 100 + (i * 50), // Decreasing quantity
                    OrderCount = 3 + i
                });
            }

            return orderBook;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reconstructing order book for {symbol}");
            throw;
        }
    }

    /// <summary>
    /// Analyzes bid-ask spread and market depth
    /// </summary>
    public OrderBookAnalysis AnalyzeOrderBook(OrderBook orderBook)
    {
        if (orderBook.Bids.Count == 0 || orderBook.Asks.Count == 0)
            throw new ArgumentException("Order book must have bids and asks");

        var analysis = new OrderBookAnalysis();

        // Calculate bid-ask spread
        double bestBid = orderBook.Bids.Max(b => b.Price);
        double bestAsk = orderBook.Asks.Min(a => a.Price);
        analysis.BidAskSpread = bestAsk - bestBid;
        analysis.SpreadPercentage = (analysis.BidAskSpread / ((bestBid + bestAsk) / 2)) * 100;

        // Calculate market depth (total quantity within 1% of mid price)
        double midPrice = (bestBid + bestAsk) / 2;
        double depthThreshold = midPrice * 0.01;

        double bidDepth = orderBook.Bids
            .Where(b => b.Price >= midPrice - depthThreshold)
            .Sum(b => b.Quantity);

        double askDepth = orderBook.Asks
            .Where(a => a.Price <= midPrice + depthThreshold)
            .Sum(a => a.Quantity);

        analysis.MarketDepth = bidDepth + askDepth;

        // Calculate liquidity ratio (bid depth / ask depth)
        analysis.LiquidityRatio = bidDepth / askDepth;

        // Calculate imbalance ratio
        analysis.ImbalanceRatio = (bidDepth - askDepth) / (bidDepth + askDepth);

        // Additional depth metrics
        analysis.DepthMetrics["BidDepth"] = bidDepth;
        analysis.DepthMetrics["AskDepth"] = askDepth;
        analysis.DepthMetrics["TotalDepth"] = analysis.MarketDepth;
        analysis.DepthMetrics["DepthImbalance"] = analysis.ImbalanceRatio;

        return analysis;
    }

    /// <summary>
    /// Generates market depth visualization data
    /// </summary>
    public Dictionary<string, object> GenerateDepthVisualization(OrderBook orderBook, int levels = 10)
    {
        var visualization = new Dictionary<string, object>();

        // Prepare bid data for visualization
        var bidData = orderBook.Bids.Take(levels).Select((b, i) => new
        {
            Level = i + 1,
            Price = b.Price,
            Quantity = b.Quantity,
            CumulativeQuantity = orderBook.Bids.Take(i + 1).Sum(x => x.Quantity)
        }).ToList();

        // Prepare ask data for visualization
        var askData = orderBook.Asks.Take(levels).Select((a, i) => new
        {
            Level = i + 1,
            Price = a.Price,
            Quantity = a.Quantity,
            CumulativeQuantity = orderBook.Asks.Take(i + 1).Sum(x => x.Quantity)
        }).ToList();

        visualization["Bids"] = bidData;
        visualization["Asks"] = askData;
        visualization["Symbol"] = orderBook.Symbol;
        visualization["Timestamp"] = orderBook.Timestamp;

        return visualization;
    }

    /// <summary>
    /// Analyzes liquidity metrics
    /// </summary>
    public Dictionary<string, double> AnalyzeLiquidity(OrderBook orderBook)
    {
        var metrics = new Dictionary<string, double>();

        if (orderBook.Bids.Count == 0 || orderBook.Asks.Count == 0)
            return metrics;

        double bestBid = orderBook.Bids.Max(b => b.Price);
        double bestAsk = orderBook.Asks.Min(a => a.Price);
        double midPrice = (bestBid + bestAsk) / 2;

        // Spread-based liquidity
        metrics["RelativeSpread"] = (bestAsk - bestBid) / midPrice;

        // Depth-based liquidity
        double bidDepth = orderBook.Bids.Sum(b => b.Quantity);
        double askDepth = orderBook.Asks.Sum(a => a.Quantity);
        metrics["BidDepth"] = bidDepth;
        metrics["AskDepth"] = askDepth;
        metrics["TotalDepth"] = bidDepth + askDepth;

        // Order book slope (price impact per unit quantity)
        if (orderBook.Bids.Count > 1)
        {
            var bidSlope = CalculateSlope(orderBook.Bids.Select(b => (b.Price, b.Quantity)).ToList());
            metrics["BidSlope"] = bidSlope;
        }

        if (orderBook.Asks.Count > 1)
        {
            var askSlope = CalculateSlope(orderBook.Asks.Select(a => (a.Price, a.Quantity)).ToList());
            metrics["AskSlope"] = askSlope;
        }

        return metrics;
    }

    private double CalculateSlope(List<(double Price, double Quantity)> levels)
    {
        if (levels.Count < 2) return 0;

        // Calculate cumulative quantity and fit linear regression
        var cumulativeData = new List<(double CumulativeQty, double Price)>();
        double cumQty = 0;

        foreach (var level in levels.OrderByDescending(l => l.Quantity)) // Sort by quantity descending
        {
            cumQty += level.Quantity;
            cumulativeData.Add((cumQty, level.Price));
        }

        // Simple slope calculation using first and last points
        if (cumulativeData.Count >= 2)
        {
            var first = cumulativeData.First();
            var last = cumulativeData.Last();
            return (last.Price - first.Price) / (last.CumulativeQty - first.CumulativeQty);
        }

        return 0;
    }
}