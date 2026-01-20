namespace QuantResearchAgent.Core;

public class FeatureEngineeringResult
{
    public string Symbol { get; set; } = string.Empty;
    public List<Dictionary<string, object>> Features { get; set; } = new();
    public int FeatureCount { get; set; }
    public int SampleCount { get; set; }
    public List<string> FeatureNames { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class FeatureImportanceResult
{
    public string Symbol { get; set; } = string.Empty;
    public Dictionary<string, double> FeatureImportances { get; set; } = new();
    public List<string> TopFeatures { get; set; } = new();
    public string ImportanceMethod { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class FeatureSelectionResult
{
    public string Symbol { get; set; } = string.Empty;
    public List<string> SelectedFeatures { get; set; } = new();
    public List<Dictionary<string, object>> FilteredData { get; set; } = new();
    public string SelectionMethod { get; set; } = string.Empty;
    public int OriginalFeatureCount { get; set; }
    public int SelectedFeatureCount { get; set; }
    public DateTime SelectedAt { get; set; } = DateTime.UtcNow;
}

public class ModelValidationResult
{
    public string Symbol { get; set; } = string.Empty;
    public int TrainSize { get; set; }
    public int TestSize { get; set; }
    public double MSE { get; set; }
    public double MAE { get; set; }
    public double R2Score { get; set; }
    public double RMSE { get; set; }
    public string ValidationMethod { get; set; } = string.Empty;
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

public class CrossValidationResult
{
    public string Symbol { get; set; } = string.Empty;
    public int Folds { get; set; }
    public List<ModelValidationResult> FoldResults { get; set; } = new();
    public double MeanMSE { get; set; }
    public double MeanMAE { get; set; }
    public double MeanR2 { get; set; }
    public double StdMSE { get; set; }
    public double StdMAE { get; set; }
    public double StdR2 { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class ModelMetricsResult
{
    public double MSE { get; set; }
    public double MAE { get; set; }
    public double RMSE { get; set; }
    public double R2Score { get; set; }
    public double MeanAbsolutePercentageError { get; set; }
    public double MaxError { get; set; }
    public double Correlation { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class AutoMLResult
{
    public string Symbol { get; set; } = string.Empty;
    public string BestModel { get; set; } = string.Empty;
    public double BestScore { get; set; }
    public int ExperimentTime { get; set; }
    public int TrialsRun { get; set; }
    public List<ModelResult> TopModels { get; set; } = new();
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class ModelResult
{
    public string ModelName { get; set; } = string.Empty;
    public double Score { get; set; }
    public double TrainingTime { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class EnsemblePredictionResult
{
    public double WeightedPrediction { get; set; }
    public List<double> IndividualPredictions { get; set; } = new();
    public List<double> ModelWeights { get; set; } = new();
    public double Confidence { get; set; }
    public string EnsembleMethod { get; set; } = string.Empty;
    public DateTime PredictedAt { get; set; } = DateTime.UtcNow;
}

public class HyperparameterOptimizationResult
{
    public string Symbol { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public Dictionary<string, object> BestParameters { get; set; } = new();
    public double BestScore { get; set; }
    public List<OptimizationTrial> Trials { get; set; } = new();
    public string OptimizationMethod { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class OptimizationTrial
{
    public int TrialNumber { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public double Score { get; set; }
    public double Duration { get; set; }
}

public class ModelSelectionResult
{
    public string Symbol { get; set; } = string.Empty;
    public List<ModelComparison> ModelComparisons { get; set; } = new();
    public string BestModel { get; set; } = string.Empty;
    public double BestScore { get; set; }
    public string SelectionCriteria { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class ModelComparison
{
    public string ModelName { get; set; } = string.Empty;
    public double TrainScore { get; set; }
    public double ValidationScore { get; set; }
    public double TestScore { get; set; }
    public double TrainingTime { get; set; }
    public double PredictionTime { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
}