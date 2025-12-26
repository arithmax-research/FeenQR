using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Monte Carlo simulation plugin for probabilistic analysis
/// </summary>
public class MonteCarloPlugin
{
    private readonly MonteCarloService _monteCarloService;

    public MonteCarloPlugin(MonteCarloService monteCarloService)
    {
        _monteCarloService = monteCarloService;
    }

    [KernelFunction("run_portfolio_monte_carlo")]
    [Description("Run Monte Carlo simulation for portfolio analysis with correlated assets")]
    public async Task<string> RunPortfolioMonteCarlo(
        [Description("Dictionary of asset weights (JSON format: {\"AAPL\": 0.3, \"MSFT\": 0.4, \"GOOGL\": 0.3})")] string weightsJson,
        [Description("Dictionary of expected annual returns (JSON format: {\"AAPL\": 0.12, \"MSFT\": 0.10, \"GOOGL\": 0.15})")] string expectedReturnsJson,
        [Description("Dictionary of annual volatilities (JSON format: {\"AAPL\": 0.25, \"MSFT\": 0.22, \"GOOGL\": 0.30})")] string volatilitiesJson,
        [Description("Correlation matrix as JSON array of arrays")] string correlationMatrixJson,
        [Description("Initial investment amount")] double initialInvestment = 100000,
        [Description("Number of simulations to run")] int numSimulations = 10000,
        [Description("Time horizon in periods (e.g., 252 for one year of daily returns)")] int timeHorizon = 252)
    {
        try
        {
            var weights = JsonSerializer.Deserialize<Dictionary<string, double>>(weightsJson)
                ?? throw new ArgumentException("Invalid weights JSON");
            var expectedReturns = JsonSerializer.Deserialize<Dictionary<string, double>>(expectedReturnsJson)
                ?? throw new ArgumentException("Invalid expected returns JSON");
            var volatilities = JsonSerializer.Deserialize<Dictionary<string, double>>(volatilitiesJson)
                ?? throw new ArgumentException("Invalid volatilities JSON");

            // Parse correlation matrix
            var correlationArray = JsonSerializer.Deserialize<double[][]>(correlationMatrixJson)
                ?? throw new ArgumentException("Invalid correlation matrix JSON");

            var correlationMatrix = new Dictionary<string, double[,]>();
            var assets = weights.Keys.ToList();
            for (int i = 0; i < assets.Count; i++)
            {
                correlationMatrix[assets[i]] = new double[assets.Count, assets.Count];
                for (int j = 0; j < assets.Count; j++)
                {
                    correlationMatrix[assets[i]][i, j] = correlationArray[i][j];
                }
            }

            var result = _monteCarloService.RunPortfolioSimulation(
                weights, expectedReturns, volatilities, correlationMatrix,
                initialInvestment, numSimulations, timeHorizon);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return $"Error running portfolio Monte Carlo simulation: {ex.Message}";
        }
    }

    [KernelFunction("run_option_monte_carlo")]
    [Description("Run Monte Carlo simulation for option pricing")]
    public async Task<string> RunOptionMonteCarlo(
        [Description("Option type: 'call' or 'put'")] string optionType,
        [Description("Current spot price of underlying asset")] double spotPrice,
        [Description("Strike price of the option")] double strikePrice,
        [Description("Time to expiration in years")] double timeToExpiration,
        [Description("Risk-free interest rate (annual)")] double riskFreeRate,
        [Description("Volatility of underlying asset (annual)")] double volatility,
        [Description("Number of simulations")] int numSimulations = 10000)
    {
        try
        {
            var result = _monteCarloService.RunOptionPricingSimulation(
                optionType, spotPrice, strikePrice, timeToExpiration,
                riskFreeRate, volatility, numSimulations);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return $"Error running option Monte Carlo simulation: {ex.Message}";
        }
    }

    [KernelFunction("run_scenario_analysis")]
    [Description("Run scenario analysis with custom shock scenarios")]
    public async Task<string> RunScenarioAnalysis(
        [Description("Dictionary of base portfolio weights (JSON format)")] string weightsJson,
        [Description("Dictionary of base expected returns (JSON format)")] string baseReturnsJson,
        [Description("List of scenarios with shocks (JSON format)")] string scenariosJson,
        [Description("Initial investment amount")] double initialInvestment = 100000)
    {
        try
        {
            var weights = JsonSerializer.Deserialize<Dictionary<string, double>>(weightsJson)
                ?? throw new ArgumentException("Invalid weights JSON");
            var baseReturns = JsonSerializer.Deserialize<Dictionary<string, double>>(baseReturnsJson)
                ?? throw new ArgumentException("Invalid base returns JSON");
            var scenarios = JsonSerializer.Deserialize<List<Scenario>>(scenariosJson)
                ?? throw new ArgumentException("Invalid scenarios JSON");

            var result = _monteCarloService.RunScenarioAnalysis(weights, baseReturns, scenarios, initialInvestment);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return $"Error running scenario analysis: {ex.Message}";
        }
    }

    [KernelFunction("analyze_monte_carlo_results")]
    [Description("Analyze Monte Carlo simulation results and provide insights")]
    public async Task<string> AnalyzeMonteCarloResults(
        [Description("JSON string of Monte Carlo simulation results")] string resultsJson)
    {
        try
        {
            var results = JsonSerializer.Deserialize<MonteCarloResult>(resultsJson);
            if (results == null)
                return "Invalid Monte Carlo results JSON";

            var analysis = new
            {
                Summary = new
                {
                    ExpectedFinalValue = $"${results.ExpectedValue:N0}",
                    MedianFinalValue = $"${results.MedianValue:N0}",
                    StandardDeviation = $"${results.StandardDeviation:N0}",
                    ValueRange = $"${results.MinValue:N0} - ${results.MaxValue:N0}",
                    RiskMetrics = new
                    {
                        VaR95 = $"${results.VaR95:N0} (5% chance of losing more)",
                        CVaR95 = $"${results.CVaR95:N0} (expected loss in worst 5%)"
                    }
                },
                Percentiles = new
                {
                    Percentile5 = $"${results.Percentile5:N0} (5% chance of value below this)",
                    Percentile95 = $"${results.Percentile95:N0} (95% chance of value below this)"
                },
                Insights = new
                {
                    RiskLevel = results.StandardDeviation / results.ExpectedValue > 0.3 ? "High Risk" :
                               results.StandardDeviation / results.ExpectedValue > 0.15 ? "Medium Risk" : "Low Risk",
                    UpsidePotential = (results.MaxValue - results.ExpectedValue) / results.ExpectedValue,
                    DownsideRisk = (results.ExpectedValue - results.MinValue) / results.ExpectedValue,
                    Recommendation = results.VaR95 > results.ExpectedValue * 0.2 ?
                        "High risk - consider risk management strategies" :
                        "Acceptable risk level for investment"
                }
            };

            return JsonSerializer.Serialize(analysis, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return $"Error analyzing Monte Carlo results: {ex.Message}";
        }
    }
}