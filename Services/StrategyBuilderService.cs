using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MathNet.Numerics.Statistics;

namespace QuantResearchAgent.Services;

/// <summary>
/// Interactive strategy builder service for creating and testing trading strategies
/// </summary>
public class StrategyBuilderService
{
    private readonly ILogger<StrategyBuilderService> _logger;

    public StrategyBuilderService(ILogger<StrategyBuilderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Strategy component types available for building
    /// </summary>
    public enum StrategyComponentType
    {
        EntrySignal,
        ExitSignal,
        RiskManagement,
        PositionSizing,
        Filter
    }

    /// <summary>
    /// Available technical indicators for strategy building
    /// </summary>
    public enum TechnicalIndicator
    {
        SMA,
        EMA,
        RSI,
        MACD,
        BollingerBands,
        Stochastic,
        WilliamsR,
        CCI,
        ADX,
        ATR
    }

    /// <summary>
    /// Strategy component definition
    /// </summary>
    public class StrategyComponent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public StrategyComponentType Type { get; set; }
        public TechnicalIndicator Indicator { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> Conditions { get; set; } = new();
    }

    /// <summary>
    /// Complete trading strategy definition
    /// </summary>
    public class TradingStrategy
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<StrategyComponent> Components { get; set; } = new();
        public Dictionary<string, double> Parameters { get; set; } = new();
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Strategy backtest results
    /// </summary>
    public class StrategyBacktestResult
    {
        public double TotalReturn { get; set; }
        public double AnnualizedReturn { get; set; }
        public double Volatility { get; set; }
        public double SharpeRatio { get; set; }
        public double MaxDrawdown { get; set; }
        public double WinRate { get; set; }
        public int TotalTrades { get; set; }
        public List<double> Returns { get; set; } = new();
        public List<double> CumulativeReturns { get; set; } = new();
        public List<double> Drawdowns { get; set; } = new();
    }

    /// <summary>
    /// Parameter optimization result
    /// </summary>
    public class ParameterOptimizationResult
    {
        public Dictionary<string, double> OptimalParameters { get; set; } = new();
        public double BestFitness { get; set; }
        public List<Dictionary<string, double>> ParameterCombinations { get; set; } = new();
        public List<double> FitnessValues { get; set; } = new();
    }

    /// <summary>
    /// Create a new trading strategy
    /// </summary>
    public TradingStrategy CreateStrategy(string name, string description)
    {
        _logger.LogInformation("Creating new trading strategy: {Name}", name);

        return new TradingStrategy
        {
            Name = name,
            Description = description
        };
    }

    /// <summary>
    /// Add a component to a strategy
    /// </summary>
    public void AddComponent(TradingStrategy strategy, StrategyComponent component)
    {
        _logger.LogInformation("Adding component {ComponentName} to strategy {StrategyName}",
            component.Name, strategy.Name);

        strategy.Components.Add(component);
        strategy.Modified = DateTime.UtcNow;
    }

    /// <summary>
    /// Remove a component from a strategy
    /// </summary>
    public void RemoveComponent(TradingStrategy strategy, string componentId)
    {
        var component = strategy.Components.FirstOrDefault(c => c.Id == componentId);
        if (component != null)
        {
            _logger.LogInformation("Removing component {ComponentName} from strategy {StrategyName}",
                component.Name, strategy.Name);

            strategy.Components.Remove(component);
            strategy.Modified = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Update component parameters
    /// </summary>
    public void UpdateComponentParameters(TradingStrategy strategy, string componentId,
        Dictionary<string, object> parameters)
    {
        var component = strategy.Components.FirstOrDefault(c => c.Id == componentId);
        if (component != null)
        {
            foreach (var param in parameters)
            {
                component.Parameters[param.Key] = param.Value;
            }
            strategy.Modified = DateTime.UtcNow;

            _logger.LogInformation("Updated parameters for component {ComponentName}",
                component.Name);
        }
    }

    /// <summary>
    /// Validate strategy logic
    /// </summary>
    public List<string> ValidateStrategy(TradingStrategy strategy)
    {
        var errors = new List<string>();

        // Check for entry signals
        if (!strategy.Components.Any(c => c.Type == StrategyComponentType.EntrySignal))
        {
            errors.Add("Strategy must have at least one entry signal component");
        }

        // Check for exit signals
        if (!strategy.Components.Any(c => c.Type == StrategyComponentType.ExitSignal))
        {
            errors.Add("Strategy must have at least one exit signal component");
        }

        // Check for conflicting conditions
        var entrySignals = strategy.Components.Where(c => c.Type == StrategyComponentType.EntrySignal).ToList();
        var exitSignals = strategy.Components.Where(c => c.Type == StrategyComponentType.ExitSignal).ToList();

        // Validate parameter ranges
        foreach (var component in strategy.Components)
        {
            switch (component.Indicator)
            {
                case TechnicalIndicator.RSI:
                    if (component.Parameters.ContainsKey("period") &&
                        (int)component.Parameters["period"] < 2)
                    {
                        errors.Add($"RSI period must be >= 2 for component {component.Name}");
                    }
                    break;
                case TechnicalIndicator.SMA:
                case TechnicalIndicator.EMA:
                    if (component.Parameters.ContainsKey("period") &&
                        (int)component.Parameters["period"] < 1)
                    {
                        errors.Add($"Moving average period must be >= 1 for component {component.Name}");
                    }
                    break;
            }
        }

        return errors;
    }

    /// <summary>
    /// Generate strategy code from components
    /// </summary>
    public string GenerateStrategyCode(TradingStrategy strategy)
    {
        var code = new System.Text.StringBuilder();

        code.AppendLine($"// Generated strategy: {strategy.Name}");
        code.AppendLine($"// Description: {strategy.Description}");
        code.AppendLine($"// Generated on: {DateTime.UtcNow}");
        code.AppendLine();
        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine("using System.Linq;");
        code.AppendLine();
        code.AppendLine($"public class {SanitizeClassName(strategy.Name)}Strategy");
        code.AppendLine("{");
        code.AppendLine("    // Strategy parameters");
        foreach (var param in strategy.Parameters)
        {
            code.AppendLine($"    public double {SanitizeParameterName(param.Key)} {{ get; set; }} = {param.Value};");
        }
        code.AppendLine();
        code.AppendLine("    public bool ShouldEnter(Dictionary<string, double> indicators)");
        code.AppendLine("    {");

        // Generate entry conditions
        var entryComponents = strategy.Components.Where(c => c.Type == StrategyComponentType.EntrySignal).ToList();
        if (entryComponents.Any())
        {
            code.AppendLine("        // Entry conditions");
            foreach (var component in entryComponents)
            {
                code.AppendLine($"        // {component.Name}");
                GenerateComponentCode(code, component, "entry");
            }
        }

        code.AppendLine("        return false; // Placeholder - implement logic");
        code.AppendLine("    }");
        code.AppendLine();
        code.AppendLine("    public bool ShouldExit(Dictionary<string, double> indicators)");
        code.AppendLine("    {");

        // Generate exit conditions
        var exitComponents = strategy.Components.Where(c => c.Type == StrategyComponentType.ExitSignal).ToList();
        if (exitComponents.Any())
        {
            code.AppendLine("        // Exit conditions");
            foreach (var component in exitComponents)
            {
                code.AppendLine($"        // {component.Name}");
                GenerateComponentCode(code, component, "exit");
            }
        }

        code.AppendLine("        return false; // Placeholder - implement logic");
        code.AppendLine("    }");
        code.AppendLine("}");

        return code.ToString();
    }

    /// <summary>
    /// Optimize strategy parameters using grid search
    /// </summary>
    public async Task<ParameterOptimizationResult> OptimizeParameters(
        TradingStrategy strategy,
        Dictionary<string, (double Min, double Max, double Step)> parameterRanges,
        List<double> historicalReturns,
        int maxIterations = 100)
    {
        _logger.LogInformation("Starting parameter optimization for strategy {StrategyName}",
            strategy.Name);

        var result = new ParameterOptimizationResult();
        var bestFitness = double.MinValue;

        // Generate parameter combinations
        var parameterCombinations = GenerateParameterCombinations(parameterRanges, maxIterations);

        foreach (var parameters in parameterCombinations)
        {
            // Update strategy parameters
            foreach (var param in parameters)
            {
                strategy.Parameters[param.Key] = param.Value;
            }

            // Evaluate strategy fitness (simplified - would need actual backtesting)
            var fitness = EvaluateStrategyFitness(strategy, historicalReturns);

            result.ParameterCombinations.Add(new Dictionary<string, double>(parameters));
            result.FitnessValues.Add(fitness);

            if (fitness > bestFitness)
            {
                bestFitness = fitness;
                result.OptimalParameters = new Dictionary<string, double>(parameters);
                result.BestFitness = fitness;
            }
        }

        _logger.LogInformation("Parameter optimization completed. Best fitness: {BestFitness}",
            bestFitness);

        return result;
    }

    /// <summary>
    /// Backtest strategy against historical data
    /// </summary>
    public async Task<StrategyBacktestResult> BacktestStrategy(
        TradingStrategy strategy,
        List<double> priceData,
        List<DateTime> dates,
        double initialCapital = 100000)
    {
        _logger.LogInformation("Backtesting strategy {StrategyName} with {DataPoints} data points",
            strategy.Name, priceData.Count);

        var result = new StrategyBacktestResult();
        var capital = initialCapital;
        var position = 0.0;
        var trades = 0;

        // Simplified backtest logic - would need full implementation
        for (int i = 20; i < priceData.Count; i++) // Start after warmup period
        {
            var currentPrice = priceData[i];

            // Simulate strategy signals (simplified)
            var shouldEnter = SimulateEntrySignal(strategy, priceData, i);
            var shouldExit = SimulateExitSignal(strategy, priceData, i);

            if (shouldEnter && position == 0)
            {
                // Enter position
                position = capital / currentPrice;
                capital = 0;
                trades++;
            }
            else if (shouldExit && position > 0)
            {
                // Exit position
                capital = position * currentPrice;
                position = 0;
                trades++;
            }
        }

        // Calculate final portfolio value
        var finalValue = capital + (position * priceData.Last());
        result.TotalReturn = (finalValue - initialCapital) / initialCapital;

        // Calculate metrics
        result.TotalTrades = trades;
        result.AnnualizedReturn = CalculateAnnualizedReturn(result.TotalReturn, dates.Count / 252.0);
        result.Volatility = CalculateVolatility(priceData);
        result.SharpeRatio = result.AnnualizedReturn / result.Volatility;
        result.MaxDrawdown = CalculateMaxDrawdown(priceData);

        _logger.LogInformation("Backtest completed. Total return: {TotalReturn:F4}, Sharpe: {Sharpe:F4}",
            result.TotalReturn, result.SharpeRatio);

        return result;
    }

    /// <summary>
    /// Get available strategy templates
    /// </summary>
    public List<TradingStrategy> GetStrategyTemplates()
    {
        return new List<TradingStrategy>
        {
            CreateSMACrossoverStrategy(),
            CreateRSIMomentumStrategy(),
            CreateBollingerBreakoutStrategy(),
            CreateMACDTrendStrategy()
        };
    }

    private TradingStrategy CreateSMACrossoverStrategy()
    {
        var strategy = CreateStrategy("SMA Crossover", "Simple moving average crossover strategy");

        var fastSMA = new StrategyComponent
        {
            Name = "Fast SMA",
            Type = StrategyComponentType.EntrySignal,
            Indicator = TechnicalIndicator.SMA,
            Parameters = new Dictionary<string, object> { { "period", 20 } }
        };

        var slowSMA = new StrategyComponent
        {
            Name = "Slow SMA",
            Type = StrategyComponentType.EntrySignal,
            Indicator = TechnicalIndicator.SMA,
            Parameters = new Dictionary<string, object> { { "period", 50 } }
        };

        strategy.Components.AddRange(new[] { fastSMA, slowSMA });
        strategy.Parameters["fastPeriod"] = 20;
        strategy.Parameters["slowPeriod"] = 50;

        return strategy;
    }

    private TradingStrategy CreateRSIMomentumStrategy()
    {
        var strategy = CreateStrategy("RSI Momentum", "RSI-based momentum strategy");

        var rsiEntry = new StrategyComponent
        {
            Name = "RSI Oversold",
            Type = StrategyComponentType.EntrySignal,
            Indicator = TechnicalIndicator.RSI,
            Parameters = new Dictionary<string, object>
            {
                { "period", 14 },
                { "overbought", 70 },
                { "oversold", 30 }
            }
        };

        var rsiExit = new StrategyComponent
        {
            Name = "RSI Overbought",
            Type = StrategyComponentType.ExitSignal,
            Indicator = TechnicalIndicator.RSI,
            Parameters = new Dictionary<string, object>
            {
                { "period", 14 },
                { "overbought", 70 },
                { "oversold", 30 }
            }
        };

        strategy.Components.AddRange(new[] { rsiEntry, rsiExit });
        strategy.Parameters["rsiPeriod"] = 14;
        strategy.Parameters["overboughtLevel"] = 70;
        strategy.Parameters["oversoldLevel"] = 30;

        return strategy;
    }

    private TradingStrategy CreateBollingerBreakoutStrategy()
    {
        var strategy = CreateStrategy("Bollinger Breakout", "Bollinger Bands breakout strategy");

        var bbEntry = new StrategyComponent
        {
            Name = "Upper Band Breakout",
            Type = StrategyComponentType.EntrySignal,
            Indicator = TechnicalIndicator.BollingerBands,
            Parameters = new Dictionary<string, object>
            {
                { "period", 20 },
                { "stdDev", 2.0 }
            }
        };

        var bbExit = new StrategyComponent
        {
            Name = "Lower Band Reversal",
            Type = StrategyComponentType.ExitSignal,
            Indicator = TechnicalIndicator.BollingerBands,
            Parameters = new Dictionary<string, object>
            {
                { "period", 20 },
                { "stdDev", 2.0 }
            }
        };

        strategy.Components.AddRange(new[] { bbEntry, bbExit });
        strategy.Parameters["bbPeriod"] = 20;
        strategy.Parameters["bbStdDev"] = 2.0;

        return strategy;
    }

    private TradingStrategy CreateMACDTrendStrategy()
    {
        var strategy = CreateStrategy("MACD Trend", "MACD trend-following strategy");

        var macdEntry = new StrategyComponent
        {
            Name = "MACD Bullish Crossover",
            Type = StrategyComponentType.EntrySignal,
            Indicator = TechnicalIndicator.MACD,
            Parameters = new Dictionary<string, object>
            {
                { "fastPeriod", 12 },
                { "slowPeriod", 26 },
                { "signalPeriod", 9 }
            }
        };

        var macdExit = new StrategyComponent
        {
            Name = "MACD Bearish Crossover",
            Type = StrategyComponentType.ExitSignal,
            Indicator = TechnicalIndicator.MACD,
            Parameters = new Dictionary<string, object>
            {
                { "fastPeriod", 12 },
                { "slowPeriod", 26 },
                { "signalPeriod", 9 }
            }
        };

        strategy.Components.AddRange(new[] { macdEntry, macdExit });
        strategy.Parameters["macdFast"] = 12;
        strategy.Parameters["macdSlow"] = 26;
        strategy.Parameters["macdSignal"] = 9;

        return strategy;
    }

    private void GenerateComponentCode(System.Text.StringBuilder code, StrategyComponent component, string context)
    {
        code.AppendLine($"        // Component: {component.Name} ({component.Indicator})");

        switch (component.Indicator)
        {
            case TechnicalIndicator.SMA:
                code.AppendLine($"        var sma = indicators[\"SMA_{(int)component.Parameters["period"]}\"];");
                break;
            case TechnicalIndicator.EMA:
                code.AppendLine($"        var ema = indicators[\"EMA_{(int)component.Parameters["period"]}\"];");
                break;
            case TechnicalIndicator.RSI:
                code.AppendLine($"        var rsi = indicators[\"RSI_{(int)component.Parameters["period"]}\"];");
                break;
            case TechnicalIndicator.MACD:
                code.AppendLine("        var macd = indicators[\"MACD\"];");
                code.AppendLine("        var macdSignal = indicators[\"MACD_Signal\"];");
                code.AppendLine("        var macdHistogram = indicators[\"MACD_Histogram\"];");
                break;
        }

        code.AppendLine("        // Add condition logic here");
    }

    private string SanitizeClassName(string name)
    {
        return string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_')).Trim();
    }

    private string SanitizeParameterName(string name)
    {
        return string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_')).Trim();
    }

    private List<Dictionary<string, double>> GenerateParameterCombinations(
        Dictionary<string, (double Min, double Max, double Step)> ranges, int maxIterations)
    {
        var combinations = new List<Dictionary<string, double>>();
        var keys = ranges.Keys.ToArray();

        if (keys.Length == 0) return combinations;

        // Simple grid search for first parameter
        var param1Values = GenerateParameterValues(ranges[keys[0]], maxIterations / keys.Length);

        foreach (var val1 in param1Values)
        {
            var combination = new Dictionary<string, double> { { keys[0], val1 } };
            combinations.Add(combination);
        }

        return combinations;
    }

    private List<double> GenerateParameterValues((double Min, double Max, double Step) range, int maxValues)
    {
        var values = new List<double>();
        var current = range.Min;

        while (current <= range.Max && values.Count < maxValues)
        {
            values.Add(current);
            current += range.Step;
        }

        return values;
    }

    private double EvaluateStrategyFitness(TradingStrategy strategy, List<double> returns)
    {
        // Simplified fitness function - Sharpe ratio
        if (returns.Count == 0) return 0;

        var avgReturn = returns.Average();
        var volatility = returns.StandardDeviation();

        return volatility > 0 ? avgReturn / volatility : 0;
    }

    private bool SimulateEntrySignal(TradingStrategy strategy, List<double> prices, int index)
    {
        // Simplified entry signal simulation
        return prices[index] > prices[index - 1]; // Price increasing
    }

    private bool SimulateExitSignal(TradingStrategy strategy, List<double> prices, int index)
    {
        // Simplified exit signal simulation
        return prices[index] < prices[index - 1]; // Price decreasing
    }

    private double CalculateAnnualizedReturn(double totalReturn, double years)
    {
        return Math.Pow(1 + totalReturn, 1.0 / years) - 1;
    }

    private double CalculateVolatility(List<double> prices)
    {
        if (prices.Count < 2) return 0;

        var returns = new List<double>();
        for (int i = 1; i < prices.Count; i++)
        {
            returns.Add(Math.Log(prices[i] / prices[i - 1]));
        }

        return returns.StandardDeviation() * Math.Sqrt(252); // Annualized
    }

    private double CalculateMaxDrawdown(List<double> prices)
    {
        if (prices.Count == 0) return 0;

        var maxDrawdown = 0.0;
        var peak = prices[0];

        foreach (var price in prices)
        {
            if (price > peak)
            {
                peak = price;
            }

            var drawdown = (peak - price) / peak;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        return maxDrawdown;
    }
}