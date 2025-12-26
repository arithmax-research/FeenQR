using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using MathNet.Numerics.Statistics;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services;

/// <summary>
/// Monte Carlo simulation service for probabilistic analysis and scenario modeling
/// </summary>
public class MonteCarloService
{
    private readonly ILogger<MonteCarloService> _logger;
    private readonly Random _random;

    public MonteCarloService(ILogger<MonteCarloService> logger)
    {
        _logger = logger;
        _random = new Random();
    }

    /// <summary>
    /// Run Monte Carlo simulation for portfolio returns
    /// </summary>
    public MonteCarloResult RunPortfolioSimulation(
        Dictionary<string, double> weights,
        Dictionary<string, double> expectedReturns,
        Dictionary<string, double> volatilities,
        Dictionary<string, double[,]> correlationMatrix,
        double initialInvestment,
        int numSimulations,
        int timeHorizon)
    {
        _logger.LogInformation("Running portfolio Monte Carlo simulation with {NumSimulations} simulations over {TimeHorizon} periods", numSimulations, timeHorizon);

        var results = new List<double>();
        var paths = new List<List<double>>();
        var varResults = new List<double>();
        var cvarResults = new List<double>();

        // Generate correlated random returns
        var correlatedReturns = GenerateCorrelatedReturns(
            weights.Keys.ToList(),
            expectedReturns,
            volatilities,
            correlationMatrix,
            numSimulations,
            timeHorizon);

        for (int sim = 0; sim < numSimulations; sim++)
        {
            double portfolioValue = initialInvestment;
            var path = new List<double> { portfolioValue };

            for (int period = 0; period < timeHorizon; period++)
            {
                double periodReturn = 0;
                foreach (var asset in weights.Keys)
                {
                    periodReturn += weights[asset] * correlatedReturns[asset][sim, period];
                }

                portfolioValue *= (1 + periodReturn);
                path.Add(portfolioValue);
            }

            results.Add(portfolioValue);
            paths.Add(path);

            // Calculate VaR and CVaR for this simulation
            var finalReturns = correlatedReturns.Values
                .SelectMany(r => Enumerable.Range(0, timeHorizon).Select(p => r[sim, p]))
                .ToList();

            varResults.Add(CalculateVaR(finalReturns, 0.05));
            cvarResults.Add(CalculateCVaR(finalReturns, 0.05));
        }

        var finalValues = results.OrderBy(x => x).ToList();

        return new MonteCarloResult
        {
            ExpectedValue = results.Average(),
            MedianValue = finalValues[finalValues.Count / 2],
            StandardDeviation = Statistics.StandardDeviation(results),
            MinValue = finalValues.Min(),
            MaxValue = finalValues.Max(),
            VaR95 = CalculateVaR(results, 0.05),
            CVaR95 = CalculateCVaR(results, 0.05),
            Percentile5 = finalValues[(int)(finalValues.Count * 0.05)],
            Percentile95 = finalValues[(int)(finalValues.Count * 0.95)],
            SimulationPaths = paths.Take(100).ToList(), // Store first 100 paths for visualization
            AllFinalValues = finalValues,
            AverageVaR = varResults.Average(),
            AverageCVaR = cvarResults.Average()
        };
    }

    /// <summary>
    /// Run Monte Carlo simulation for option pricing
    /// </summary>
    public MonteCarloOptionResult RunOptionPricingSimulation(
        string optionType,
        double spotPrice,
        double strikePrice,
        double timeToExpiration,
        double riskFreeRate,
        double volatility,
        int numSimulations)
    {
        _logger.LogInformation("Running Monte Carlo option pricing simulation for {OptionType} option", optionType);

        var payoffs = new List<double>();
        var paths = new List<List<double>>();

        for (int sim = 0; sim < numSimulations; sim++)
        {
            var path = new List<double> { spotPrice };
            double currentPrice = spotPrice;

            // Generate geometric Brownian motion path
            for (double t = 0.01; t <= timeToExpiration; t += 0.01)
            {
                double drift = (riskFreeRate - 0.5 * volatility * volatility) * 0.01;
                double diffusion = volatility * Math.Sqrt(0.01) * Normal.Sample(_random, 0, 1);
                currentPrice *= Math.Exp(drift + diffusion);
                path.Add(currentPrice);
            }

            double payoff = 0;
            if (optionType.ToLower() == "call")
            {
                payoff = Math.Max(currentPrice - strikePrice, 0);
            }
            else if (optionType.ToLower() == "put")
            {
                payoff = Math.Max(strikePrice - currentPrice, 0);
            }

            payoffs.Add(payoff);
            paths.Add(path);
        }

        double optionPrice = payoffs.Average() * Math.Exp(-riskFreeRate * timeToExpiration);
        double standardError = Statistics.StandardDeviation(payoffs) / Math.Sqrt(numSimulations) * Math.Exp(-riskFreeRate * timeToExpiration);

        return new MonteCarloOptionResult
        {
            OptionPrice = optionPrice,
            StandardError = standardError,
            ConfidenceInterval95 = new double[] {
                optionPrice - 1.96 * standardError,
                optionPrice + 1.96 * standardError
            },
            Payoffs = payoffs,
            SamplePaths = paths.Take(50).ToList()
        };
    }

