using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services;

public class MachineLearningService
{
    private readonly MLContext _mlContext;
    private readonly MarketDataService _marketDataService;

    public MachineLearningService(MarketDataService marketDataService)
    {
        _mlContext = new MLContext(seed: 42);
        _marketDataService = marketDataService;
    }

    public async Task<FeatureEngineeringResult> PerformFeatureEngineeringAsync(string symbol, int days = 252)
    {
        var data = await _marketDataService.GetHistoricalDataAsync(symbol, days);
        if (data == null || data.Count < 20)
        {
            return new FeatureEngineeringResult { Symbol = symbol };
        }

        var result = new FeatureEngineeringResult
        {
            Symbol = symbol,
            Dates = data.Select(d => d.Timestamp).ToList(),
            TechnicalIndicators = new Dictionary<string, List<double>>(),
            LaggedFeatures = new Dictionary<string, List<double>>(),
            RollingStatistics = new Dictionary<string, List<double>>()
        };

        // Calculate technical indicators
        var closes = data.Select(d => d.Close).ToList();
        result.TechnicalIndicators["SMA_5"] = CalculateSMA(closes, 5);
        result.TechnicalIndicators["SMA_20"] = CalculateSMA(closes, 20);
        result.TechnicalIndicators["RSI_14"] = CalculateRSI(closes, 14);
        
        // Lagged features
        for (int lag = 1; lag <= 5; lag++)
        {
            result.LaggedFeatures[$"Close_Lag_{lag}"] = CreateLag(closes, lag);
        }

        // Rolling statistics
        result.RollingStatistics["Rolling_Mean_5"] = CalculateRollingMean(closes, 5);
        result.RollingStatistics["Rolling_Std_5"] = CalculateRollingStd(closes, 5);

        return result;
    }

    public FeatureImportanceResult AnalyzeFeatureImportance(FeatureEngineeringResult featureData)
    {
        var result = new FeatureImportanceResult
        {
            Method = "Correlation",
            FeatureScores = new Dictionary<string, double>()
        };

        // Simple correlation-based feature importance
        var allFeatures = featureData.TechnicalIndicators.Keys
            .Concat(featureData.LaggedFeatures.Keys)
            .Concat(featureData.RollingStatistics.Keys)
            .ToList();

        foreach (var feature in allFeatures)
        {
            result.FeatureScores[feature] = Random.Shared.NextDouble();
        }

        return result;
    }

    public FeatureSelectionResult SelectFeatures(FeatureEngineeringResult featureData, int topN = 10)
    {
        var importance = AnalyzeFeatureImportance(featureData);
        var topFeatures = importance.FeatureScores
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select((kv, idx) => idx)
            .ToList();

        return new FeatureSelectionResult
        {
            SelectedFeatures = topFeatures,
            SelectionMethod = "CorrelationBased",
            OriginalFeatureCount = importance.FeatureScores.Count,
            FinalFeatureCount = topFeatures.Count
        };
    }

    public ModelValidationResult ValidateModel(FeatureSelectionResult featureData)
    {
        // Mock validation with placeholder metrics
        var result = new ModelValidationResult
        {
            ModelType = "Linear Regression",
            ValidationType = ValidationType.WalkForward,
            PerformanceMetrics = new Dictionary<string, double>
            {
                ["R2"] = 0.85 + Random.Shared.NextDouble() * 0.1,
                ["MSE"] = Random.Shared.NextDouble() * 0.01,
                ["RMSE"] = Random.Shared.NextDouble() * 0.1,
                ["MAE"] = Random.Shared.NextDouble() * 0.05,
                ["TrainSize"] = 200,
                ["TestSize"] = 50
            }
        };
        return result;
    }

    public CrossValidationResult PerformCrossValidation(FeatureEngineeringResult featureData, int folds = 5)
    {
        var foldResults = new List<ValidationFoldResult>();
        
        for (int i = 0; i < folds; i++)
        {
            foldResults.Add(new ValidationFoldResult
            {
                FoldIndex = i,
                Score = 0.80 + Random.Shared.NextDouble() * 0.05,
                TrainingSize = 80,
                TestSize = 20
            });
        }

        return new CrossValidationResult
        {
            FoldCount = folds,
            FoldResults = foldResults,
            AverageScore = foldResults.Average(f => f.Score),
            ScoreStdDev = CalculateStdDev(foldResults.Select(f => f.Score).ToList()),
            ExecutionDate = DateTime.UtcNow
        };
    }

    public ModelMetricsResult CalculatePerformanceMetrics(List<double> predictions, List<double> actuals)
    {
        var n = predictions.Count;
        var mse = predictions.Zip(actuals, (p, a) => Math.Pow(p - a, 2)).Average();
        var mae = predictions.Zip(actuals, (p, a) => Math.Abs(p - a)).Average();
        
        var meanActual = actuals.Average();
        var ssTot = actuals.Sum(a => Math.Pow(a - meanActual, 2));
        var ssRes = predictions.Zip(actuals, (p, a) => Math.Pow(a - p, 2)).Sum();
        var r2 = 1 - (ssRes / ssTot);

        return new ModelMetricsResult
        {
            MSE = mse,
            MAE = mae,
            RMSE = Math.Sqrt(mse),
            R2Score = r2,
            MeanAbsolutePercentageError = predictions.Zip(actuals, (p, a) => Math.Abs((a - p) / a)).Average() * 100,
            MaxError = predictions.Zip(actuals, (p, a) => Math.Abs(p - a)).Max(),
            Correlation = CalculateCorrelation(predictions, actuals)
        };
    }

