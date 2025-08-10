using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Services;

/// <summary>
/// Consolidated Trading Strategy Library Service
/// Integrates all trading strategies from crypto_research/ folder
/// </summary>
public class TradingStrategyLibraryService
{
    private readonly Kernel _kernel;
    private readonly Dictionary<string, ITradingStrategy> _strategies = new();

    public TradingStrategyLibraryService(Kernel kernel)
    {
        _kernel = kernel;
        InitializeStrategies();
    }

    [KernelFunction]
    [Description("Execute a specific trading strategy with given parameters")]
    public async Task<string> ExecuteStrategyAsync(
        [Description("Strategy name (SMA, EMA, Bollinger, RSI, MACD, MeanReversion, Momentum, Pairs, Scalping, Swing)")] string strategyName,
        [Description("Trading symbol")] string symbol,
        [Description("Strategy parameters in JSON format")] string parameters = "{}",
        [Description("Backtesting period in days")] int backtestDays = 30)
    {
        try
        {
            if (!_strategies.ContainsKey(strategyName.ToUpper()))
            {
                return $"Strategy '{strategyName}' not found. Available strategies: {string.Join(", ", _strategies.Keys)}";
            }

            var strategy = _strategies[strategyName.ToUpper()];
            var strategyParams = JsonSerializer.Deserialize<Dictionary<string, object>>(parameters) ?? new();
            
            var result = await strategy.ExecuteAsync(symbol, strategyParams, backtestDays);
            
            return JsonSerializer.Serialize(new
            {
                Strategy = strategyName,
                Symbol = symbol,
                Parameters = strategyParams,
                BacktestPeriod = backtestDays,
                Results = result,
                Timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error executing strategy {strategyName}: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Compare multiple trading strategies performance")]
    public async Task<string> CompareStrategiesAsync(
        [Description("Comma-separated strategy names")] string strategies,
        [Description("Trading symbol")] string symbol,
        [Description("Comparison period in days")] int comparisonDays = 90)
    {
        try
        {
            var strategyList = strategies.Split(',').Select(s => s.Trim().ToUpper()).ToList();
            var comparisons = new List<object>();

            foreach (var strategyName in strategyList)
            {
                if (_strategies.ContainsKey(strategyName))
                {
                    var strategy = _strategies[strategyName];
                    var result = await strategy.ExecuteAsync(symbol, new Dictionary<string, object>(), comparisonDays);
                    
                    comparisons.Add(new
                    {
                        Strategy = strategyName,
                        Performance = result,
                        RiskAdjustedReturn = CalculateRiskAdjustedReturn(result),
                        MaxDrawdown = CalculateMaxDrawdown(result),
                        SharpeRatio = CalculateSharpeRatio(result)
                    });
                }
            }

            var ranking = comparisons.OrderByDescending(c => 
                ((double)c.GetType().GetProperty("RiskAdjustedReturn")!.GetValue(c)!)).ToList();

            var comparison = await _kernel.InvokePromptAsync($@"
                Analyze this strategy comparison for {symbol}:
                {JsonSerializer.Serialize(ranking, new JsonSerializerOptions { WriteIndented = true })}
                
                Provide:
                1. Performance ranking with reasoning
                2. Risk-return analysis
                3. Market condition suitability
                4. Recommendation for current market
                5. Portfolio allocation suggestions
                
                Format output as PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags.
            ");

            return comparison.ToString();
        }
        catch (Exception ex)
        {
            return $"Error comparing strategies: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Generate adaptive strategy signals based on market conditions")]
    public async Task<string> GenerateAdaptiveSignalsAsync(
        [Description("Trading symbol")] string symbol,
        [Description("Market regime (trending, ranging, volatile, calm)")] string marketRegime = "auto",
        [Description("Signal strength threshold (0.0-1.0)")] double threshold = 0.6)
    {
        try
        {
            var regime = marketRegime == "auto" ? await DetectMarketRegimeAsync(symbol) : marketRegime;
            var signals = new List<object>();

            // Select strategies based on market regime
            var suitableStrategies = GetStrategiesForRegime(regime);

            foreach (var strategyName in suitableStrategies)
            {
                var strategy = _strategies[strategyName];
                var signal = await strategy.GenerateSignalAsync(symbol, threshold);
                
                if (signal.Strength >= threshold)
                {
                    signals.Add(signal);
                }
            }

            var aggregatedSignal = await AggregateSignalsAsync(signals, regime);

            return JsonSerializer.Serialize(new
            {
                Symbol = symbol,
                MarketRegime = regime,
                IndividualSignals = signals,
                AggregatedSignal = aggregatedSignal,
                Confidence = CalculateSignalConfidence(signals),
                Timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error generating adaptive signals for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Run portfolio optimization across multiple strategies")]
    public async Task<string> OptimizePortfolioStrategiesAsync(
        [Description("Comma-separated list of symbols")] string symbols,
        [Description("Risk tolerance (conservative, moderate, aggressive)")] string riskTolerance = "moderate",
        [Description("Optimization period in days")] int optimizationDays = 60)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim().ToUpper()).ToList();
            var portfolioResults = new List<object>();

            foreach (var symbol in symbolList)
            {
                var bestStrategy = await FindOptimalStrategyAsync(symbol, riskTolerance, optimizationDays);
                portfolioResults.Add(new
                {
                    Symbol = symbol,
                    OptimalStrategy = bestStrategy.Name,
                    ExpectedReturn = bestStrategy.ExpectedReturn,
                    Risk = bestStrategy.Risk,
                    AllocationWeight = CalculateAllocationWeight(bestStrategy, riskTolerance)
                });
            }

            var optimization = await _kernel.InvokePromptAsync($@"
                Optimize this multi-strategy portfolio:
                {JsonSerializer.Serialize(portfolioResults, new JsonSerializerOptions { WriteIndented = true })}
                
                Risk Tolerance: {riskTolerance}
                
                Provide:
                1. Optimal allocation weights
                2. Diversification analysis
                3. Risk-return optimization
                4. Rebalancing schedule
                5. Performance projections
                
                Format output as PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags.
            ");

            return optimization.ToString();
        }
        catch (Exception ex)
        {
            return $"Error optimizing portfolio strategies: {ex.Message}";
        }
    }

    private void InitializeStrategies()
    {
        // Initialize all trading strategies from the crypto_research folder
        _strategies["SMA"] = new SMAStrategy();
        _strategies["EMA"] = new EMAStrategy();
        _strategies["BOLLINGER"] = new BollingerBandsStrategy();
        _strategies["RSI"] = new RSIStrategy();
        _strategies["MACD"] = new MACDStrategy();
        _strategies["MEANREVERSION"] = new MeanReversionStrategy();
        _strategies["MOMENTUM"] = new MomentumStrategy();
        _strategies["PAIRS"] = new PairsTradingStrategy();
        _strategies["SCALPING"] = new ScalpingStrategy();
        _strategies["SWING"] = new SwingHighStrategy();
        _strategies["LONGSHORT"] = new LongShortStrategy();
        _strategies["PULLBACK"] = new PullbackStrategy();
        _strategies["REVERSE"] = new ReverseStrategy();
    }

    private async Task<string> DetectMarketRegimeAsync(string symbol)
    {
        // Market regime detection logic
        var prompt = $@"
            Analyze current market conditions for {symbol} and determine the market regime:
            - Trending (strong directional movement)
            - Ranging (sideways movement)
            - Volatile (high volatility)
            - Calm (low volatility)
            
            Return only the regime name in PLAIN TEXT - no markdown formatting.
        ";

        var regime = await _kernel.InvokePromptAsync(prompt);
        return regime.ToString().Trim().ToLower();
    }

    private List<string> GetStrategiesForRegime(string regime)
    {
        return regime.ToLower() switch
        {
            "trending" => new List<string> { "MOMENTUM", "EMA", "MACD" },
            "ranging" => new List<string> { "MEANREVERSION", "BOLLINGER", "RSI" },
            "volatile" => new List<string> { "SCALPING", "REVERSE", "PAIRS" },
            "calm" => new List<string> { "SMA", "SWING", "LONGSHORT" },
            _ => new List<string> { "SMA", "EMA", "RSI" }
        };
    }

    private Task<object> AggregateSignalsAsync(List<object> signals, string regime)
    {
        // Signal aggregation logic based on regime
        var result = new
        {
            Direction = "BUY", // Placeholder
            Strength = 0.75,
            Confidence = 0.80,
            Regime = regime
        };
        
        return Task.FromResult<object>(result);
    }

    private double CalculateSignalConfidence(List<object> signals) => 0.75; // Placeholder
    private double CalculateRiskAdjustedReturn(object result) => 0.15; // Placeholder
    private double CalculateMaxDrawdown(object result) => -0.05; // Placeholder
    private double CalculateSharpeRatio(object result) => 1.5; // Placeholder
    private double CalculateAllocationWeight(StrategyResult result, string riskTolerance) => 0.20; // Placeholder

    private Task<StrategyResult> FindOptimalStrategyAsync(string symbol, string riskTolerance, int days)
    {
        var result = new StrategyResult
        {
            Name = "MOMENTUM",
            ExpectedReturn = 0.12,
            Risk = 0.18
        };
        
        return Task.FromResult(result);
    }

    public class StrategyResult
    {
        public string Name { get; set; } = string.Empty;
        public double ExpectedReturn { get; set; }
        public double Risk { get; set; }
    }
}

// Strategy interface and implementations
public interface ITradingStrategy
{
    Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days);
    Task<SignalResult> GenerateSignalAsync(string symbol, double threshold);
}

public class SignalResult
{
    public string Direction { get; set; } = string.Empty; // BUY, SELL, HOLD
    public double Strength { get; set; } // 0.0 to 1.0
    public double Confidence { get; set; } // 0.0 to 1.0
    public string Strategy { get; set; } = string.Empty;
}

// Strategy implementations (simplified)
public class SMAStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.08, Volatility = 0.15, MaxDrawdown = -0.03 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "BUY", Strength = 0.7, Confidence = 0.8, Strategy = "SMA" };
        return Task.FromResult(result);
    }
}

public class EMAStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.10, Volatility = 0.18, MaxDrawdown = -0.04 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "BUY", Strength = 0.75, Confidence = 0.82, Strategy = "EMA" };
        return Task.FromResult(result);
    }
}

