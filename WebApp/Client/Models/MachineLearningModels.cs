namespace Client.Models;

public class FeatureEngineeringResult
{
    public int FeatureCount { get; set; }
    public int SampleCount { get; set; }
    public List<string> FeatureNames { get; set; } = new();
}

public class FeatureImportanceResult
{
    public Dictionary<string, double> FeatureImportances { get; set; } = new();
}

public class FeatureSelectionResult
{
    public int OriginalFeatureCount { get; set; }
    public int FinalFeatureCount { get; set; }
    public List<int> SelectedFeatures { get; set; } = new();
}

public class ModelValidationResult
{
    public string ModelType { get; set; } = string.Empty;
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
}

public class CrossValidationResult
{
    public int FoldCount { get; set; }
    public List<ValidationFoldResult> FoldResults { get; set; } = new();
    public double AverageScore { get; set; }
    public double ScoreStdDev { get; set; }
    public DateTime ExecutionDate { get; set; }
}

public class ValidationFoldResult
{
    public int FoldIndex { get; set; }
    public double Score { get; set; }
    public int TrainingSize { get; set; }
    public int TestSize { get; set; }
}

public class AutoMLResult
{
    public string TargetType { get; set; } = string.Empty;
    public int ModelsTested { get; set; }
    public ModelResult BestModel { get; set; } = new();
    public List<ModelResult> AllModelResults { get; set; } = new();
    public DateTime ExecutionTime { get; set; }
}

public class ModelResult
{
    public string ModelType { get; set; } = string.Empty;
    public ModelPerformance Performance { get; set; } = new();
}

public class ModelPerformance
{
    public double Score { get; set; }
    public double MSE { get; set; }
    public double MAE { get; set; }
    public double R2 { get; set; }
}

public class ModelComparison
{
    public string ModelName { get; set; } = string.Empty;
    public double ValidationScore { get; set; }
    public double TrainingTime { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
}

public class ModelSelectionResult
{
    public string Symbol { get; set; } = string.Empty;
    public List<ModelComparison> ModelComparisons { get; set; } = new();
    public string BestModel { get; set; } = string.Empty;
    public double BestScore { get; set; }
    public string SelectionCriteria { get; set; } = string.Empty;
}

public class EnsemblePredictionResult
{
    public double Prediction { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, double> ModelContributions { get; set; } = new();
}

public class HyperparameterOptimizationResult
{
    public string Symbol { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public Dictionary<string, object> BestParameters { get; set; } = new();
    public double BestScore { get; set; }
    public List<OptimizationTrial> Trials { get; set; } = new();
    public string OptimizationMethod { get; set; } = string.Empty;
}

public class OptimizationTrial
{
    public int TrialNumber { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public double Score { get; set; }
    public double Duration { get; set; }
}

public class ModelMetricsResult
{
    public double R2 { get; set; }
    public double MSE { get; set; }
    public double RMSE { get; set; }
    public double MAE { get; set; }
    public double MAPE { get; set; }
}
