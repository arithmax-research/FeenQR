using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace QuantResearchAgent.Services;

public class MarketImpactService
{
    private readonly ILogger<MarketImpactService> _logger;
    private readonly AlpacaService _alpacaService;
    private readonly ILLMService _llmService;

    public MarketImpactService(
        ILogger<MarketImpactService> logger,
        AlpacaService alpacaService,
        ILLMService llmService)
    {
        _logger = logger;
        _alpacaService = alpacaService;
        _llmService = llmService;
    }

    public class AlmgrenChrissModel
    {
        public double OptimalTrajectory { get; set; }
        public double TradingCost { get; set; }
        public double RiskTerm { get; set; }
        public double TotalCost { get; set; }
        public List<double> OptimalSchedule { get; set; } = new();
    }

    public class ImplementationShortfall
    {
        public double ExpectedCost { get; set; }
        public double RealizedCost { get; set; }
        public double Shortfall { get; set; }
        public double MarketImpact { get; set; }
        public double TimingRisk { get; set; }
    }

    public class PriceImpactModel
    {
        public double PermanentImpact { get; set; }
        public double TemporaryImpact { get; set; }
        public double TotalImpact { get; set; }
        public double PriceElasticity { get; set; }
    }

    /// <summary>
    /// Implements the Almgren-Chriss market impact model
    /// </summary>
    public AlmgrenChrissModel CalculateAlmgrenChriss(
        double totalShares, double timeHorizon, double volatility,
        double lambda = 1e-6, double gamma = 1.0, double eta = 0.1)
    {
        try
        {
            _logger.LogInformation($"Calculating Almgren-Chriss model for {totalShares} shares over {timeHorizon} periods");

            var model = new AlmgrenChrissModel();

            // Almgren-Chriss parameters
            // lambda: risk aversion parameter
            // gamma: permanent impact parameter
            // eta: temporary impact parameter

            int n = (int)timeHorizon; // Number of trading periods
            double x = totalShares; // Total shares to trade

            // Calculate optimal trading trajectory
            double tau = timeHorizon;
            double kappa = Math.Sqrt(lambda * volatility * volatility / (gamma * eta));

            // Optimal trajectory parameter
            model.OptimalTrajectory = kappa * Math.Tanh(kappa * tau);

            // Trading cost components
            double permanentCost = (gamma * x * x) / tau;
            double temporaryCost = eta * x * Math.Sqrt(volatility * volatility / tau);

            model.TradingCost = permanentCost + temporaryCost;
            model.RiskTerm = (lambda * volatility * volatility * x * x) / tau;
            model.TotalCost = model.TradingCost + model.RiskTerm;

            // Generate optimal trading schedule
            for (int t = 1; t <= n; t++)
            {
                double timeFraction = (double)t / n;
                double schedule = (Math.Sinh(kappa * (tau - timeFraction * tau)) /
                                 Math.Sinh(kappa * tau)) * x / tau;
                model.OptimalSchedule.Add(schedule);
            }

            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Almgren-Chriss model");
            throw;
        }
    }

    /// <summary>
    /// Calculates implementation shortfall
    /// </summary>
    public ImplementationShortfall CalculateImplementationShortfall(
        double benchmarkPrice, List<double> executionPrices, List<double> executionVolumes,
        double totalVolume, DateTime startTime, DateTime endTime)
    {
        try
        {
            var shortfall = new ImplementationShortfall();

            // Calculate volume-weighted average price (VWAP)
            double totalValue = 0;
            double totalExecutedVolume = 0;

            for (int i = 0; i < executionPrices.Count; i++)
            {
                totalValue += executionPrices[i] * executionVolumes[i];
                totalExecutedVolume += executionVolumes[i];
            }

            double vwap = totalValue / totalExecutedVolume;

            // Expected cost (difference from benchmark)
            shortfall.ExpectedCost = vwap - benchmarkPrice;

            // Market impact (difference between VWAP and arrival price)
            double arrivalPrice = executionPrices.FirstOrDefault();
            shortfall.MarketImpact = vwap - arrivalPrice;

            // Timing risk (variance in execution prices)
            double meanPrice = executionPrices.Average();
            double variance = executionPrices.Sum(p => Math.Pow(p - meanPrice, 2)) / executionPrices.Count;
            shortfall.TimingRisk = Math.Sqrt(variance);

            // Total shortfall
            shortfall.RealizedCost = shortfall.ExpectedCost;
            shortfall.Shortfall = shortfall.ExpectedCost + shortfall.TimingRisk;

            return shortfall;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating implementation shortfall");
            throw;
        }
    }

