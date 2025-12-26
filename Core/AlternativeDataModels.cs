using System;
using System.Collections.Generic;

namespace QuantResearchAgent.Core;

/// <summary>
/// Represents alternative data sources and their analysis results
/// </summary>
public class AlternativeDataModels
{
    // SEC Filings Models
    public class SECFiling
    {
        public string CompanyName { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public string FilingType { get; set; } = string.Empty; // 10-K, 10-Q, 8-K, etc.
        public DateTime FilingDate { get; set; }
        public DateTime PeriodEndDate { get; set; }
        public string AccessionNumber { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object> ExtractedData { get; set; } = new();
        public List<RiskFactor> RiskFactors { get; set; } = new();
        public ManagementDiscussion MDandA { get; set; } = new();
    }

    public class RiskFactor
    {
        public string Category { get; set; } = string.Empty; // Market, Operational, Financial, Legal, etc.
        public string Description { get; set; } = string.Empty;
        public double Severity { get; set; } // 1-10 scale
        public List<string> Keywords { get; set; } = new();
    }

    public class ManagementDiscussion
    {
        public string Summary { get; set; } = string.Empty;
        public Dictionary<string, double> KeyMetrics { get; set; } = new();
        public List<string> StrategicInitiatives { get; set; } = new();
        public SentimentAnalysis Sentiment { get; set; } = new();
    }

    // Earnings Call Models
    public class EarningsCall
    {
        public string CompanyName { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public DateTime CallDate { get; set; }
        public Quarter Quarter { get; set; }
        public int Year { get; set; }
        public string Transcript { get; set; } = string.Empty;
        public List<SpeakerSegment> Segments { get; set; } = new();
        public List<SpeakerSegment> SpeakerSegments { get; set; } = new(); // Alias for Segments
        public FinancialMetrics Metrics { get; set; } = new();
        public ForwardGuidance Guidance { get; set; } = new();
        public SentimentAnalysis OverallSentiment { get; set; } = new();
        public List<QandAExchange> QandA { get; set; } = new();
    }

    public class SpeakerSegment
    {
        public string Speaker { get; set; } = string.Empty; // CEO, CFO, Analyst, etc.
        public string SpeakerName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public TimeSpan Timestamp { get; set; } // Alias for StartTime
        public SentimentAnalysis Sentiment { get; set; } = new();
        public List<string> KeyTopics { get; set; } = new();
    }

    public class QandAExchange
    {
        public string AnalystName { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public TimeSpan Timestamp { get; set; }
        public SentimentAnalysis QuestionSentiment { get; set; } = new();
        public SentimentAnalysis AnswerSentiment { get; set; } = new();
    }

    public class FinancialMetrics
    {
        public Dictionary<string, double> ReportedMetrics { get; set; } = new();
        public Dictionary<string, double> GuidanceMetrics { get; set; } = new();
        public List<string> MetricComparisons { get; set; } = new();
        public double Revenue { get; set; }
        public double EPS { get; set; }
        public double GuidanceLow { get; set; }
        public double GuidanceHigh { get; set; }
        public List<string> AIExtractedInsights { get; set; } = new();
    }

    public class ForwardGuidance
    {
        public string QualitativeGuidance { get; set; } = string.Empty;
        public Dictionary<string, (double Min, double Max)> QuantitativeGuidance { get; set; } = new();
        public List<string> Assumptions { get; set; } = new();
        public SentimentAnalysis GuidanceSentiment { get; set; } = new();
    }

    // Supply Chain Models
    public class SupplyChainData
    {
        public string CompanyName { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public string CompanyTicker { get; set; } = string.Empty; // Alias for Ticker
        public DateTime AnalysisDate { get; set; }
        public DateTime DataDate { get; set; } // Alias for AnalysisDate
        public List<Supplier> Suppliers { get; set; } = new();
        public InventoryMetrics Inventory { get; set; } = new();
        public InventoryMetrics InventoryMetrics { get; set; } = new(); // Alias for Inventory
        public LogisticsData Logistics { get; set; } = new();
        public LogisticsData LogisticsData { get; set; } = new(); // Alias for Logistics
        public SupplyChainRisk RiskAssessment { get; set; } = new();
        public Dictionary<string, object> RiskIndicators { get; set; } = new();
    }

    public class Supplier
    {
        public string SupplierName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty; // Alias for SupplierName
        public string RelationshipType { get; set; } = string.Empty; // Tier 1, Tier 2, etc.
        public double RevenuePercentage { get; set; } // % of company's revenue
        public string Country { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public SupplierRisk RiskProfile { get; set; } = new();
        public double RiskScore { get; set; } // Alias for RiskProfile overall score
    }

    public class SupplierRisk
    {
        public double FinancialRisk { get; set; } // 1-10 scale
        public double OperationalRisk { get; set; } // 1-10 scale
        public double GeopoliticalRisk { get; set; } // 1-10 scale
        public List<string> RiskFactors { get; set; } = new();
    }

    public class InventoryMetrics
    {
        public double InventoryTurnover { get; set; }
        public double DaysInventoryOutstanding { get; set; }
        public double InventoryTurnoverRatio { get; set; } // Alias for InventoryTurnover
        public double InventoryToSalesRatio { get; set; }
        public double SupplyChainEfficiency { get; set; }
        public Dictionary<string, double> InventoryByCategory { get; set; } = new();
        public List<InventoryTrend> Trends { get; set; } = new();
    }

    public class InventoryTrend
    {
        public DateTime Date { get; set; }
        public double InventoryLevel { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    public class LogisticsData
    {
        public double ShippingCosts { get; set; }
        public double DeliveryTime { get; set; }
        public Dictionary<string, double> ShippingByRegion { get; set; } = new();
        public List<LogisticsDisruption> Disruptions { get; set; } = new();
    }

    public class LogisticsDisruption
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty; // Port congestion, weather, etc.
        public string Description { get; set; } = string.Empty;
        public double Impact { get; set; } // Days delayed, cost increase %
    }

    public class SupplyChainRisk
    {
        public double OverallRiskScore { get; set; } // 1-10 scale
        public Dictionary<string, double> RiskByCategory { get; set; } = new();
        public List<string> MitigationStrategies { get; set; } = new();
        public List<SupplyChainVulnerability> Vulnerabilities { get; set; } = new();
    }

    public class SupplyChainVulnerability
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Severity { get; set; } // 1-10 scale
        public List<string> AffectedSuppliers { get; set; } = new();
    }

    // Common Models
    public class SentimentAnalysis
    {
        public double OverallScore { get; set; } // -1 to 1 scale
        public double Confidence { get; set; } // 0-1 scale
        public Dictionary<string, double> TopicSentiments { get; set; } = new();
        public List<string> KeyPositivePhrases { get; set; } = new();
        public List<string> KeyNegativePhrases { get; set; } = new();
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    public enum Quarter
    {
        Q1, Q2, Q3, Q4
    }

    // Analysis Results
    public class SECFilingAnalysis
    {
        public SECFiling Filing { get; set; } = new();
        public DateTime AnalysisDate { get; set; }
        public Dictionary<string, object> Insights { get; set; } = new();
        public List<string> KeyFindings { get; set; } = new();
        public SentimentAnalysis ContentSentiment { get; set; } = new();
    }

    public class EarningsCallAnalysis
    {
        public EarningsCall Call { get; set; } = new();
        public EarningsCall EarningsCall { get; set; } = new(); // Alias for Call
        public DateTime AnalysisDate { get; set; }
        public Dictionary<string, object> Insights { get; set; } = new();
        public List<string> KeyTakeaways { get; set; } = new();
        public bool BeatEstimates { get; set; }
        public double SurpriseMagnitude { get; set; }
        public FinancialMetrics FinancialMetrics { get; set; } = new();
        public SentimentAnalysis SentimentAnalysis { get; set; } = new();
        public List<string> StrategicInsights { get; set; } = new();
        public Dictionary<string, object> RiskIndicators { get; set; } = new();
        public Dictionary<string, object> CompetitivePositioning { get; set; } = new();
    }

    public class SupplyChainAnalysis
    {
        public SupplyChainData Data { get; set; } = new();
        public SupplyChainData SupplyChainData { get; set; } = new(); // Alias for Data
        public DateTime AnalysisDate { get; set; }
        public Dictionary<string, object> Insights { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public double RiskScore { get; set; }
        public double ResilienceScore { get; set; }
        public SupplyChainRisk RiskAssessment { get; set; } = new();
        public Dictionary<string, object> DiversificationMetrics { get; set; } = new();
        public Dictionary<string, object> GeographicExposure { get; set; } = new();
        public Dictionary<string, object> ConcentrationRisks { get; set; } = new();
    }
}