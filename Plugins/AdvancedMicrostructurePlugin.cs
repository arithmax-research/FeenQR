using System.ComponentModel;
using Microsoft.SemanticKernel;
using Feen.Services;

namespace Feen.Plugins
{
    /// <summary>
    /// Plugin for advanced market microstructure analysis
    /// </summary>
    public class AdvancedMicrostructurePlugin
    {
        private readonly AdvancedMicrostructureService _microstructureService;

        public AdvancedMicrostructurePlugin(AdvancedMicrostructureService microstructureService)
        {
            _microstructureService = microstructureService;
        }

        [KernelFunction, Description("Reconstructs real-time order book across multiple exchanges")]
        public async Task<string> ReconstructOrderBook(
            [Description("Stock symbol to analyze")] string symbol)
        {
            try
            {
                var reconstruction = await _microstructureService.ReconstructOrderBookAsync(symbol);

                return $@"Order Book Reconstruction for {symbol}:

Aggregated Book:
- Best Bid: {reconstruction.AggregatedBook!.BestBid:C}
- Best Ask: {reconstruction.AggregatedBook!.BestAsk:C}
- Spread: {reconstruction.AggregatedBook!.Spread:C}
- Mid Price: {reconstruction.AggregatedBook!.MidPrice:C}
- Total Bid Volume: {reconstruction.AggregatedBook!.TotalBidVolume:N0}
- Total Ask Volume: {reconstruction.AggregatedBook!.TotalAskVolume:N0}
- Imbalance: {reconstruction.AggregatedBook!.Imbalance:P2}

Microstructure Metrics:
- Effective Spread: {reconstruction.MicrostructureMetrics!.EffectiveSpread:C}
- Realized Spread: {reconstruction.MicrostructureMetrics!.RealizedSpread:C}
- Price Impact: {reconstruction.MicrostructureMetrics!.PriceImpact:C}
- Depth: {reconstruction.MicrostructureMetrics!.Depth:N0}
- Resilience: {reconstruction.MicrostructureMetrics!.Resilience:P2}

Exchange Details:
{string.Join("\n\n", reconstruction.Exchanges!.Select(ex => $@"{ex.Exchange}:
- Best Bid: {ex.Bids!.FirstOrDefault()?.Price:C ?? 0:C}
- Best Ask: {ex.Asks!.FirstOrDefault()?.Price:C ?? 0:C}
- Last Update: {ex.LastUpdate:HH:mm:ss}"))}";
            }
            catch (Exception ex)
            {
                return $"Error reconstructing order book for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes high-frequency trading patterns and algorithms")]
        public async Task<string> AnalyzeHighFrequencyTrading(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Analysis window in minutes")] int analysisWindowMinutes = 5)
        {
            try
            {
                var hftAnalysis = await _microstructureService.AnalyzeHighFrequencyTradingAsync(symbol, analysisWindowMinutes);

                return $@"HFT Analysis for {symbol} ({hftAnalysis.AnalysisWindow.TotalMinutes} min window):

Total Trades: {hftAnalysis.TotalTrades:N0}
HFT Participation: {hftAnalysis.HFTParticipation:P2}

Algorithm Types:
{string.Join("\n", hftAnalysis.AlgorithmTypes?.Select(kvp =>
    $"- {kvp.Key}: {kvp.Value:P2}") ?? new List<string>())}

Trade Size Distribution:
{string.Join("\n", hftAnalysis.TradeSizeDistribution?.Select(kvp =>
    $"- {kvp.Key}: {kvp.Value:N0}") ?? new List<string>())}

Latency Metrics:
- Average: {(hftAnalysis.LatencyMetrics!.AverageLatency * 1000000):F0} μs
- Median: {(hftAnalysis.LatencyMetrics!.MedianLatency * 1000000):F0} μs
- P95: {(hftAnalysis.LatencyMetrics!.P95Latency * 1000000):F0} μs
- P99: {(hftAnalysis.LatencyMetrics!.P99Latency * 1000000):F0} μs

Market Quality:
- Quoted Spread: {hftAnalysis.MarketQuality!.QuotedSpread:C}
- Effective Spread: {hftAnalysis.MarketQuality!.EffectiveSpread:C}
- Depth: {hftAnalysis.MarketQuality!.Depth:N0}
- Volatility: {hftAnalysis.MarketQuality!.Volatility:P2}";
            }
            catch (Exception ex)
            {
                return $"Error analyzing HFT patterns for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes liquidity patterns and optimal execution strategies")]
        public async Task<string> AnalyzeLiquidity(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Order size to analyze")] int orderSize = 10000)
        {
            try
            {
                var liquidityAnalysis = await _microstructureService.AnalyzeLiquidityAsync(symbol, orderSize);

                return $@"Liquidity Analysis for {symbol} (Order Size: {orderSize:N0}):

Current Liquidity:
- Bid Depth: {liquidityAnalysis.CurrentLiquidity!.BidDepth:N0}
- Ask Depth: {liquidityAnalysis.CurrentLiquidity!.AskDepth:N0}
- Spread: {liquidityAnalysis.CurrentLiquidity!.Spread:C}
- Market Depth: {liquidityAnalysis.CurrentLiquidity!.MarketDepth:N0}
- Turnover Ratio: {liquidityAnalysis.CurrentLiquidity!.TurnoverRatio:F2}

Optimal Execution Strategy: {liquidityAnalysis.OptimalExecution!.Strategy}
- Estimated Slippage: {liquidityAnalysis.OptimalExecution!.EstimatedSlippage:P2}
- Estimated Market Impact: {liquidityAnalysis.OptimalExecution!.EstimatedMarketImpact:P2}
- Time Horizon: {liquidityAnalysis.OptimalExecution!.RecommendedTimeHorizon.TotalMinutes:F0} minutes
- Confidence: {liquidityAnalysis.OptimalExecution!.ConfidenceScore:P2}

Liquidity Risk Metrics:
- Illiquidity Ratio: {liquidityAnalysis.LiquidityRisk!.IlliquidityRatio:F2}
- Amihud Ratio: {liquidityAnalysis.LiquidityRisk!.AmihudRatio:F2}
- Roll Spread: {liquidityAnalysis.LiquidityRisk!.RollSpread:C}
- Effective Tick: {liquidityAnalysis.LiquidityRisk!.EffectiveTick:F6}

Alternative Strategies:
{string.Join("\n", liquidityAnalysis.AlternativeStrategies!.Select(s =>
    $"- {s.Strategy}: Slippage {s.EstimatedSlippage:P2}, Impact {s.EstimatedMarketImpact:P2}, Confidence {s.ConfidenceScore:P2}"))}";
            }
            catch (Exception ex)
            {
                return $"Error analyzing liquidity for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Detects market manipulation patterns and anomalies")]
        public async Task<string> DetectMarketManipulation(
            [Description("Stock symbol to analyze")] string symbol)
        {
            try
            {
                var detection = await _microstructureService.DetectMarketManipulationAsync(symbol);

                return $@"Market Manipulation Detection for {symbol}:

Manipulation Score: {detection.ManipulationScore:F2}
Overall Risk Assessment: {detection.OverallRiskAssessment}

Detected Patterns:
{string.Join("\n", detection.DetectedPatterns?.Select(p =>
    $"- {p.PatternType}: Confidence {p.Confidence:F2}, Severity {p.Severity}") ?? new List<string>())}

Market Anomalies:
{string.Join("\n", detection.Anomalies?.Select(a =>
    $"- {a.AnomalyType}: Magnitude {a.Magnitude:F1}, Risk {a.RiskLevel}") ?? new List<string>())}

Regulatory Flags: {string.Join(", ", detection.RegulatoryFlags!)}";
            }
            catch (Exception ex)
            {
                return $"Error detecting market manipulation for {symbol}: {ex.Message}";
            }
        }
    }
}