public class BollingerBandsStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.09, Volatility = 0.16, MaxDrawdown = -0.035 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "SELL", Strength = 0.65, Confidence = 0.78, Strategy = "BOLLINGER" };
        return Task.FromResult(result);
    }
}

public class RSIStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.07, Volatility = 0.14, MaxDrawdown = -0.025 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "HOLD", Strength = 0.55, Confidence = 0.75, Strategy = "RSI" };
        return Task.FromResult(result);
    }
}

public class MACDStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.11, Volatility = 0.19, MaxDrawdown = -0.045 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "BUY", Strength = 0.8, Confidence = 0.85, Strategy = "MACD" };
        return Task.FromResult(result);
    }
}

public class MeanReversionStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.06, Volatility = 0.12, MaxDrawdown = -0.02 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "BUY", Strength = 0.68, Confidence = 0.77, Strategy = "MEANREVERSION" };
        return Task.FromResult(result);
    }
}

public class MomentumStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.13, Volatility = 0.22, MaxDrawdown = -0.06 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "BUY", Strength = 0.85, Confidence = 0.88, Strategy = "MOMENTUM" };
        return Task.FromResult(result);
    }
}

public class PairsTradingStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.09, Volatility = 0.13, MaxDrawdown = -0.025 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "HOLD", Strength = 0.62, Confidence = 0.79, Strategy = "PAIRS" };
        return Task.FromResult(result);
    }
}

