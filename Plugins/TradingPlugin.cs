using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for trading operations and signal management
/// </summary>
public class TradingPlugin
{
    private readonly TradingSignalService _signalService;
    private readonly PortfolioService _portfolioService;
    private readonly RiskManagementService _riskService;

    public TradingPlugin(
        TradingSignalService signalService,
        PortfolioService portfolioService,
        RiskManagementService riskService)
    {
        _signalService = signalService;
        _portfolioService = portfolioService;
        _riskService = riskService;
    }

    [KernelFunction, Description("Generate trading signals for a specific symbol or all tracked symbols")]
    public async Task<string> GenerateTradingSignalsAsync(
        [Description("The trading symbol to analyze (e.g., BTCUSDT, AAPL). Leave empty for all symbols")] string? symbol = null)
    {
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                var signals = await _signalService.GenerateSignalsAsync();
                
                if (!signals.Any())
                {
                    return "No trading signals generated at this time.";
                }
                
                var result = $"Generated {signals.Count} Trading Signals:\n\n";
                
                foreach (var signal in signals.Take(10)) // Limit to top 10
                {
                    result += FormatSignal(signal) + "\n\n";
                }
                
                return result.TrimEnd();
            }
            else
            {
                var signal = await _signalService.GenerateSignalAsync(symbol.ToUpper());
                
                if (signal == null)
                {
                    return $"No trading signal generated for {symbol}. Check symbol validity or market conditions.";
                }
                
                return $"Trading Signal for {symbol}:\n\n{FormatSignal(signal)}";
            }
        }
        catch (Exception ex)
        {
            return $"Error generating trading signals: {ex.Message}";
        }
    }

    [KernelFunction, Description("Execute a trading signal with risk management validation")]
    public async Task<string> ExecuteTradingSignalAsync(
        [Description("The trading symbol")] string symbol,
        [Description("Signal type: BUY, SELL, STRONG_BUY, STRONG_SELL")] string signalType,
        [Description("Signal strength (0.0-1.0)")] double strength,
        [Description("Current market price")] double price,
        [Description("Reasoning for the signal")] string reasoning)
    {
        try
        {
            // Create signal object
            var signal = new TradingSignal
            {
                Symbol = symbol.ToUpper(),
                Type = Enum.Parse<SignalType>(signalType.Replace("_", ""), true),
                Strength = Math.Max(0.0, Math.Min(1.0, strength)),
                Price = price,
                Reasoning = reasoning,
                GeneratedAt = DateTime.UtcNow,
                Source = "manual_execution"
            };

            // Validate signal with risk management
            var isValid = await _riskService.ValidateSignalAsync(signal);
            if (!isValid)
            {
                return $"Signal rejected by risk management for {symbol}. Check risk parameters and market conditions.";
            }

            // Calculate position size
            var quantity = await _riskService.CalculatePositionSizeAsync(signal);
            if (quantity <= 0)
            {
                return $"Cannot calculate valid position size for {symbol}. Signal not executed.";
            }

            // Execute the signal
            var executed = await _portfolioService.ExecuteSignalAsync(signal, quantity);
            
            if (executed)
            {
                return $"Successfully executed {signalType} signal for {symbol}:\n" +
                       $"Quantity: {quantity:F6}\n" +
                       $"Price: ${price:F2}\n" +
                       $"Total Value: ${quantity * price:F2}\n" +
                       $"Reasoning: {reasoning}";
            }
            else
            {
                return $"Failed to execute signal for {symbol}. Check portfolio balance and market conditions.";
            }
        }
        catch (Exception ex)
        {
            return $"Error executing trading signal: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get current active trading signals")]
    public string GetActiveSignals()
    {
        try
        {
            var activeSignals = _signalService.GetActiveSignals();
            
            if (!activeSignals.Any())
            {
                return "No active trading signals at this time.";
            }
            
            var result = $"Active Trading Signals ({activeSignals.Count}):\n\n";
            
            foreach (var signal in activeSignals.Take(15))
            {
                result += FormatSignal(signal) + "\n\n";
            }
            
            return result.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Error retrieving active signals: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get signal history for a specific symbol")]
    public string GetSignalHistory(
        [Description("The trading symbol to get history for")] string symbol,
        [Description("Number of recent signals to return (default: 10)")] int count = 10)
    {
        try
        {
            var history = _signalService.GetSignalHistory(symbol.ToUpper());
            
            if (!history.Any())
            {
                return $"No signal history found for {symbol}.";
            }
            
            var recentSignals = history.OrderByDescending(s => s.GeneratedAt).Take(count);
            
            var result = $"Signal History for {symbol} (Last {recentSignals.Count()} signals):\n\n";
            
            foreach (var signal in recentSignals)
            {
                result += $"[{signal.GeneratedAt:yyyy-MM-dd HH:mm}] {FormatSignal(signal)}\n\n";
            }
            
            return result.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Error retrieving signal history: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze trading performance and provide recommendations")]
    public async Task<string> AnalyzeTradingPerformanceAsync()
    {
        try
        {
            var metrics = await _portfolioService.CalculateMetricsAsync();
            var activeSignals = _signalService.GetActiveSignals();
            var riskAssessment = await _riskService.AssessPortfolioRiskAsync();
            
            var result = $"Trading Performance Analysis:\n\n" +
                        $"Portfolio Metrics:\n" +
                        $"• Total Value: ${metrics.TotalValue:F2}\n" +
                        $"• Total P&L: ${metrics.TotalPnL:F2} ({metrics.TotalPnLPercent:F2}%)\n" +
                        $"• Daily Return: {metrics.DailyReturn:F2}%\n" +
                        $"• Volatility: {metrics.Volatility:F2}%\n" +
                        $"• Sharpe Ratio: {metrics.SharpeRatio:F2}\n" +
                        $"• Max Drawdown: {metrics.MaxDrawdown:F2}%\n" +
                        $"• Win Rate: {metrics.WinRate:F2}%\n\n" +
                        $"Active Signals: {activeSignals.Count}\n\n" +
                        $"Risk Assessment:\n{riskAssessment}";
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing trading performance: {ex.Message}";
        }
    }

    private string FormatSignal(TradingSignal signal)
    {
        var actionColor = signal.Type switch
        {
            SignalType.StrongBuy => "STRONG BUY",
            SignalType.Buy => "BUY",
            SignalType.Hold => "HOLD",
            SignalType.Sell => "SELL",
            SignalType.StrongSell => "STRONG SELL",
            _ => signal.Type.ToString()
        };
        
        return $"{actionColor} - {signal.Symbol}\n" +
               $"Price: ${signal.Price:F2} | Strength: {signal.Strength:F2}\n" +
               $"Source: {signal.Source} | Status: {signal.Status}\n" +
               $"Reasoning: {signal.Reasoning}";
    }
}
