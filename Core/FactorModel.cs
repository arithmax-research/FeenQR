using System;
using System.Collections.Generic;

namespace QuantResearchAgent.Core;

/// <summary>
/// Represents a factor model for portfolio analysis
/// </summary>
public class FactorModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Factors { get; set; } = new();
    public Dictionary<string, double> FactorReturns { get; set; } = new();
    public Dictionary<string, double> FactorExposures { get; set; } = new();
    public double Alpha { get; set; }
    public double R2 { get; set; }
    public DateTime AnalysisDate { get; set; }
}

/// <summary>
/// Fama-French 3-Factor Model
/// </summary>
public class FamaFrench3FactorModel : FactorModel
{
    public FamaFrench3FactorModel()
    {
        Name = "Fama-French 3-Factor";
        Description = "Market, Size, and Value factors";
        Factors = new List<string> { "Market", "SMB", "HML" };
    }

    public double MarketBeta { get; set; }
    public double SizeBeta { get; set; } // SMB - Small Minus Big
    public double ValueBeta { get; set; } // HML - High Minus Low
}

/// <summary>
/// Carhart 4-Factor Model (Fama-French 3 + Momentum)
/// </summary>
public class Carhart4FactorModel : FamaFrench3FactorModel
{
    public Carhart4FactorModel()
    {
        Name = "Carhart 4-Factor";
        Description = "Fama-French 3-Factor + Momentum";
        Factors = new List<string> { "Market", "SMB", "HML", "MOM" };
    }

    public double MomentumBeta { get; set; } // MOM - Momentum
}

/// <summary>
/// Custom factor model for user-defined factors
/// </summary>
public class CustomFactorModel : FactorModel
{
    public CustomFactorModel(string name, string description, List<string> factors)
    {
        Name = name;
        Description = description;
        Factors = factors;
    }
}

/// <summary>
/// Factor attribution analysis results
/// </summary>
public class FactorAttribution
{
    public string Asset { get; set; } = string.Empty;
    public Dictionary<string, double> FactorContributions { get; set; } = new();
    public double TotalAttribution { get; set; }
    public double ResidualReturn { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}

/// <summary>
/// Factor data point
/// </summary>
public class FactorData
{
    public DateTime Date { get; set; }
    public Dictionary<string, double> FactorValues { get; set; } = new();
}

/// <summary>
/// Factor model regression results
/// </summary>
public class FactorRegressionResult
{
    public string Asset { get; set; } = string.Empty;
    public Dictionary<string, double> Coefficients { get; set; } = new();
    public double Intercept { get; set; }
    public double RSquared { get; set; }
    public double AdjustedRSquared { get; set; }
    public double FStatistic { get; set; }
    public Dictionary<string, double> TStatistics { get; set; } = new();
    public Dictionary<string, double> PValues { get; set; } = new();
    public List<double> Residuals { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
}
