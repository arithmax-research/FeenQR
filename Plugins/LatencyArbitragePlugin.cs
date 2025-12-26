using System.ComponentModel;
using Microsoft.SemanticKernel;
using Feen.Services;

namespace Feen.Plugins
{
    /// <summary>
    /// Plugin for latency arbitrage detection and analysis
    /// </summary>
    public class LatencyArbitragePlugin
    {
        private readonly LatencyArbitrageService _latencyArbitrageService;

        public LatencyArbitragePlugin(LatencyArbitrageService latencyArbitrageService)
        {
            _latencyArbitrageService = latencyArbitrageService;
        }

        [KernelFunction, Description("Detects cross-exchange latency arbitrage opportunities")]
        public async Task<string> DetectLatencyArbitrage(
            [Description("Stock symbol to analyze")] string symbol)
        {
            try
            {
                var analysis = await _latencyArbitrageService.DetectLatencyArbitrageAsync(symbol);

                return $@"Latency Arbitrage Analysis for {symbol}:

Active Arbitrageurs: {analysis.ActiveArbitrageurs}
Success Rate: {analysis.SuccessRate:P2}

Latency Metrics:
- Average: {(analysis.LatencyMetrics!.AverageLatency * 1000000):F0} μs
- Median: {(analysis.LatencyMetrics!.MedianLatency * 1000000):F0} μs
- P95: {(analysis.LatencyMetrics!.P95Latency * 1000000):F0} μs
- P99: {(analysis.LatencyMetrics!.P99Latency * 1000000):F0} μs

Arbitrage Opportunities:
{string.Join("\n\n", analysis.ArbitrageOpportunities!.Select(opp => $@"{opp.BuyExchange} ↔ {opp.SellExchange}
- Price Difference: {opp.PriceDifference:C}
- Latency Gap: {(opp.LatencyGap * 1000000):F0} μs
- Estimated Profit: {opp.EstimatedProfit:C}
- Confidence: {opp.Confidence:P2}
- Risk Level: {opp.RiskLevel}
- Time to Live: {opp.TimeToLive.TotalMilliseconds:F0} ms"))}

Network Topology:
Data Centers: {string.Join(", ", analysis.NetworkTopology!.DataCenters!.Select(dc => $"{dc.Name} ({dc.Location}: {(dc.Latency * 1000000):F0} μs)"))}
Optimal Routes: {string.Join(", ", analysis.NetworkTopology!.OptimalRoutes!)}";
            }
            catch (Exception ex)
            {
                return $"Error detecting latency arbitrage for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes co-location advantages and latency optimization")]
        public async Task<string> AnalyzeCoLocationAdvantages(
            [Description("Exchange to analyze")] string exchange)
        {
            try
            {
                var analysis = await _latencyArbitrageService.AnalyzeCoLocationAdvantagesAsync(exchange);

                return $@"Co-Location Analysis for {exchange}:

Performance Metrics:
- Average Latency: {(analysis.PerformanceMetrics!.AverageLatency * 1000000):F0} μs
- Jitter: {(analysis.PerformanceMetrics!.Jitter * 1000000):F0} μs
- Packet Loss: {analysis.PerformanceMetrics!.PacketLoss:P4}
- Throughput: {analysis.PerformanceMetrics!.Throughput:N0} msg/s

Co-Location Tiers:
{string.Join("\n\n", analysis.CoLocationTiers!.Select(tier => $@"{tier.Tier}:
- Distance: {(tier.Distance * 1000000):F0} μs
- Monthly Cost: ${tier.MonthlyCost:N0}
- Latency Advantage: {(tier.LatencyAdvantage * 1000000):F0} μs
- Bandwidth: {tier.Bandwidth}
- Power Backup: {tier.PowerBackup}"))}

Cost-Benefit Analysis:
- Break-even Trades: {analysis.CostBenefitAnalysis!.BreakEvenTrades:N0}
- Monthly Revenue: ${analysis.CostBenefitAnalysis!.MonthlyRevenue:N0}
- ROI: {analysis.CostBenefitAnalysis!.ROI:F2}
- Payback Period: {analysis.CostBenefitAnalysis!.PaybackPeriod.TotalDays:N0} days";
            }
            catch (Exception ex)
            {
                return $"Error analyzing co-location advantages for {exchange}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes order routing latency and optimization")]
        public async Task<string> AnalyzeOrderRouting(
            [Description("Stock symbol")] string symbol,
            [Description("Order type (Market/Limit/Stop)")] string orderType)
        {
            try
            {
                var analysis = await _latencyArbitrageService.AnalyzeOrderRoutingAsync(symbol, orderType);

                return $@"Order Routing Analysis for {symbol} ({orderType}):

Optimal Strategy: {analysis.OptimalStrategy}

Routing Strategies:
{string.Join("\n", analysis.RoutingStrategies!.Select(s => $@"- {s.Strategy}:
  • Latency: {(s.AverageLatency * 1000000):F0} μs
  • Success Rate: {s.SuccessRate:P2}
  • Cost: {s.Cost:C}
  • Reliability: {s.Reliability:P2}"))}

Latency Breakdown:
- Network: {(analysis.LatencyBreakdown!.NetworkLatency * 1000000):F0} μs
- Processing: {(analysis.LatencyBreakdown!.ProcessingLatency * 1000000):F0} μs
- Queue: {(analysis.LatencyBreakdown!.QueueLatency * 1000000):F0} μs
- Exchange: {(analysis.LatencyBreakdown!.ExchangeLatency * 1000000):F0} μs
- Total: {(analysis.LatencyBreakdown!.TotalLatency * 1000000):F0} μs

Performance Metrics:
- Fill Rate: {analysis.PerformanceMetrics!.FillRate:P2}
- Slippage: {analysis.PerformanceMetrics!.Slippage:P2}
- Market Impact: {analysis.PerformanceMetrics!.MarketImpact:P2}
- Execution Quality: {analysis.PerformanceMetrics!.ExecutionQuality:P2}";
            }
            catch (Exception ex)
            {
                return $"Error analyzing order routing for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes market data feed latency and quality")]
        public async Task<string> AnalyzeMarketDataFeeds(
            [Description("Stock symbol to analyze")] string symbol)
        {
            try
            {
                var analysis = await _latencyArbitrageService.AnalyzeMarketDataFeedsAsync(symbol);

                return $@"Market Data Feed Analysis for {symbol}:

Optimal Feed: {analysis.OptimalFeed}

Feed Providers:
{string.Join("\n", analysis.FeedProviders!.Select(f => $@"- {f.Provider}:
  • Latency: {(f.Latency * 1000000):F0} μs
  • Update Frequency: {f.UpdateFrequency:N0}/s
  • Coverage: {f.Coverage:P2}
  • Reliability: {f.Reliability:P4}
  • Cost: ${f.Cost:N0}/month"))}

Latency Distribution:
- P50: {(analysis.LatencyDistribution!.P50 * 1000000):F0} μs
- P95: {(analysis.LatencyDistribution!.P95 * 1000000):F0} μs
- P99: {(analysis.LatencyDistribution!.P99 * 1000000):F0} μs
- Max: {(analysis.LatencyDistribution!.MaxLatency * 1000000):F0} μs

Data Quality:
- Completeness: {analysis.DataQuality!.Completeness:P4}
- Accuracy: {analysis.DataQuality!.Accuracy:P4}
- Timeliness: {analysis.DataQuality!.Timeliness:P4}
- Consistency: {analysis.DataQuality!.Consistency:P4}";
            }
            catch (Exception ex)
            {
                return $"Error analyzing market data feeds for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Calculates theoretical latency arbitrage profit potential")]
        public async Task<string> CalculateArbitrageProfitability(
            [Description("Stock symbol")] string symbol,
            [Description("Capital amount")] decimal capital = 1000000)
        {
            try
            {
                var profitability = await _latencyArbitrageService.CalculateArbitrageProfitabilityAsync(symbol, capital);

                return $@"Arbitrage Profitability Analysis for {symbol}:

Capital: ${capital:N0}
Daily Opportunities: {profitability!.DailyOpportunities}
Average Profit/Trade: ${profitability!.AverageProfitPerTrade:F2}
Win Rate: {profitability!.WinRate:P2}

Expected Returns:
- Daily: ${profitability!.ExpectedDailyProfit:F2}
- Monthly: ${profitability!.ExpectedMonthlyProfit:F2}

Risk Metrics:
- Sharpe Ratio: {profitability!.SharpeRatio:F2}
- Max Drawdown: {profitability!.MaxDrawdown:P2}
- VaR (95%): {profitability!.RiskMetrics!.ValueAtRisk:P2}
- Expected Shortfall: {profitability!.RiskMetrics!.ExpectedShortfall:P2}

Infrastructure Costs (Monthly):
- Co-location: ${profitability!.InfrastructureCosts!.CoLocation:N0}
- Data Feeds: ${profitability!.InfrastructureCosts!.DataFeeds:N0}
- Technology: ${profitability!.InfrastructureCosts!.Technology:N0}
- Personnel: ${profitability!.InfrastructureCosts!.Personnel:N0}
- Total: ${profitability!.InfrastructureCosts!.TotalMonthly:N0}

Net Profitability:
- Monthly Revenue: ${profitability!.NetProfitability!.MonthlyRevenue:F2}
- Monthly Costs: ${profitability!.NetProfitability!.MonthlyCosts:F2}
- Monthly Net: ${profitability!.NetProfitability!.MonthlyNet:+0.00;-0.00}
- Break-even Trades: {profitability!.NetProfitability!.BreakEvenTrades:N0}
- Break-even Capital: ${profitability!.NetProfitability!.BreakEvenCapital:N0}";
            }
            catch (Exception ex)
            {
                return $"Error calculating arbitrage profitability for {symbol}: {ex.Message}";
            }
        }
    }
}