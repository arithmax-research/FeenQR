using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class LiveStrategyPlugin
    {
        private readonly LiveStrategyService _liveStrategyService;

        public LiveStrategyPlugin(LiveStrategyService liveStrategyService)
        {
            _liveStrategyService = liveStrategyService;
        }

        [KernelFunction, Description("Deploy a live trading strategy with risk management")]
        public async Task<string> DeployLiveStrategy(
            [Description("Name of the trading strategy")] string strategyName,
            [Description("Stock symbol to trade")] string symbol,
            [Description("Maximum position size as percentage of portfolio (0-100)")] decimal maxPositionSize = 10.0m,
            [Description("Stop loss percentage")] decimal stopLossPercent = 5.0m,
            [Description("Take profit percentage")] decimal takeProfitPercent = 10.0m,
            [Description("Rebalance frequency in minutes")] int rebalanceFrequencyMinutes = 60,
            [Description("Enable automatic rebalancing")] bool enableAutoRebalancing = true)
        {
            try
            {
                var config = new LiveStrategyService.LiveStrategyConfig
                {
                    StrategyName = strategyName,
                    Symbol = symbol,
                    MaxPositionSize = maxPositionSize,
                    StopLossPercent = stopLossPercent,
                    TakeProfitPercent = takeProfitPercent,
                    RebalanceFrequencyMinutes = rebalanceFrequencyMinutes,
                    EnableAutoRebalancing = enableAutoRebalancing,
                    StrategyParameters = new Dictionary<string, object>()
                };

                var strategyId = await _liveStrategyService.DeployLiveStrategyAsync(config);
                return $"Live strategy '{strategyName}' deployed successfully with ID: {strategyId}";
            }
            catch (Exception ex)
            {
                return $"Error deploying live strategy: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get performance metrics for a live strategy")]
        public async Task<string> GetStrategyPerformance(
            [Description("Strategy ID to check performance for")] string strategyId)
        {
            try
            {
                var performance = await _liveStrategyService.GetStrategyPerformanceAsync(strategyId);

                return $"Strategy Performance:\n" +
                       $"- Current P&L: {performance.CurrentPnL:C}\n" +
                       $"- Daily P&L: {performance.DailyPnL:C}\n" +
                       $"- Sharpe Ratio: {performance.SharpeRatio:F2}\n" +
                       $"- Max Drawdown: {performance.MaxDrawdown:P}\n" +
                       $"- Win Rate: {performance.WinRate:P}\n" +
                       $"- Total Trades: {performance.TotalTrades}\n" +
                       $"- Last Update: {performance.LastUpdate}";
            }
            catch (Exception ex)
            {
                return $"Error getting strategy performance: {ex.Message}";
            }
        }

        [KernelFunction, Description("Adjust position size for a live strategy")]
        public async Task<string> AdjustStrategyPosition(
            [Description("Strategy ID")] string strategyId,
            [Description("Stock symbol")] string symbol,
            [Description("Target position size")] decimal targetPosition)
        {
            try
            {
                var success = await _liveStrategyService.AdjustStrategyPositionAsync(strategyId, symbol, targetPosition);

                if (success)
                {
                    return $"Strategy position adjusted successfully: {symbol} to {targetPosition} shares";
                }
                else
                {
                    return "Failed to adjust strategy position";
                }
            }
            catch (Exception ex)
            {
                return $"Error adjusting strategy position: {ex.Message}";
            }
        }

        [KernelFunction, Description("Stop a live trading strategy and close positions")]
        public async Task<string> StopLiveStrategy(
            [Description("Strategy ID to stop")] string strategyId)
        {
            try
            {
                var success = await _liveStrategyService.StopStrategyAsync(strategyId);

                if (success)
                {
                    return $"Live strategy {strategyId} stopped successfully";
                }
                else
                {
                    return "Failed to stop live strategy";
                }
            }
            catch (Exception ex)
            {
                return $"Error stopping live strategy: {ex.Message}";
            }
        }
    }
}