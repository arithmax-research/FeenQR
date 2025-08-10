using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for risk management and portfolio operations
/// </summary>
public class RiskManagementPlugin
{
    private readonly RiskManagementService _riskService;
    private readonly PortfolioService _portfolioService;

    public RiskManagementPlugin(
        RiskManagementService riskService,
        PortfolioService portfolioService)
    {
        _riskService = riskService;
        _portfolioService = portfolioService;
    }

    [KernelFunction, Description("Assess current portfolio risk and get recommendations")]
    public async Task<string> AssessPortfolioRiskAsync()
    {
        try
        {
            var riskAssessment = await _riskService.AssessPortfolioRiskAsync();
            return $"RISK: Portfolio Risk Assessment:\n\n{riskAssessment}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Error assessing portfolio risk: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get current portfolio positions and performance")]
    public async Task<string> GetPortfolioSummaryAsync()
    {
        try
        {
            var positions = await _portfolioService.GetPositionsAsync();
            var metrics = await _portfolioService.CalculateMetricsAsync();
            var cashBalance = _portfolioService.GetCashBalance();
            
            if (!positions.Any())
            {
                return $"MONEY: Portfolio Summary:\n\n" +
                       $"Cash Balance: ${cashBalance:F2}\n" +
                       $"Total Portfolio Value: ${metrics.TotalValue:F2}\n" +
                       $"No active positions.";
            }
            
            var result = $"MONEY: Portfolio Summary:\n\n" +
                        $"Total Value: ${metrics.TotalValue:F2}\n" +
                        $"Cash Balance: ${cashBalance:F2}\n" +
                        $"Total P&L: ${metrics.TotalPnL:F2} ({metrics.TotalPnLPercent:F2}%)\n" +
                        $"Active Positions: {positions.Count}\n\n" +
                        $" Performance Metrics:\n" +
                        $"• Daily Return: {metrics.DailyReturn:F2}%\n" +
                        $"• Volatility: {metrics.Volatility:F2}%\n" +
                        $"• Sharpe Ratio: {metrics.SharpeRatio:F2}\n" +
                        $"• Max Drawdown: {metrics.MaxDrawdown:F2}%\n" +
                        $"• Win Rate: {metrics.WinRate:F2}%\n\n" +
                        $"TARGET: Current Positions:\n";
            
            foreach (var position in positions.OrderByDescending(p => Math.Abs(p.UnrealizedPnL)))
            {
                var pnlIndicator = position.UnrealizedPnL >= 0 ? "" : "TREND:";
                var positionValue = position.Quantity * position.CurrentPrice;
                
                result += $"{pnlIndicator} {position.Symbol}\n" +
                         $"   Quantity: {position.Quantity:F6}\n" +
                         $"   Avg Price: ${position.AveragePrice:F2}\n" +
                         $"   Current: ${position.CurrentPrice:F2}\n" +
                         $"   Value: ${positionValue:F2}\n" +
                         $"   P&L: ${position.UnrealizedPnL:F2} ({position.UnrealizedPnLPercent:F2}%)\n\n";
            }
            
            return result.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"ERROR: Error retrieving portfolio summary: {ex.Message}";
        }
    }