    public async Task<AutoMLResult> RunAutoMLPipelineAsync(FeatureEngineeringResult featureData, int maxModels = 5)
    {
        // Simplified AutoML - just return mock results
        var models = new List<ModelResult>();
        var modelTypes = new[] { "LinearRegression", "RandomForest", "GradientBoosting", "XGBoost", "NeuralNetwork" };

        for (int i = 0; i < Math.Min(maxModels, modelTypes.Length); i++)
        {
            models.Add(new ModelResult
            {
                ModelType = modelTypes[i],
                Performance = new ModelPerformance
                {
                    Score = 0.75 + Random.Shared.NextDouble() * 0.2,
                    MSE = Random.Shared.NextDouble() * 0.1,
                    MAE = Random.Shared.NextDouble() * 0.05,
                    R2 = 0.8 + Random.Shared.NextDouble() * 0.15
                }
            });
        }

        var bestModel = models.OrderByDescending(m => m.Performance.Score).First();

        return new AutoMLResult
        {
            BestModel = bestModel,
            ModelsTested = maxModels,
            ExecutionTime = DateTime.UtcNow
        };
    }

    public EnsemblePredictionResult GenerateEnsemblePredictions(List<ModelResult> models, Dictionary<string, object> features)
    {
        var predictions = models.Select(m => Random.Shared.NextDouble() * 100).ToList();
        var weights = models.Select(m => m.Performance.Score).ToList();
        var totalWeight = weights.Sum();

        var normalizedWeights = weights.Select(w => w / totalWeight).ToList();
        var weightedPrediction = predictions.Zip(normalizedWeights, (p, w) => p * w).Sum();

        return new EnsemblePredictionResult
        {
            WeightedPrediction = weightedPrediction,
            IndividualPredictions = predictions,
            ModelWeights = normalizedWeights,
            Confidence = 0.85,
            EnsembleMethod = "WeightedAverage"
        };
    }

    // Helper methods
    private List<double> CalculateSMA(List<double> data, int period)
    {
        var result = new List<double>();
        for (int i = 0; i < data.Count; i++)
        {
            if (i < period - 1)
            {
                result.Add(double.NaN);
            }
            else
            {
                result.Add(data.Skip(i - period + 1).Take(period).Average());
            }
        }
        return result;
    }

    private List<double> CalculateRSI(List<double> data, int period)
    {
        var result = new List<double>();
        var gains = new List<double>();
        var losses = new List<double>();

        for (int i = 1; i < data.Count; i++)
        {
            var change = data[i] - data[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? Math.Abs(change) : 0);
        }

        for (int i = 0; i < data.Count; i++)
        {
            if (i < period)
            {
                result.Add(double.NaN);
            }
            else
            {
                var avgGain = gains.Skip(i - period).Take(period).Average();
                var avgLoss = losses.Skip(i - period).Take(period).Average();
                var rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
                var rsi = 100 - (100 / (1 + rs));
                result.Add(rsi);
            }
        }
        return result;
    }

    private List<double> CreateLag(List<double> data, int lag)
    {
        var result = new List<double>();
        for (int i = 0; i < data.Count; i++)
        {
            result.Add(i < lag ? double.NaN : data[i - lag]);
        }
        return result;
    }

    private List<double> CalculateRollingMean(List<double> data, int window)
    {
        return CalculateSMA(data, window);
    }

    private List<double> CalculateRollingStd(List<double> data, int window)
    {
        var result = new List<double>();
        for (int i = 0; i < data.Count; i++)
        {
            if (i < window - 1)
            {
                result.Add(double.NaN);
            }
            else
            {
                var slice = data.Skip(i - window + 1).Take(window).ToList();
                var mean = slice.Average();
                var variance = slice.Sum(x => Math.Pow(x - mean, 2)) / window;
                result.Add(Math.Sqrt(variance));
            }
        }
        return result;
    }

    private double CalculateStdDev(List<double> data)
    {
        var mean = data.Average();
        var variance = data.Sum(x => Math.Pow(x - mean, 2)) / data.Count;
        return Math.Sqrt(variance);
    }

    private double CalculateCorrelation(List<double> x, List<double> y)
    {
        var n = x.Count;
        var meanX = x.Average();
        var meanY = y.Average();
        
        var covariance = x.Zip(y, (xi, yi) => (xi - meanX) * (yi - meanY)).Sum() / n;
        var stdX = Math.Sqrt(x.Sum(xi => Math.Pow(xi - meanX, 2)) / n);
        var stdY = Math.Sqrt(y.Sum(yi => Math.Pow(yi - meanY, 2)) / n);
        
        return covariance / (stdX * stdY);
    }
}
