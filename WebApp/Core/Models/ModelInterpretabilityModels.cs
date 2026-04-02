namespace FeenQR.Core.Models;

public class ShapAnalysisRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public int TopFeatures { get; set; } = 10;
}

public class PartialDependenceRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public int GridSize { get; set; } = 20;
}

public class FeatureInteractionRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public int TopFeaturePairs { get; set; } = 5;
}

public class ExplainPredictionRequest
{
    public string ModelName { get; set; } = string.Empty;
    public Dictionary<string, decimal> InputData { get; set; } = new();
    public decimal PredictionValue { get; set; }
    public string InputDescription { get; set; } = string.Empty;
}

public class PermutationImportanceRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public int NumberOfRepeats { get; set; } = 10;
}

public class ModelFairnessRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public string SensitiveAttribute { get; set; } = string.Empty;
}

public class InterpretabilityReportRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public bool IncludeFairness { get; set; } = true;
}

public class ShapAnalysisResult
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public Dictionary<string, decimal> ShapValues { get; set; } = new();
    public decimal BaselineValue { get; set; }
    public decimal PredictionValue { get; set; }
    public Dictionary<string, FeatureContribution> FeatureContributions { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
    public decimal ModelAccuracy { get; set; }
}

public class FeatureContribution
{
    public string Feature { get; set; } = string.Empty;
    public decimal ShapValue { get; set; }
    public string Impact { get; set; } = string.Empty;
}

public class PartialDependenceResult
{
    public string ModelName { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public List<PartialDependencePoint> DependencePoints { get; set; } = new();
    public decimal FeatureMin { get; set; }
    public decimal FeatureMax { get; set; }
    public string CurveShape { get; set; } = string.Empty;
    public string MonotonicTrend { get; set; } = string.Empty;
    public decimal ComputationTime { get; set; }
}

public class PartialDependencePoint
{
    public decimal FeatureValue { get; set; }
    public decimal PredictionValue { get; set; }
    public int SampleCount { get; set; }
}

public class FeatureInteractionResult
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public List<FeatureInteractionPair> FeatureInteractions { get; set; } = new();
    public int TotalInteractionPairsAnalyzed { get; set; }
    public decimal AverageInteractionStrength { get; set; }
    public List<string> StrongestInteractions { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
}

public class FeatureInteractionPair
{
    public string Feature1 { get; set; } = string.Empty;
    public string Feature2 { get; set; } = string.Empty;
    public decimal InteractionStrength { get; set; }
    public decimal Hvalue { get; set; }
}

public class PredictionExplanationResult
{
    public string ModelName { get; set; } = string.Empty;
    public decimal PredictionValue { get; set; }
    public decimal BaselineValue { get; set; }
    public Dictionary<string, decimal> FeatureContributions { get; set; } = new();
    public List<string> FeatureOrder { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public decimal DecisionBoundary { get; set; }
    public decimal DistanceToBoundary { get; set; }
}

public class PermutationImportanceResult
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public int NumberOfRepeats { get; set; }
    public Dictionary<string, FeaturePermutationScore> FeatureImportances { get; set; } = new();
    public int TotalFeatureCount { get; set; }
    public decimal BaselineAccuracy { get; set; }
    public Dictionary<string, decimal> RelativeImportances { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
}

public class FeaturePermutationScore
{
    public string Feature { get; set; } = string.Empty;
    public decimal MeanDecrease { get; set; }
    public decimal StdDeviation { get; set; }
}

public class ModelFairnessResult
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public string SensitiveAttribute { get; set; } = string.Empty;
    public Dictionary<string, FairnessMetrics> GroupMetrics { get; set; } = new();
    public decimal AccuracyDisparity { get; set; }
    public decimal DisparateImpactRatio { get; set; }
    public bool IsFair { get; set; }
    public decimal FairnessScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
}

public class FairnessMetrics
{
    public string GroupName { get; set; } = string.Empty;
    public decimal Accuracy { get; set; }
    public decimal FalsePositiveRate { get; set; }
    public decimal FalseNegativeRate { get; set; }
}

public class InterpretabilityReportResult
{
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSymbol { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; }
    public ShapAnalysisResult? ShapAnalysis { get; set; }
    public PermutationImportanceResult? PermutationAnalysis { get; set; }
    public FeatureInteractionResult? InteractionAnalysis { get; set; }
    public ModelFairnessResult? FairnessAnalysis { get; set; }
    public string ExecutiveSummary { get; set; } = string.Empty;
    public string[] KeyInsights { get; set; } = Array.Empty<string>();
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
}