    [KernelFunction, Description("Calculate optimal position size for a trading signal")]
    public async Task<string> CalculatePositionSizeAsync(
        [Description("Trading symbol")] string symbol,
        [Description("Signal strength (0.0-1.0)")] double signalStrength,
        [Description("Current market price")] double currentPrice)
    {
        try
        {
            if (signalStrength < 0 || signalStrength > 1)
            {
                return "ERROR: Signal strength must be between 0.0 and 1.0";
            }
            
            // Create a mock signal for position sizing calculation
            var mockSignal = new Core.TradingSignal
            {
                Symbol = symbol.ToUpper(),
                Strength = signalStrength,
                Price = currentPrice,
                Type = Core.SignalType.Buy // Default for calculation
            };
            
            var quantity = await _riskService.CalculatePositionSizeAsync(mockSignal);
            var positionValue = quantity * currentPrice;
            var metrics = await _portfolioService.CalculateMetricsAsync();
            var portfolioPercentage = metrics.TotalValue > 0 ? (positionValue / metrics.TotalValue) * 100 : 0;
            
            return $"ANALYSIS: Position Size Calculation for {symbol}:\n\n" +
                   $"Signal Strength: {signalStrength:F2}\n" +
                   $"Current Price: ${currentPrice:F2}\n" +
                   $"Recommended Quantity: {quantity:F6}\n" +
                   $"Position Value: ${positionValue:F2}\n" +
                   $"Portfolio Allocation: {portfolioPercentage:F2}%\n\n" +
                   $"Risk Management Applied:\n" +
                   $"• Volatility adjustment\n" +
                   $"• Signal strength scaling\n" +
                   $"• Portfolio diversification limits";
        }
        catch (Exception ex)
        {
            return $"ERROR: Error calculating position size: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get risk management configuration and limits")]
    public string GetRiskParameters()
    {
        try
        {
            var config = _riskService.GetConfig();
            
            return $"RISK: Risk Management Parameters:\n\n" +
                   $"Portfolio Limits:\n" +
                   $"• Max Drawdown: {config.MaxDrawdown:P}\n" +
                   $"• Volatility Target: {config.VolatilityTarget:P}\n" +
                   $"• Max Positions: {config.MaxPositions}\n" +
                   $"• Position Size: {config.PositionSizePercent:P} per trade\n\n" +
                   $"Trade Management:\n" +
                   $"• Stop Loss: {config.StopLossPercent:P}\n" +
                   $"• Take Profit: {config.TakeProfitPercent:P}\n\n" +
                   $"These parameters ensure responsible risk management\n" +
                   $"and protect against excessive losses.";
        }
        catch (Exception ex)
        {
            return $"ERROR: Error retrieving risk parameters: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze portfolio diversification and concentration risk")]
    public async Task<string> AnalyzeDiversificationAsync()
    {
        try
        {
            var positions = await _portfolioService.GetPositionsAsync();
            
            if (!positions.Any())
            {
                return "ANALYSIS: Diversification Analysis:\n\nNo positions to analyze.";
            }
            
            var totalValue = positions.Sum(p => p.Quantity * p.CurrentPrice);
            var positionsByValue = positions
                .Select(p => new { 
                    Symbol = p.Symbol, 
                    Value = p.Quantity * p.CurrentPrice,
                    Percentage = totalValue > 0 ? (p.Quantity * p.CurrentPrice / totalValue) * 100 : 0
                })
                .OrderByDescending(p => p.Value)
                .ToList();
            
            // Calculate concentration metrics
            var herfindahlIndex = positionsByValue.Sum(p => Math.Pow(p.Percentage / 100, 2));
            var effectiveNumPositions = herfindahlIndex > 0 ? 1.0 / herfindahlIndex : 0;
            var largestPosition = positionsByValue.First().Percentage;
            var top3Concentration = positionsByValue.Take(3).Sum(p => p.Percentage);
            
            var result = $"ANALYSIS: Portfolio Diversification Analysis:\n\n" +
                        $"Concentration Metrics:\n" +
                        $"• Herfindahl Index: {herfindahlIndex:F4}\n" +
                        $"• Effective # Positions: {effectiveNumPositions:F1}\n" +
                        $"• Largest Position: {largestPosition:F1}%\n" +
                        $"• Top 3 Concentration: {top3Concentration:F1}%\n\n" +
                        $"Position Breakdown:\n";
            
            foreach (var position in positionsByValue)
            {
                var riskLevel = position.Percentage switch
                {
                    > 30 => "[NEGATIVE] High Risk",
                    > 20 => "[NEUTRAL] Medium Risk", 
                    > 10 => "[POSITIVE] Low Risk",
                    _ => "SUCCESS: Safe"
                };
                
                result += $"• {position.Symbol}: {position.Percentage:F1}% {riskLevel}\n";
            }
            
            // Add recommendations
            result += "\nRECOMMENDATIONS:\n";
            
            if (largestPosition > 30)
                result += "• Reduce concentration in largest position\n";
            
            if (positions.Count < 5)
                result += "• Consider adding more positions for better diversification\n";
            
            if (top3Concentration > 70)
                result += "• Top 3 positions are over-concentrated\n";
            
            if (effectiveNumPositions < 3)
                result += "• Portfolio is under-diversified\n";
            
            if (result.EndsWith("RECOMMENDATIONS:\n"))
                result += "• Portfolio diversification looks good\n";
            
            return result;
        }
        catch (Exception ex)
        {
            return $"ERROR: Error analyzing diversification: {ex.Message}";
        }
    }

    [KernelFunction, Description("Set stop loss and take profit levels for a position")]
    public async Task<string> SetStopLossAndTakeProfitAsync(
        [Description("Trading symbol")] string symbol,
        [Description("Stop loss price")] double stopLossPrice,
        [Description("Take profit price")] double takeProfitPrice)
    {
        try
        {
            var positions = await _portfolioService.GetPositionsAsync();
            var position = positions.FirstOrDefault(p => p.Symbol.Equals(symbol.ToUpper(), StringComparison.OrdinalIgnoreCase));
            
            if (position == null)
            {
                return $"ERROR: No position found for {symbol}";
            }
            
            // Validate stop loss and take profit levels
            var currentPrice = position.CurrentPrice;
            var isLongPosition = position.Quantity > 0;
            
            if (isLongPosition)
            {
                if (stopLossPrice >= currentPrice)
                {
                    return "ERROR: Stop loss must be below current price for long positions";
                }
                if (takeProfitPrice <= currentPrice)
                {
                    return "ERROR: Take profit must be above current price for long positions";
                }
            }
            else
            {
                if (stopLossPrice <= currentPrice)
                {
                    return "ERROR: Stop loss must be above current price for short positions";
                }
                if (takeProfitPrice >= currentPrice)
                {
                    return "ERROR: Take profit must be below current price for short positions";
                }
            }
            
            // Calculate risk/reward ratio
            var risk = Math.Abs(currentPrice - stopLossPrice);
            var reward = Math.Abs(takeProfitPrice - currentPrice);
            var riskRewardRatio = risk > 0 ? reward / risk : 0;
            
            // Set the levels (in a real implementation, you'd set actual orders)
            position.StopLoss = stopLossPrice.ToString("F2");
            position.TakeProfit = takeProfitPrice.ToString("F2");
            
            return $"SUCCESS: Stop Loss & Take Profit Set for {symbol}:\n\n" +
                   $"Current Price: ${currentPrice:F2}\n" +
                   $"Stop Loss: ${stopLossPrice:F2}\n" +
                   $"Take Profit: ${takeProfitPrice:F2}\n" +
                   $"Risk/Reward Ratio: 1:{riskRewardRatio:F2}\n" +
                   $"Position Type: {(isLongPosition ? "Long" : "Short")}\n\n" +
                   $"Risk Management Status: " + (riskRewardRatio >= 1.5 ? "SUCCESS: Good" : "WARNING: Consider better ratio");
        }
        catch (Exception ex)
        {
            return $"ERROR: Error setting stop loss and take profit: {ex.Message}";
        }
    }
}