    /// <summary>
    /// Run scenario analysis with custom scenarios
    /// </summary>
    public ScenarioAnalysisResult RunScenarioAnalysis(
        Dictionary<string, double> baseWeights,
        Dictionary<string, double> baseReturns,
        List<Scenario> scenarios,
        double initialInvestment)
    {
        _logger.LogInformation("Running scenario analysis with {NumScenarios} scenarios", scenarios.Count);

        var results = new List<ScenarioResult>();

        foreach (var scenario in scenarios)
        {
            double portfolioValue = initialInvestment;

            // Apply scenario shocks to returns
            var scenarioReturns = new Dictionary<string, double>(baseReturns);
            foreach (var shock in scenario.Shocks)
            {
                if (scenarioReturns.ContainsKey(shock.Asset))
                {
                    scenarioReturns[shock.Asset] *= (1 + shock.ReturnShock);
                }
            }

            // Calculate portfolio return under scenario
            double portfolioReturn = 0;
            foreach (var asset in baseWeights.Keys)
            {
                portfolioReturn += baseWeights[asset] * scenarioReturns[asset];
            }

            portfolioValue *= (1 + portfolioReturn);

            results.Add(new ScenarioResult
            {
                ScenarioName = scenario.Name,
                PortfolioValue = portfolioValue,
                PortfolioReturn = portfolioReturn,
                Probability = scenario.Probability
            });
        }

        // Calculate expected value and risk metrics
        double expectedValue = results.Sum(r => r.PortfolioValue * r.Probability);
        var returns = results.Select(r => r.PortfolioReturn).ToList();
        double worstCase = results.Min(r => r.PortfolioValue);
        double bestCase = results.Max(r => r.PortfolioValue);

        return new ScenarioAnalysisResult
        {
            ExpectedValue = expectedValue,
            WorstCase = worstCase,
            BestCase = bestCase,
            ScenarioResults = results,
            VaR95 = CalculateVaR(returns, 0.05),
            CVaR95 = CalculateCVaR(returns, 0.05)
        };
    }

