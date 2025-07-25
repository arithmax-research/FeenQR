using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Collections.Concurrent;

namespace QuantResearchAgent.Services;

public class PortfolioService
{
    private readonly ILogger<PortfolioService> _logger;
    private readonly MarketDataService _marketDataService;
    private readonly ConcurrentDictionary<string, Position> _positions = new();
    private readonly List<PortfolioMetrics> _metricsHistory = new();
    private double _cashBalance = 100000; // Starting with $100k
    private readonly List<double> _portfolioValues = new();

    public PortfolioService(
        ILogger<PortfolioService> logger,
        MarketDataService marketDataService)
    {
        _logger = logger;
        _marketDataService = marketDataService;
    }

    public async Task<List<Position>> GetPositionsAsync()
    {
        var positions = _positions.Values.ToList();
        
        // Update current prices
        foreach (var position in positions)
        {
            var marketData = await _marketDataService.GetMarketDataAsync(position.Symbol);
            if (marketData != null)
            {
                position.CurrentPrice = marketData.Price;
            }
        }
        
        return positions;
    }

    public async Task<PortfolioMetrics> CalculateMetricsAsync()
    {
        try
        {
            var positions = await GetPositionsAsync();
            var totalValue = _cashBalance;
            var totalPnL = 0.0;
            
            foreach (var position in positions)
            {
                var positionValue = position.Quantity * position.CurrentPrice;
                totalValue += positionValue;
                totalPnL += position.UnrealizedPnL;
            }
            
            _portfolioValues.Add(totalValue);
            
            var metrics = new PortfolioMetrics
            {
                TotalValue = totalValue,
                TotalPnL = totalPnL,
                TotalPnLPercent = totalValue > 0 ? (totalPnL / (totalValue - totalPnL)) * 100 : 0,
                DailyReturn = CalculateDailyReturn(),
                Volatility = CalculateVolatility(),
                SharpeRatio = CalculateSharpeRatio(),
                MaxDrawdown = CalculateMaxDrawdown(),
                WinningTrades = CountWinningTrades(),
                LosingTrades = CountLosingTrades(),
                CalculatedAt = DateTime.UtcNow
            };
            
            _metricsHistory.Add(metrics);
            
            // Keep only last 1000 metrics
            if (_metricsHistory.Count > 1000)
            {
                _metricsHistory.RemoveAt(0);
            }
            
            _logger.LogInformation("Portfolio metrics calculated: Value=${TotalValue:F2}, P&L={TotalPnL:F2} ({TotalPnLPercent:F2}%)", 
                metrics.TotalValue, metrics.TotalPnL, metrics.TotalPnLPercent);
            
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate portfolio metrics");
            throw;
        }
    }