    /// <summary>
    /// Estimates price impact using square-root model
    /// </summary>
    public PriceImpactModel EstimatePriceImpact(
        double tradeSize, double averageDailyVolume, double volatility,
        double marketCap = 0, double beta = 1.0)
    {
        try
        {
            var model = new PriceImpactModel();

            // Participation rate
            double participationRate = tradeSize / averageDailyVolume;

            // Square-root impact model: Impact ∝ √(participation rate)
            double baseImpact = 0.1 * Math.Sqrt(participationRate); // 10 basis points base

            // Adjust for volatility
            double volatilityAdjustment = volatility * Math.Sqrt(participationRate);

            // Adjust for market cap (smaller companies have higher impact)
            double sizeAdjustment = marketCap > 0 ? Math.Log(1e12 / marketCap) / 10 : 0;

            // Adjust for beta
            double betaAdjustment = beta - 1.0;

            model.PermanentImpact = baseImpact + volatilityAdjustment + sizeAdjustment + betaAdjustment;
            model.TemporaryImpact = model.PermanentImpact * 0.3; // Temporary impact is ~30% of permanent
            model.TotalImpact = model.PermanentImpact + model.TemporaryImpact;

            // Price elasticity (inverse of impact)
            model.PriceElasticity = 1.0 / model.TotalImpact;

            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating price impact");
            throw;
        }
    }

    /// <summary>
    /// Calculates optimal execution strategy
    /// </summary>
    public Dictionary<string, object> CalculateOptimalExecution(
        double totalShares, double timeHorizon, double volatility,
        double currentPrice, double averageVolume)
    {
        try
        {
            var strategy = new Dictionary<string, object>();

            // Use Almgren-Chriss model for optimal trajectory
            var acModel = CalculateAlmgrenChriss(totalShares, timeHorizon, volatility);

            // Calculate participation rate
            double dailyParticipation = totalShares / averageVolume;
            strategy["ParticipationRate"] = dailyParticipation;

            // Risk assessment
            double riskScore = volatility * Math.Sqrt(timeHorizon) * dailyParticipation;
            strategy["RiskScore"] = riskScore;

            // Recommended execution style
            string executionStyle;
            if (dailyParticipation < 0.01)
                executionStyle = "Aggressive - Low market impact risk";
            else if (dailyParticipation < 0.05)
                executionStyle = "Normal - Moderate market impact";
            else if (dailyParticipation < 0.10)
                executionStyle = "Conservative - High market impact risk";
            else
                executionStyle = "Very Conservative - Extreme market impact";

            strategy["RecommendedStyle"] = executionStyle;
            strategy["AlmgrenChrissModel"] = acModel;
            strategy["EstimatedCost"] = acModel.TotalCost / totalShares * currentPrice; // Cost per share

            return strategy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating optimal execution");
            throw;
        }
    }

    /// <summary>
    /// Analyzes market impact from historical trades
    /// </summary>
    public Dictionary<string, double> AnalyzeHistoricalImpact(
        List<double> tradePrices, List<double> tradeVolumes,
        List<double> benchmarkPrices, double averageVolume)
    {
        try
        {
            var analysis = new Dictionary<string, double>();

            if (tradePrices.Count != tradeVolumes.Count || tradePrices.Count != benchmarkPrices.Count)
                throw new ArgumentException("All input arrays must have the same length");

            // Calculate VWAP
            double totalValue = 0;
            double totalVolume = 0;
            for (int i = 0; i < tradePrices.Count; i++)
            {
                totalValue += tradePrices[i] * tradeVolumes[i];
                totalVolume += tradeVolumes[i];
            }
            double vwap = totalValue / totalVolume;

            // Calculate benchmark average
            double benchmarkAvg = benchmarkPrices.Average();

            // Market impact metrics
            analysis["VWAP"] = vwap;
            analysis["BenchmarkPrice"] = benchmarkAvg;
            analysis["PriceImpact"] = vwap - benchmarkPrices.First();
            analysis["TotalImpact"] = vwap - benchmarkAvg;

            // Participation rate
            analysis["ParticipationRate"] = totalVolume / averageVolume;

            // Impact per unit volume
            analysis["ImpactPerShare"] = analysis["TotalImpact"] / totalVolume;

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing historical impact");
            throw;
        }
    }
}