public class ScalpingStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.15, Volatility = 0.25, MaxDrawdown = -0.08 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "BUY", Strength = 0.72, Confidence = 0.73, Strategy = "SCALPING" };
        return Task.FromResult(result);
    }
}

public class SwingHighStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.10, Volatility = 0.17, MaxDrawdown = -0.04 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "BUY", Strength = 0.78, Confidence = 0.81, Strategy = "SWING" };
        return Task.FromResult(result);
    }
}

public class LongShortStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.08, Volatility = 0.11, MaxDrawdown = -0.02 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "HOLD", Strength = 0.58, Confidence = 0.76, Strategy = "LONGSHORT" };
        return Task.FromResult(result);
    }
}

public class PullbackStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.07, Volatility = 0.14, MaxDrawdown = -0.03 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "BUY", Strength = 0.66, Confidence = 0.74, Strategy = "PULLBACK" };
        return Task.FromResult(result);
    }
}

public class ReverseStrategy : ITradingStrategy
{
    public Task<object> ExecuteAsync(string symbol, Dictionary<string, object> parameters, int days)
    {
        var result = new { Return = 0.12, Volatility = 0.20, MaxDrawdown = -0.05 };
        return Task.FromResult<object>(result);
    }

    public Task<SignalResult> GenerateSignalAsync(string symbol, double threshold)
    {
        var result = new SignalResult { Direction = "SELL", Strength = 0.73, Confidence = 0.79, Strategy = "REVERSE" };
        return Task.FromResult(result);
    }
}