    public async Task<bool> ExecuteSignalAsync(TradingSignal signal, double quantity)
    {
        try
        {
            var marketData = await _marketDataService.GetMarketDataAsync(signal.Symbol);
            if (marketData == null)
            {
                _logger.LogError("Cannot execute signal: No market data for {Symbol}", signal.Symbol);
                return false;
            }

            var currentPrice = marketData.Price;
            var totalCost = quantity * currentPrice;

            if (signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy)
            {
                // Check if we have enough cash
                if (totalCost > _cashBalance)
                {
                    _logger.LogWarning("Insufficient cash for buy order: Required=${Required:F2}, Available=${Available:F2}", 
                        totalCost, _cashBalance);
                    return false;
                }

                // Execute buy order
                if (_positions.ContainsKey(signal.Symbol))
                {
                    // Add to existing position
                    var existingPosition = _positions[signal.Symbol];
                    var newTotalQuantity = existingPosition.Quantity + quantity;
                    var newAveragePrice = ((existingPosition.Quantity * existingPosition.AveragePrice) + (quantity * currentPrice)) / newTotalQuantity;
                    
                    existingPosition.Quantity = newTotalQuantity;
                    existingPosition.AveragePrice = newAveragePrice;
                    existingPosition.CurrentPrice = currentPrice;
                }
                else
                {
                    // Create new position
                    _positions[signal.Symbol] = new Position
                    {
                        Symbol = signal.Symbol,
                        Quantity = quantity,
                        AveragePrice = currentPrice,
                        CurrentPrice = currentPrice,
                        OpenedAt = DateTime.UtcNow
                    };
                }

                _cashBalance -= totalCost;
                signal.Status = SignalStatus.Executed;
                
                _logger.LogInformation("Executed BUY signal for {Symbol}: {Quantity:F6} @ ${Price:F2}", 
                    signal.Symbol, quantity, currentPrice);
            }
            else if (signal.Type == SignalType.Sell || signal.Type == SignalType.StrongSell)
            {
                // Check if we have the position
                if (!_positions.ContainsKey(signal.Symbol))
                {
                    _logger.LogWarning("Cannot sell {Symbol}: No position found", signal.Symbol);
                    return false;
                }

                var position = _positions[signal.Symbol];
                if (position.Quantity < quantity)
                {
                    _logger.LogWarning("Cannot sell {Symbol}: Insufficient quantity. Have {Have:F6}, trying to sell {Sell:F6}", 
                        signal.Symbol, position.Quantity, quantity);
                    quantity = position.Quantity; // Sell all available
                }

                // Execute sell order
                var sellValue = quantity * currentPrice;
                _cashBalance += sellValue;
                
                position.Quantity -= quantity;
                position.CurrentPrice = currentPrice;

                // Remove position if quantity is zero
                if (position.Quantity <= 0.000001) // Account for floating point precision
                {
                    _positions.TryRemove(signal.Symbol, out _);
                }

                signal.Status = SignalStatus.Executed;
                
                _logger.LogInformation("Executed SELL signal for {Symbol}: {Quantity:F6} @ ${Price:F2}", 
                    signal.Symbol, quantity, currentPrice);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute signal for {Symbol}", signal.Symbol);
            return false;
        }
    }

    public double GetCashBalance() => _cashBalance;

    public void SetCashBalance(double balance)
    {
        _cashBalance = balance;
        _logger.LogInformation("Cash balance updated to ${Balance:F2}", balance);
    }

    private double CalculateDailyReturn()
    {
        if (_portfolioValues.Count < 2) return 0;
        
        var currentValue = _portfolioValues.Last();
        var previousValue = _portfolioValues[^2];
        
        return previousValue > 0 ? ((currentValue - previousValue) / previousValue) * 100 : 0;
    }

    private double CalculateVolatility()
    {
        if (_portfolioValues.Count < 10) return 0;
        
        var returns = new List<double>();
        for (int i = 1; i < _portfolioValues.Count; i++)
        {
            if (_portfolioValues[i - 1] > 0)
            {
                var returnValue = (_portfolioValues[i] - _portfolioValues[i - 1]) / _portfolioValues[i - 1];
                returns.Add(returnValue);
            }
        }
        
        if (returns.Count == 0) return 0;
        
        var meanReturn = returns.Average();
        var variance = returns.Sum(r => Math.Pow(r - meanReturn, 2)) / returns.Count;
        var volatility = Math.Sqrt(variance);
        
        // Annualize volatility
        return volatility * Math.Sqrt(252); // 252 trading days per year
    }

    private double CalculateSharpeRatio()
    {
        var volatility = CalculateVolatility();
        if (volatility == 0) return 0;
        
        var annualizedReturn = CalculateAnnualizedReturn();
        var riskFreeRate = 0.02; // Assume 2% risk-free rate
        
        return (annualizedReturn - riskFreeRate) / volatility;
    }

    private double CalculateAnnualizedReturn()
    {
        if (_portfolioValues.Count < 2) return 0;
        
        var initialValue = _portfolioValues.First();
        var currentValue = _portfolioValues.Last();
        
        if (initialValue <= 0) return 0;
        
        var totalReturn = (currentValue - initialValue) / initialValue;
        var periods = _portfolioValues.Count;
        
        // Assuming daily data, annualize
        return Math.Pow(1 + totalReturn, 252.0 / periods) - 1;
    }

    private double CalculateMaxDrawdown()
    {
        if (_portfolioValues.Count < 2) return 0;
        
        var peak = _portfolioValues[0];
        var maxDrawdown = 0.0;
        
        foreach (var value in _portfolioValues)
        {
            if (value > peak)
            {
                peak = value;
            }
            else
            {
                var drawdown = (peak - value) / peak;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
        }
        
        return maxDrawdown;
    }

    private int CountWinningTrades()
    {
        // This would need to be implemented based on closed positions
        // For now, return a placeholder
        return _positions.Values.Count(p => p.UnrealizedPnL > 0);
    }

    private int CountLosingTrades()
    {
        // This would need to be implemented based on closed positions
        // For now, return a placeholder
        return _positions.Values.Count(p => p.UnrealizedPnL < 0);
    }

    public List<PortfolioMetrics> GetMetricsHistory() => _metricsHistory.ToList();
}
