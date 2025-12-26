using System;
using System.Collections.Generic;

namespace QuantResearchAgent.Core
{
    // Phase 3.2: Advanced Optimization Models
    public class BlackLittermanModel
    {
        public DateTime AnalysisDate { get; set; }
        public Dictionary<string, double> PriorReturns { get; set; } = new();
        public Dictionary<string, double> MarketWeights { get; set; } = new();
        public double RiskAversion { get; set; }
        public Dictionary<string, double> PosteriorReturns { get; set; } = new();
        public Dictionary<string, double> OptimalWeights { get; set; } = new();
        public double ExpectedReturn { get; set; }
        public double ExpectedVolatility { get; set; }
        public double SharpeRatio { get; set; }
    }

    public class RiskParityPortfolio
    {
        public DateTime AnalysisDate { get; set; }
        public Dictionary<string, double> AssetWeights { get; set; } = new();
        public Dictionary<string, double> RiskContributions { get; set; } = new();
        public double TotalRisk { get; set; }
        public double ExpectedReturn { get; set; }
        public bool Converged { get; set; }
        public int Iterations { get; set; }
    }

    public class HierarchicalRiskParity
    {
        public DateTime AnalysisDate { get; set; }
        public List<string> Assets { get; set; } = new();
        public Dictionary<string, double> Weights { get; set; } = new();
        public List<Cluster> Clusters { get; set; } = new();
        public double TotalRisk { get; set; }
        public double ExpectedReturn { get; set; }
    }

    public class Cluster
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Assets { get; set; } = new();
        public double Weight { get; set; }
        public double Risk { get; set; }
    }

    public class MinimumVariancePortfolio
    {
        public DateTime AnalysisDate { get; set; }
        public Dictionary<string, double> Weights { get; set; } = new();
        public double PortfolioVariance { get; set; }
        public double PortfolioVolatility { get; set; }
        public double ExpectedReturn { get; set; }
        public bool Success { get; set; }
    }

    // Phase 3.3: Risk Management Models
    public class ValueAtRisk
    {
        public DateTime AnalysisDate { get; set; }
        public string Method { get; set; } = "Historical"; // "Historical", "Parametric", "MonteCarlo"
        public double ConfidenceLevel { get; set; }
        public double VaR { get; set; }
        public double ExpectedShortfall { get; set; }
        public Dictionary<string, double> ComponentVaR { get; set; } = new();
        public double DiversificationRatio { get; set; }
    }

    public class StressTestResult
    {
        public DateTime AnalysisDate { get; set; }
        public string ScenarioName { get; set; } = string.Empty;
        public Dictionary<string, double> ShockReturns { get; set; } = new();
        public double PortfolioReturn { get; set; }
        public double PortfolioLoss { get; set; }
        public Dictionary<string, double> AssetContributions { get; set; } = new();
        public bool BreachThreshold { get; set; }
        public double Threshold { get; set; }
    }

    public class RiskFactorAttribution
    {
        public DateTime AnalysisDate { get; set; }
        public Dictionary<string, double> FactorExposures { get; set; } = new();
        public Dictionary<string, double> FactorContributions { get; set; } = new();
        public double TotalAttribution { get; set; }
        public double ResidualRisk { get; set; }
        public double R2 { get; set; }
    }

    public class RiskReport
    {
        public DateTime ReportDate { get; set; }
        public ValueAtRisk VaR { get; set; } = new();
        public List<StressTestResult> StressTests { get; set; } = new();
        public RiskFactorAttribution FactorAttribution { get; set; } = new();
        public Dictionary<string, double> RiskMetrics { get; set; } = new();
        public string RiskRating { get; set; } = "Medium"; // "Low", "Medium", "High", "Extreme"
    }

    // Optimization Constraints
    public class OptimizationConstraints
    {
        public Dictionary<string, double> MinWeights { get; set; } = new();
        public Dictionary<string, double> MaxWeights { get; set; } = new();
        public double MinReturn { get; set; }
        public double MaxRisk { get; set; }
        public List<string> ExcludedAssets { get; set; } = new();
        public bool AllowShortSelling { get; set; }
        public double TurnoverLimit { get; set; }
    }

    // Optimization Views for Black-Litterman
    public class BlackLittermanViews
    {
        public Dictionary<string, double> AbsoluteViews { get; set; } = new(); // Asset -> Expected Return
        public Dictionary<(string, string), double> RelativeViews { get; set; } = new(); // (Asset1, Asset2) -> Expected Return Difference
        public Dictionary<string, double> ViewConfidences { get; set; } = new();
    }
}