    /// <summary>
    /// Generate correlated random returns using Cholesky decomposition
    /// </summary>
    private Dictionary<string, double[,]> GenerateCorrelatedReturns(
        List<string> assets,
        Dictionary<string, double> expectedReturns,
        Dictionary<string, double> volatilities,
        Dictionary<string, double[,]> correlationMatrix,
        int numSimulations,
        int timeHorizon)
    {
        var results = new Dictionary<string, double[,]>();

        // Initialize result matrices
        foreach (var asset in assets)
        {
            results[asset] = new double[numSimulations, timeHorizon];
        }

        // Generate uncorrelated normal random variables
        var uncorrelatedNormals = new double[assets.Count, numSimulations * timeHorizon];
        for (int i = 0; i < assets.Count; i++)
        {
            for (int j = 0; j < numSimulations * timeHorizon; j++)
            {
                uncorrelatedNormals[i, j] = Normal.Sample(_random, 0, 1);
            }
        }

        // Apply Cholesky decomposition for correlation
        var choleskyMatrix = CholeskyDecomposition(correlationMatrix[assets[0]]);

        for (int sim = 0; sim < numSimulations; sim++)
        {
            for (int period = 0; period < timeHorizon; period++)
            {
                // Generate correlated normals
                var correlatedNormals = new double[assets.Count];
                for (int i = 0; i < assets.Count; i++)
                {
                    correlatedNormals[i] = 0;
                    for (int j = 0; j < assets.Count; j++)
                    {
                        correlatedNormals[i] += choleskyMatrix[i, j] * uncorrelatedNormals[j, sim * timeHorizon + period];
                    }
                }

                // Convert to returns
                for (int i = 0; i < assets.Count; i++)
                {
                    var asset = assets[i];
                    double annualizedReturn = expectedReturns[asset];
                    double annualizedVol = volatilities[asset];

                    // Convert to period return (assuming daily)
                    double periodDrift = annualizedReturn / 252.0;
                    double periodVol = annualizedVol / Math.Sqrt(252.0);

                    double periodReturn = periodDrift + periodVol * correlatedNormals[i];
                    results[asset][sim, period] = periodReturn;
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Simple Cholesky decomposition for correlation matrix
    /// </summary>
    private double[,] CholeskyDecomposition(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        var result = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = 0;
                for (int k = 0; k < j; k++)
                {
                    sum += result[i, k] * result[j, k];
                }

                if (i == j)
                {
                    result[i, j] = Math.Sqrt(matrix[i, i] - sum);
                }
                else
                {
                    result[i, j] = (matrix[i, j] - sum) / result[j, j];
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Calculate Value at Risk
    /// </summary>
    private double CalculateVaR(List<double> returns, double confidenceLevel)
    {
        var sortedReturns = returns.OrderBy(x => x).ToList();
        int index = (int)((1 - confidenceLevel) * sortedReturns.Count);
        return -sortedReturns[index]; // Negative because VaR is loss
    }

    /// <summary>
    /// Calculate Conditional Value at Risk (CVaR)
    /// </summary>
    private double CalculateCVaR(List<double> returns, double confidenceLevel)
    {
        var sortedReturns = returns.OrderBy(x => x).ToList();
        int varIndex = (int)((1 - confidenceLevel) * sortedReturns.Count);
        var tailReturns = sortedReturns.Take(varIndex + 1).ToList();
        return -tailReturns.Average(); // Negative because CVaR is expected loss
    }
}

/// <summary>
/// Result of Monte Carlo portfolio simulation
/// </summary>
public class MonteCarloResult
{
    public double ExpectedValue { get; set; }
    public double MedianValue { get; set; }
    public double StandardDeviation { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double VaR95 { get; set; }
    public double CVaR95 { get; set; }
    public double Percentile5 { get; set; }
    public double Percentile95 { get; set; }
    public List<List<double>> SimulationPaths { get; set; } = new();
    public List<double> AllFinalValues { get; set; } = new();
    public double AverageVaR { get; set; }
    public double AverageCVaR { get; set; }
}

/// <summary>
/// Result of Monte Carlo option pricing
/// </summary>
public class MonteCarloOptionResult
{
    public double OptionPrice { get; set; }
    public double StandardError { get; set; }
    public double[] ConfidenceInterval95 { get; set; } = new double[2];
    public List<double> Payoffs { get; set; } = new();
    public List<List<double>> SamplePaths { get; set; } = new();
}

/// <summary>
/// Scenario definition for scenario analysis
/// </summary>
public class Scenario
{
    public string Name { get; set; } = string.Empty;
    public double Probability { get; set; }
    public List<Shock> Shocks { get; set; } = new();
}

/// <summary>
/// Shock applied to an asset in a scenario
/// </summary>
public class Shock
{
    public string Asset { get; set; } = string.Empty;
    public double ReturnShock { get; set; } // e.g., -0.1 for -10% shock
}

/// <summary>
/// Result of scenario analysis
/// </summary>
public class ScenarioAnalysisResult
{
    public double ExpectedValue { get; set; }
    public double WorstCase { get; set; }
    public double BestCase { get; set; }
    public List<ScenarioResult> ScenarioResults { get; set; } = new();
    public double VaR95 { get; set; }
    public double CVaR95 { get; set; }
}

/// <summary>
/// Individual scenario result
/// </summary>
public class ScenarioResult
{
    public string ScenarioName { get; set; } = string.Empty;
    public double PortfolioValue { get; set; }
    public double PortfolioReturn { get; set; }
    public double Probability { get; set; }
}