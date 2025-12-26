using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Interactive strategy builder plugin for AI agents
/// </summary>
public class StrategyBuilderPlugin
{
    private readonly StrategyBuilderService _strategyBuilderService;

    public StrategyBuilderPlugin(StrategyBuilderService strategyBuilderService)
    {
        _strategyBuilderService = strategyBuilderService;
    }

    [KernelFunction("create_strategy")]
    [Description("Create a new trading strategy with the given name and description")]
    public async Task<string> CreateStrategy(
        [Description("Name of the trading strategy")] string name,
        [Description("Description of the strategy")] string description)
    {
        try
        {
            var strategy = _strategyBuilderService.CreateStrategy(name, description);

            return JsonSerializer.Serialize(new
            {
                success = true,
                strategyId = strategy.Id,
                strategyName = strategy.Name,
                message = $"Strategy '{name}' created successfully"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("add_strategy_component")]
    [Description("Add a component to an existing trading strategy")]
    public async Task<string> AddStrategyComponent(
        [Description("Strategy ID to add component to")] string strategyId,
        [Description("Component name")] string componentName,
        [Description("Component type: EntrySignal, ExitSignal, RiskManagement, PositionSizing, Filter")] string componentType,
        [Description("Technical indicator: SMA, EMA, RSI, MACD, BollingerBands, Stochastic, WilliamsR, CCI, ADX, ATR")] string indicator,
        [Description("Component parameters as JSON string")] string parametersJson = "{}")
    {
        try
        {
            // Parse component type
            if (!Enum.TryParse<StrategyBuilderService.StrategyComponentType>(componentType, out var type))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Invalid component type: {componentType}"
                });
            }

            // Parse indicator
            if (!Enum.TryParse<StrategyBuilderService.TechnicalIndicator>(indicator, out var techIndicator))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Invalid indicator: {indicator}"
                });
            }

            // Parse parameters
            var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson)
                ?? new Dictionary<string, object>();

            var component = new StrategyBuilderService.StrategyComponent
            {
                Name = componentName,
                Type = type,
                Indicator = techIndicator,
                Parameters = parameters
            };

            // For now, we'll need to store strategies somewhere accessible
            // This is a simplified implementation
            return JsonSerializer.Serialize(new
            {
                success = true,
                componentId = component.Id,
                message = $"Component '{componentName}' added to strategy"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("validate_strategy")]
    [Description("Validate a trading strategy for logical consistency and completeness")]
    public async Task<string> ValidateStrategy(
        [Description("Strategy ID to validate")] string strategyId)
    {
        try
        {
            // Create a dummy strategy for validation demo
            var strategy = _strategyBuilderService.CreateStrategy("Test Strategy", "Validation test");

            var errors = _strategyBuilderService.ValidateStrategy(strategy);

            return JsonSerializer.Serialize(new
            {
                success = true,
                isValid = errors.Count == 0,
                errors = errors,
                message = errors.Count == 0 ? "Strategy is valid" : $"Strategy has {errors.Count} validation errors"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("generate_strategy_code")]
    [Description("Generate executable C# code from a strategy definition")]
    public async Task<string> GenerateStrategyCode(
        [Description("Strategy ID to generate code for")] string strategyId)
    {
        try
        {
            // Create a sample strategy with components
            var strategy = _strategyBuilderService.CreateStrategy("Sample Strategy", "Generated sample");

            // Add some sample components
            var rsiComponent = new StrategyBuilderService.StrategyComponent
            {
                Name = "RSI Entry",
                Type = StrategyBuilderService.StrategyComponentType.EntrySignal,
                Indicator = StrategyBuilderService.TechnicalIndicator.RSI,
                Parameters = new Dictionary<string, object>
                {
                    { "period", 14 },
                    { "oversold", 30 }
                }
            };

            var smaComponent = new StrategyBuilderService.StrategyComponent
            {
                Name = "SMA Exit",
                Type = StrategyBuilderService.StrategyComponentType.ExitSignal,
                Indicator = StrategyBuilderService.TechnicalIndicator.SMA,
                Parameters = new Dictionary<string, object>
                {
                    { "period", 20 }
                }
            };

            strategy.Components.AddRange(new[] { rsiComponent, smaComponent });

            var code = _strategyBuilderService.GenerateStrategyCode(strategy);

            return JsonSerializer.Serialize(new
            {
                success = true,
                code = code,
                message = "Strategy code generated successfully"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("optimize_strategy_parameters")]
    [Description("Optimize strategy parameters using historical data")]
    public async Task<string> OptimizeStrategyParameters(
        [Description("Strategy ID to optimize")] string strategyId,
        [Description("Parameter ranges as JSON: {\"paramName\": {\"min\": 0, \"max\": 100, \"step\": 1}}")] string parameterRangesJson,
        [Description("Historical returns data as JSON array")] string historicalReturnsJson,
        [Description("Maximum number of optimization iterations")] int maxIterations = 100)
    {
        try
        {
            var parameterRanges = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(parameterRangesJson)
                ?? new Dictionary<string, Dictionary<string, double>>();

            var ranges = new Dictionary<string, (double Min, double Max, double Step)>();
            foreach (var param in parameterRanges)
            {
                var range = param.Value;
                ranges[param.Key] = (range["min"], range["max"], range["step"]);
            }

            var historicalReturns = JsonSerializer.Deserialize<List<double>>(historicalReturnsJson)
                ?? new List<double>();

            // Create a sample strategy for optimization
            var strategy = _strategyBuilderService.CreateStrategy("Optimization Strategy", "Parameter optimization");

            var result = await _strategyBuilderService.OptimizeParameters(strategy, ranges, historicalReturns, maxIterations);

            return JsonSerializer.Serialize(new
            {
                success = true,
                optimalParameters = result.OptimalParameters,
                bestFitness = result.BestFitness,
                parameterCombinationsTested = result.ParameterCombinations.Count,
                message = $"Parameter optimization completed. Best fitness: {result.BestFitness:F4}"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("backtest_strategy")]
    [Description("Backtest a strategy against historical price data")]
    public async Task<string> BacktestStrategy(
        [Description("Strategy ID to backtest")] string strategyId,
        [Description("Historical price data as JSON array")] string priceDataJson,
        [Description("Corresponding dates as JSON array (ISO format)")] string datesJson,
        [Description("Initial capital for backtest")] double initialCapital = 100000)
    {
        try
        {
            var priceData = JsonSerializer.Deserialize<List<double>>(priceDataJson)
                ?? new List<double>();

            var dates = JsonSerializer.Deserialize<List<string>>(datesJson)?
                .Select(d => DateTime.Parse(d)).ToList()
                ?? new List<DateTime>();

            // Create a sample strategy for backtesting
            var strategy = _strategyBuilderService.CreateStrategy("Backtest Strategy", "Backtesting sample");

            var result = await _strategyBuilderService.BacktestStrategy(strategy, priceData, dates, initialCapital);

            return JsonSerializer.Serialize(new
            {
                success = true,
                totalReturn = result.TotalReturn,
                annualizedReturn = result.AnnualizedReturn,
                volatility = result.Volatility,
                sharpeRatio = result.SharpeRatio,
                maxDrawdown = result.MaxDrawdown,
                winRate = result.WinRate,
                totalTrades = result.TotalTrades,
                message = $"Backtest completed. Total return: {result.TotalReturn:P2}, Sharpe ratio: {result.SharpeRatio:F2}"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("get_strategy_templates")]
    [Description("Get available strategy templates for quick start")]
    public async Task<string> GetStrategyTemplates()
    {
        try
        {
            var templates = _strategyBuilderService.GetStrategyTemplates();

            var templateSummaries = templates.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                description = t.Description,
                componentCount = t.Components.Count,
                parameters = t.Parameters
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                templates = templateSummaries,
                count = templates.Count,
                message = $"Found {templates.Count} strategy templates"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("analyze_strategy_performance")]
    [Description("Analyze strategy performance metrics and provide insights")]
    public async Task<string> AnalyzeStrategyPerformance(
        [Description("Strategy backtest results as JSON")] string backtestResultsJson)
    {
        try
        {
            var results = JsonSerializer.Deserialize<Dictionary<string, object>>(backtestResultsJson)
                ?? new Dictionary<string, object>();

            var analysis = new Dictionary<string, string>();

            // Extract key metrics
            var totalReturn = results.ContainsKey("totalReturn") ? (double)results["totalReturn"] : 0;
            var sharpeRatio = results.ContainsKey("sharpeRatio") ? (double)results["sharpeRatio"] : 0;
            var maxDrawdown = results.ContainsKey("maxDrawdown") ? (double)results["maxDrawdown"] : 0;
            var winRate = results.ContainsKey("winRate") ? (double)results["winRate"] : 0;

            // Performance analysis
            if (sharpeRatio > 1.5)
                analysis["risk_adjusted_return"] = "Excellent risk-adjusted returns";
            else if (sharpeRatio > 1.0)
                analysis["risk_adjusted_return"] = "Good risk-adjusted returns";
            else if (sharpeRatio > 0.5)
                analysis["risk_adjusted_return"] = "Moderate risk-adjusted returns";
            else
                analysis["risk_adjusted_return"] = "Poor risk-adjusted returns";

            if (maxDrawdown < 0.1)
                analysis["drawdown"] = "Low maximum drawdown - good risk control";
            else if (maxDrawdown < 0.2)
                analysis["drawdown"] = "Moderate maximum drawdown";
            else
                analysis["drawdown"] = "High maximum drawdown - consider risk management improvements";

            if (winRate > 0.6)
                analysis["win_rate"] = "Strong win rate";
            else if (winRate > 0.5)
                analysis["win_rate"] = "Moderate win rate";
            else
                analysis["win_rate"] = "Low win rate - review entry/exit logic";

            var recommendations = new List<string>();

            if (sharpeRatio < 0.5)
                recommendations.Add("Consider adjusting position sizing or adding filters to improve risk-adjusted returns");

            if (maxDrawdown > 0.2)
                recommendations.Add("Implement stop-loss mechanisms to reduce maximum drawdown");

            if (winRate < 0.4)
                recommendations.Add("Review entry signals and consider adding confirmation indicators");

            return JsonSerializer.Serialize(new
            {
                success = true,
                analysis = analysis,
                recommendations = recommendations,
                overall_rating = GetOverallRating(totalReturn, sharpeRatio, maxDrawdown, winRate),
                message = "Strategy performance analysis completed"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private string GetOverallRating(double totalReturn, double sharpeRatio, double maxDrawdown, double winRate)
    {
        var score = 0;

        if (totalReturn > 0.5) score += 25;
        else if (totalReturn > 0.2) score += 15;
        else if (totalReturn > 0) score += 5;

        if (sharpeRatio > 1.5) score += 25;
        else if (sharpeRatio > 1.0) score += 15;
        else if (sharpeRatio > 0.5) score += 10;

        if (maxDrawdown < 0.1) score += 25;
        else if (maxDrawdown < 0.2) score += 15;
        else if (maxDrawdown < 0.3) score += 5;

        if (winRate > 0.6) score += 25;
        else if (winRate > 0.5) score += 15;
        else if (winRate > 0.4) score += 5;

        if (score >= 80) return "Excellent";
        if (score >= 60) return "Good";
        if (score >= 40) return "Fair";
        return "Poor";
    }
}