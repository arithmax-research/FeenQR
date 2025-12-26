using System.ComponentModel;
using Microsoft.SemanticKernel;
using Feen.Services;

namespace Feen.Plugins
{
    /// <summary>
    /// Plugin for volatility trading tools and VIX futures analysis
    /// </summary>
    public class VolatilityTradingPlugin
    {
        private readonly VolatilityTradingService _volatilityTradingService;

        public VolatilityTradingPlugin(VolatilityTradingService volatilityTradingService)
        {
            _volatilityTradingService = volatilityTradingService;
        }

        [KernelFunction, Description("Analyzes implied volatility surface for a symbol")]
        public async Task<string> AnalyzeVolatilitySurface(
            [Description("Stock symbol to analyze")] string symbol)
        {
            try
            {
                var surface = await _volatilityTradingService.AnalyzeVolatilitySurfaceAsync(symbol);

                return $@"Volatility Surface Analysis for {symbol}:

Spot Price: {surface.SpotPrice:C}
Skew: {surface.Skew:F3}
Kurtosis: {surface.Kurtosis:F2}
Term Structure: {surface.TermStructure}
Risk Reversal: {surface.RiskReversal:F3}

Volatility Matrix:
{string.Join("\n", surface.VolatilityMatrix.Select(kvp =>
    $"{kvp.Key}: {string.Join(", ", kvp.Value.OrderBy(x => x.Key).Select(x => $"{x.Key:C}: {x.Value:P2}"))}"))}";
            }
            catch (Exception ex)
            {
                return $"Error analyzing volatility surface for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes VIX futures and volatility products")]
        public async Task<string> AnalyzeVIXFutures()
        {
            try
            {
                var vixAnalysis = await _volatilityTradingService.AnalyzeVIXFuturesAsync();

                return $@"VIX Futures Analysis:

VIX Spot: {vixAnalysis.VIXSpot:F2} ({vixAnalysis.VIXChange:+0.00;-0.00})
Market Regime: {vixAnalysis.MarketRegime}
Contango: {vixAnalysis.Contango:F3}
Signal Strength: {vixAnalysis.SignalStrength:P2}

Futures Curve:
{string.Join("\n", vixAnalysis.FuturesCurve.Select(f =>
    $"{f.Month}: {f.Price:F2} ({f.Change:+0.00;-0.00})"))}

VIX ETFs:
{string.Join("\n", vixAnalysis.VIXETFs.Select(etf =>
    $"{etf.Symbol}: {etf.Price:C} ({etf.Change:+0.00;-0.00}%) Vol: {etf.Volume:N0}"))}";
            }
            catch (Exception ex)
            {
                return $"Error analyzing VIX futures: {ex.Message}";
            }
        }

        [KernelFunction, Description("Calculates volatility trading strategies and signals")]
        public async Task<string> CalculateVolatilityStrategy(
            [Description("Stock symbol")] string symbol,
            [Description("Strategy type (Long Volatility/Short Volatility/Volatility Arbitrage)")] string strategyType)
        {
            try
            {
                var strategy = await _volatilityTradingService.CalculateVolatilityStrategyAsync(symbol, strategyType);

                return $@"{strategyType} Strategy for {symbol}:

Current Volatility: {strategy.CurrentVolatility:P2}
Historical Volatility: {strategy.HistoricalVolatility:P2}
Implied Volatility: {strategy.ImpliedVolatility:P2}
Volatility Ratio: {strategy.VolatilityRatio:F2}

Signal: {strategy.Signal}
Confidence: {strategy.Confidence:P2}

Recommended Position:
- Direction: {strategy.RecommendedPosition.Direction}
- Size: {strategy.RecommendedPosition.Size:N0}
- Entry Price: {strategy.RecommendedPosition.EntryPrice:C}
- Stop Loss: {strategy.RecommendedPosition.StopLoss:C}
- Take Profit: {strategy.RecommendedPosition.TakeProfit:C}
- Risk/Reward Ratio: {strategy.RecommendedPosition.RiskRewardRatio:F2}

Risk Metrics:
- Max Drawdown: {strategy.RiskMetrics.MaxDrawdown:P2}
- Sharpe Ratio: {strategy.RiskMetrics.SharpeRatio:F2}
- Sortino Ratio: {strategy.RiskMetrics.SortinoRatio:F2}
- Value at Risk: {strategy.RiskMetrics.ValueAtRisk:P2}";
            }
            catch (Exception ex)
            {
                return $"Error calculating volatility strategy for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Monitors volatility skew and term structure changes")]
        public async Task<string> MonitorVolatilityChanges(
            [Description("Stock symbol to monitor")] string symbol)
        {
            try
            {
                var monitor = await _volatilityTradingService.MonitorVolatilityChangesAsync(symbol);

                return $@"Volatility Monitor for {symbol}:

Trend Direction: {monitor.TrendDirection}
Momentum Score: {monitor.MomentumScore:F2}
Skew Change: {monitor.SkewChange:F3}
Term Structure Change: {monitor.TermStructureChange}

Volatility Changes:
{string.Join("\n", monitor.VolatilityChange.Select(kvp =>
    $"{kvp.Key}: {kvp.Value:+0.00;-0.00}"))}

Alerts:
{string.Join("\n", monitor.Alerts.Select(a =>
    $"{a.Timestamp:HH:mm:ss} [{a.Severity}] {a.AlertType}: {a.Message}"))}";
            }
            catch (Exception ex)
            {
                return $"Error monitoring volatility changes for {symbol}: {ex.Message}";
            }
        }
    }
}