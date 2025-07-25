using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

public class RiskManagementService
{
    private readonly ILogger<RiskManagementService> _logger;
    private readonly IConfiguration _configuration;
    private readonly PortfolioService _portfolioService;
    private readonly MarketDataService _marketDataService;
    private readonly RiskManagementConfig _config;

    public RiskManagementService(
        ILogger<RiskManagementService> logger,
        IConfiguration configuration,
        PortfolioService portfolioService,
        MarketDataService marketDataService)
    {
        _logger = logger;
        _configuration = configuration;
        _portfolioService = portfolioService;
        _marketDataService = marketDataService;
        
        // Load risk management configuration
        _config = new RiskManagementConfig();
        configuration.GetSection("Agent:RiskManagement").Bind(_config);
    }

    public async Task<string> AssessPortfolioRiskAsync()
    {
        try
        {
            var metrics = await _portfolioService.CalculateMetricsAsync();
            var positions = await _portfolioService.GetPositionsAsync();
            
            var riskLevel = CalculateRiskLevel(metrics, positions);
            var recommendations = GenerateRiskRecommendations(metrics, positions);
            
            _logger.LogInformation("Portfolio risk assessment completed. Risk level: {RiskLevel}", riskLevel);
            
            return $"Risk Level: {riskLevel}\nRecommendations: {string.Join("; ", recommendations)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assess portfolio risk");
            return "Risk assessment failed";
        }
    }

    public async Task<bool> ValidateSignalAsync(TradingSignal signal)
    {
        try
        {
            // Check if we're at maximum positions
            var positions = await _portfolioService.GetPositionsAsync();
            if (positions.Count >= _config.MaxPositions && !positions.Any(p => p.Symbol == signal.Symbol))
            {
                _logger.LogWarning("Signal rejected: Maximum positions ({MaxPositions}) reached", _config.MaxPositions);
                return false;
            }

            // Check position size
            var metrics = await _portfolioService.CalculateMetricsAsync();
            var proposedPositionValue = metrics.TotalValue * _config.PositionSizePercent;
            
            if (proposedPositionValue > metrics.TotalValue * 0.2) // Max 20% per position
            {
                _logger.LogWarning("Signal rejected: Position size too large for {Symbol}", signal.Symbol);
                return false;
            }

            // Check drawdown
            if (metrics.MaxDrawdown > _config.MaxDrawdown)
            {
                _logger.LogWarning("Signal rejected: Maximum drawdown exceeded ({CurrentDrawdown:P} > {MaxDrawdown:P})", 
                    metrics.MaxDrawdown, _config.MaxDrawdown);
                return false;
            }

            // Check volatility
            var marketData = await _marketDataService.GetMarketDataAsync(signal.Symbol);
            if (marketData != null)
            {
                var historicalData = await _marketDataService.GetHistoricalDataAsync(signal.Symbol, 20);
                if (historicalData != null && historicalData.Count > 1)
                {
                    var volatility = CalculateVolatility(historicalData);
                    if (volatility > _config.VolatilityTarget * 2) // Max 2x target volatility
                    {
                        _logger.LogWarning("Signal rejected: Volatility too high for {Symbol} ({Volatility:P})", 
                            signal.Symbol, volatility);
                        return false;
                    }
                }
            }

            // Validate stop loss and take profit
            if (signal.StopLoss.HasValue && signal.TakeProfit.HasValue)
            {
                var riskRewardRatio = CalculateRiskRewardRatio(signal);
                if (riskRewardRatio < 1.0) // Minimum 1:1 risk/reward
                {
                    _logger.LogWarning("Signal rejected: Poor risk/reward ratio for {Symbol} ({Ratio:F2})", 
                        signal.Symbol, riskRewardRatio);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate signal for {Symbol}", signal.Symbol);
            return false;
        }
    }

    public async Task<double> CalculatePositionSizeAsync(TradingSignal signal)
    {
        try
        {
            var metrics = await _portfolioService.CalculateMetricsAsync();
            var marketData = await _marketDataService.GetMarketDataAsync(signal.Symbol);
            
            if (marketData == null)
            {
                _logger.LogWarning("Cannot calculate position size: No market data for {Symbol}", signal.Symbol);
                return 0;
            }

            // Base position size
            var basePositionValue = metrics.TotalValue * _config.PositionSizePercent;
            
            // Adjust for signal strength
            var adjustedPositionValue = basePositionValue * signal.Strength;
            
            // Adjust for volatility
            var historicalData = await _marketDataService.GetHistoricalDataAsync(signal.Symbol, 20);
            if (historicalData != null && historicalData.Count > 1)
            {
                var volatility = CalculateVolatility(historicalData);
                var volatilityAdjustment = Math.Min(1.0, _config.VolatilityTarget / volatility);
                adjustedPositionValue *= volatilityAdjustment;
            }
            
            // Convert to quantity
            var quantity = adjustedPositionValue / marketData.Price;
            
            _logger.LogInformation("Calculated position size for {Symbol}: {Quantity:F6} (${Value:F2})", 
                signal.Symbol, quantity, adjustedPositionValue);
            
            return quantity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate position size for {Symbol}", signal.Symbol);
            return 0;
        }
    }

    private string CalculateRiskLevel(PortfolioMetrics metrics, List<Position> positions)
    {
        var riskScore = 0;
        
        // Drawdown risk
        if (metrics.MaxDrawdown > _config.MaxDrawdown * 0.8) riskScore += 3;
        else if (metrics.MaxDrawdown > _config.MaxDrawdown * 0.5) riskScore += 2;
        else if (metrics.MaxDrawdown > _config.MaxDrawdown * 0.3) riskScore += 1;
        
        // Concentration risk
        var concentrationRatio = positions.Count > 0 ? 1.0 / positions.Count : 0;
        if (concentrationRatio > 0.5) riskScore += 3;
        else if (concentrationRatio > 0.3) riskScore += 2;
        else if (concentrationRatio > 0.2) riskScore += 1;
        
        // Volatility risk
        if (metrics.Volatility > _config.VolatilityTarget * 1.5) riskScore += 2;
        else if (metrics.Volatility > _config.VolatilityTarget * 1.2) riskScore += 1;
        
        // Performance risk
        if (metrics.TotalPnLPercent < -10) riskScore += 2;
        else if (metrics.TotalPnLPercent < -5) riskScore += 1;
        
        return riskScore switch
        {
            0 => "Very Low",
            1 or 2 => "Low",
            3 or 4 => "Medium",
            5 or 6 => "High",
            _ => "Very High"
        };
    }

    private List<string> GenerateRiskRecommendations(PortfolioMetrics metrics, List<Position> positions)
    {
        var recommendations = new List<string>();
        
        if (metrics.MaxDrawdown > _config.MaxDrawdown * 0.8)
        {
            recommendations.Add("Consider reducing position sizes due to high drawdown");
        }
        
        if (positions.Count < 3)
        {
            recommendations.Add("Diversify portfolio by adding more positions");
        }
        
        if (positions.Count > _config.MaxPositions * 0.8)
        {
            recommendations.Add("Consider consolidating positions to reduce complexity");
        }
        
        if (metrics.Volatility > _config.VolatilityTarget * 1.3)
        {
            recommendations.Add("Portfolio volatility is high, consider defensive positions");
        }
        
        if (metrics.SharpeRatio < 0.5)
        {
            recommendations.Add("Poor risk-adjusted returns, review strategy");
        }
        
        var winRate = metrics.WinRate;
        if (winRate < 0.4)
        {
            recommendations.Add("Low win rate detected, review entry criteria");
        }
        
        if (recommendations.Count == 0)
        {
            recommendations.Add("Portfolio risk levels are within acceptable ranges");
        }
        
        return recommendations;
    }

    private double CalculateVolatility(List<MarketData> historicalData)
    {
        if (historicalData.Count < 2) return 0;
        
        var returns = new List<double>();
        for (int i = 1; i < historicalData.Count; i++)
        {
            var returnValue = (historicalData[i].Price - historicalData[i - 1].Price) / historicalData[i - 1].Price;
            returns.Add(returnValue);
        }
        
        if (returns.Count == 0) return 0;
        
        var meanReturn = returns.Average();
        var variance = returns.Sum(r => Math.Pow(r - meanReturn, 2)) / returns.Count;
        var volatility = Math.Sqrt(variance);
        
        // Annualize volatility (assuming 5-minute data)
        return volatility * Math.Sqrt(365 * 24 * 12); // 12 5-minute periods per hour
    }

    private double CalculateRiskRewardRatio(TradingSignal signal)
    {
        if (!signal.StopLoss.HasValue || !signal.TakeProfit.HasValue)
            return 0;
        
        var currentPrice = signal.Price;
        var stopLoss = signal.StopLoss.Value;
        var takeProfit = signal.TakeProfit.Value;
        
        double risk, reward;
        
        if (signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy)
        {
            risk = Math.Abs(currentPrice - stopLoss);
            reward = Math.Abs(takeProfit - currentPrice);
        }
        else
        {
            risk = Math.Abs(stopLoss - currentPrice);
            reward = Math.Abs(currentPrice - takeProfit);
        }
        
        return risk > 0 ? reward / risk : 0;
    }

    public RiskManagementConfig GetConfig() => _config;
}
