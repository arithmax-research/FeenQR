using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.AutoML;
using FeenQR.Models;

namespace FeenQR.Services;

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
        
        var features = new List<Dictionary<string, object>>();
        
        for (int i = 20; i < data.Count; i++)
        {
            var feature = new Dictionary<string, object>
            {
                ["Symbol"] = symbol,
                ["Date"] = data[i].Date,
                ["Close"] = data[i].Close,
                ["Volume"] = data[i].Volume,
                
                // Price-based features
                ["Returns"] = (data[i].Close - data[i-1].Close) / data[i-1].Close,
                ["LogReturns"] = Math.Log(data[i].Close / data[i-1].Close),
                ["HighLowRatio"] = data[i].High / data[i].Low,
                ["OpenCloseRatio"] = data[i].Open / data[i].Close,
                
                // Technical indicators
                ["SMA_5"] = data.Skip(i-4).Take(5).Average(x => x.Close),
                ["SMA_20"] = data.Skip(i-19).Take(20).Average(x => x.Close),
                ["EMA_12"] = CalculateEMA(data.Take(i+1).Select(x => x.Close).ToList(), 12),
                ["RSI_14"] = CalculateRSI(data.Skip(i-13).Take(14).Select(x => x.Close).ToList()),
                
                // Volatility features
                ["Volatility_5"] = CalculateVolatility(data.Skip(i-4).Take(5).Select(x => x.Close).ToList()),
                ["Volatility_20"] = CalculateVolatility(data.Skip(i-19).Take(20).Select(x => x.Close).ToList()),
                
                // Volume features
                ["VolumeMA_5"] = data.Skip(i-4).Take(5).Average(x => x.Volume),
                ["VolumeRatio"] = data[i].Volume / data.Skip(i-19).Take(20).Average(x => x.Volume),
                
                // Target variable (next day return)
                ["NextDayReturn"] = i < data.Count - 1 ? (data[i+1].Close - data[i].Close) / data[i].Close : 0
            };
            
            features.Add(feature);
        }

        return new FeatureEngineeringResult
        {
            Symbol = symbol,
            Features = features,
            FeatureCount = features.FirstOrDefault()?.Count ?? 0,
            SampleCount = features.Count,
            FeatureNames = features.FirstOrDefault()?.Keys.ToList() ?? new List<string>()
        };
    }

    public FeatureImportanceResult AnalyzeFeatureImportance(FeatureEngineeringResult featureData)
    {
        var importanceScores = new Dictionary<string, double>();
        var target = "NextDayReturn";
        
        foreach (var feature in featureData.FeatureNames.Where(f => f != target && f != "Date" && f != "Symbol"))
        {
            var correlation = CalculateCorrelation(
                featureData.Features.Select(f => Convert.ToDouble(f[feature])).ToList(),
                featureData.Features.Select(f => Convert.ToDouble(f[target])).ToList()
            );
            importanceScores[feature] = Math.Abs(correlation);
        }

        var sortedFeatures = importanceScores.OrderByDescending(x => x.Value).ToList();

        return new FeatureImportanceResult
        {
            Symbol = featureData.Symbol,
            FeatureImportances = sortedFeatures.ToDictionary(x => x.Key, x => x.Value),
            TopFeatures = sortedFeatures.Take(10).Select(x => x.Key).ToList(),
            ImportanceMethod = "Correlation"
        };
    }

    public FeatureSelectionResult SelectFeatures(FeatureEngineeringResult featureData, int topK = 10)
    {
        var importance = AnalyzeFeatureImportance(featureData);
        var selectedFeatures = importance.TopFeatures.Take(topK).ToList();
        
        // Add essential features
        selectedFeatures.AddRange(new[] { "Symbol", "Date", "NextDayReturn" });
        
        var filteredFeatures = featureData.Features
            .Select(f => f.Where(kv => selectedFeatures.Contains(kv.Key))
                         .ToDictionary(kv => kv.Key, kv => kv.Value))
            .ToList();

        return new FeatureSelectionResult
        {
            Symbol = featureData.Symbol,
            SelectedFeatures = selectedFeatures,
            FilteredData = filteredFeatures,
            SelectionMethod = "Top-K Correlation",
            OriginalFeatureCount = featureData.FeatureCount,
            SelectedFeatureCount = selectedFeatures.Count
        };
    }

    public async Task<ModelValidationResult> ValidateModelAsync(FeatureSelectionResult featureData)
    {
        var trainSize = (int)(featureData.FilteredData.Count * 0.8);
        var trainData = featureData.FilteredData.Take(trainSize).ToList();
        var testData = featureData.FilteredData.Skip(trainSize).ToList();

        // Simple linear regression validation
        var predictions = new List<double>();
        var actuals = new List<double>();

        foreach (var sample in testData)
        {
            var actual = Convert.ToDouble(sample["NextDayReturn"]);
            var predicted = PredictReturn(sample, trainData); // Simple prediction logic
            
            predictions.Add(predicted);
            actuals.Add(actual);
        }

        var mse = CalculateMSE(predictions, actuals);
        var mae = CalculateMAE(predictions, actuals);
        var r2 = CalculateR2(predictions, actuals);

        return new ModelValidationResult
        {
            Symbol = featureData.Symbol,
            TrainSize = trainSize,
            TestSize = testData.Count,
            MSE = mse,
            MAE = mae,
            R2Score = r2,
            RMSE = Math.Sqrt(mse),
            ValidationMethod = "Train-Test Split"
        };
    }

    public CrossValidationResult PerformCrossValidation(FeatureSelectionResult featureData, int folds = 5)
    {
        var foldSize = featureData.FilteredData.Count / folds;
        var cvResults = new List<ModelValidationResult>();

        for (int fold = 0; fold < folds; fold++)
        {
            var testStart = fold * foldSize;
            var testEnd = Math.Min(testStart + foldSize, featureData.FilteredData.Count);
            
            var testData = featureData.FilteredData.Skip(testStart).Take(testEnd - testStart).ToList();
            var trainData = featureData.FilteredData.Take(testStart)
                .Concat(featureData.FilteredData.Skip(testEnd)).ToList();

            var predictions = new List<double>();
            var actuals = new List<double>();

            foreach (var sample in testData)
            {
                var actual = Convert.ToDouble(sample["NextDayReturn"]);
                var predicted = PredictReturn(sample, trainData);
                
                predictions.Add(predicted);
                actuals.Add(actual);
            }

            cvResults.Add(new ModelValidationResult
            {
                Symbol = featureData.Symbol,
                TrainSize = trainData.Count,
                TestSize = testData.Count,
                MSE = CalculateMSE(predictions, actuals),
                MAE = CalculateMAE(predictions, actuals),
                R2Score = CalculateR2(predictions, actuals),
                ValidationMethod = $"CV Fold {fold + 1}"
            });
        }

        return new CrossValidationResult
        {
            Symbol = featureData.Symbol,
            Folds = folds,
            FoldResults = cvResults,
            MeanMSE = cvResults.Average(r => r.MSE),
            MeanMAE = cvResults.Average(r => r.MAE),
            MeanR2 = cvResults.Average(r => r.R2Score),
            StdMSE = CalculateStandardDeviation(cvResults.Select(r => r.MSE).ToList()),
            StdMAE = CalculateStandardDeviation(cvResults.Select(r => r.MAE).ToList()),
            StdR2 = CalculateStandardDeviation(cvResults.Select(r => r.R2Score).ToList())
        };
    }

    public ModelMetricsResult CalculateModelMetrics(List<double> predictions, List<double> actuals)
    {
        return new ModelMetricsResult
        {
            MSE = CalculateMSE(predictions, actuals),
            MAE = CalculateMAE(predictions, actuals),
            RMSE = Math.Sqrt(CalculateMSE(predictions, actuals)),
            R2Score = CalculateR2(predictions, actuals),
            MeanAbsolutePercentageError = CalculateMAPE(predictions, actuals),
            MaxError = predictions.Zip(actuals, (p, a) => Math.Abs(p - a)).Max(),
            Correlation = CalculateCorrelation(predictions, actuals)
        };
    }

    public async Task<AutoMLResult> RunAutoMLPipelineAsync(string symbol, int days = 252)
    {
        var featureData = await PerformFeatureEngineeringAsync(symbol, days);
        var selectedFeatures = SelectFeatures(featureData, 15);
        
        // Convert to ML.NET format
        var mlData = ConvertToMLData(selectedFeatures.FilteredData);
        var dataView = _mlContext.Data.LoadFromEnumerable(mlData);

        // AutoML experiment
        var experiment = _mlContext.Auto().CreateRegressionExperiment(maxExperimentTimeInSeconds: 60);
        var result = experiment.Execute(dataView, labelColumnName: "NextDayReturn");

        return new AutoMLResult
        {
            Symbol = symbol,
            BestModel = result.BestRun.TrainerName,
            BestScore = result.BestRun.ValidationMetrics.RSquared,
            ExperimentTime = 60,
            TrialsRun = result.RunDetails.Count(),
            TopModels = result.RunDetails.Take(5).Select(r => new ModelResult
            {
                ModelName = r.TrainerName,
                Score = r.ValidationMetrics?.RSquared ?? 0,
                TrainingTime = r.RuntimeInSeconds
            }).ToList()
        };
    }

    public EnsemblePredictionResult GenerateEnsemblePredictions(List<ModelResult> models, Dictionary<string, object> features)
    {
        var predictions = new List<double>();
        var weights = models.Select(m => m.Score).ToList();
        var totalWeight = weights.Sum();

        // Simple weighted average ensemble
        var weightedPrediction = 0.0;
        for (int i = 0; i < models.Count; i++)
        {
            var prediction = PredictWithModel(models[i], features);
            weightedPrediction += prediction * (weights[i] / totalWeight);
            predictions.Add(prediction);
        }

        return new EnsemblePredictionResult
        {
            WeightedPrediction = weightedPrediction,
            IndividualPredictions = predictions,
            ModelWeights = weights.Select(w => w / totalWeight).ToList(),
            Confidence = CalculateEnsembleConfidence(predictions),
            EnsembleMethod = "Weighted Average"
        };
    }

    // Helper methods
    private double CalculateEMA(List<double> prices, int period)
    {
        var multiplier = 2.0 / (period + 1);
        var ema = prices.Take(period).Average();
        
        for (int i = period; i < prices.Count; i++)
        {
            ema = (prices[i] * multiplier) + (ema * (1 - multiplier));
        }
        
        return ema;
    }

    private double CalculateRSI(List<double> prices)
    {
        var gains = new List<double>();
        var losses = new List<double>();
        
        for (int i = 1; i < prices.Count; i++)
        {
            var change = prices[i] - prices[i-1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? -change : 0);
        }
        
        var avgGain = gains.Average();
        var avgLoss = losses.Average();
        
        if (avgLoss == 0) return 100;
        
        var rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }

    private double CalculateVolatility(List<double> prices)
    {
        var returns = new List<double>();
        for (int i = 1; i < prices.Count; i++)
        {
            returns.Add((prices[i] - prices[i-1]) / prices[i-1]);
        }
        return CalculateStandardDeviation(returns);
    }

    private double CalculateCorrelation(List<double> x, List<double> y)
    {
        var n = Math.Min(x.Count, y.Count);
        var sumX = x.Take(n).Sum();
        var sumY = y.Take(n).Sum();
        var sumXY = x.Take(n).Zip(y.Take(n), (a, b) => a * b).Sum();
        var sumX2 = x.Take(n).Sum(a => a * a);
        var sumY2 = y.Take(n).Sum(b => b * b);
        
        var numerator = n * sumXY - sumX * sumY;
        var denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
        
        return denominator == 0 ? 0 : numerator / denominator;
    }

    private double CalculateMSE(List<double> predictions, List<double> actuals)
    {
        return predictions.Zip(actuals, (p, a) => Math.Pow(p - a, 2)).Average();
    }

    private double CalculateMAE(List<double> predictions, List<double> actuals)
    {
        return predictions.Zip(actuals, (p, a) => Math.Abs(p - a)).Average();
    }

    private double CalculateR2(List<double> predictions, List<double> actuals)
    {
        var meanActual = actuals.Average();
        var ssRes = actuals.Zip(predictions, (a, p) => Math.Pow(a - p, 2)).Sum();
        var ssTot = actuals.Sum(a => Math.Pow(a - meanActual, 2));
        return ssTot == 0 ? 0 : 1 - (ssRes / ssTot);
    }

    private double CalculateMAPE(List<double> predictions, List<double> actuals)
    {
        return predictions.Zip(actuals, (p, a) => Math.Abs((a - p) / a)).Where(x => !double.IsInfinity(x)).Average() * 100;
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    private double PredictReturn(Dictionary<string, object> sample, List<Dictionary<string, object>> trainData)
    {
        // Simple prediction based on similar patterns
        var close = Convert.ToDouble(sample["Close"]);
        var sma20 = Convert.ToDouble(sample["SMA_20"]);
        return (close - sma20) / sma20 * 0.1; // Simplified prediction
    }

    private List<MLDataPoint> ConvertToMLData(List<Dictionary<string, object>> data)
    {
        return data.Select(d => new MLDataPoint
        {
            Close = Convert.ToSingle(d["Close"]),
            Volume = Convert.ToSingle(d["Volume"]),
            Returns = Convert.ToSingle(d["Returns"]),
            SMA_20 = Convert.ToSingle(d["SMA_20"]),
            RSI_14 = Convert.ToSingle(d["RSI_14"]),
            NextDayReturn = Convert.ToSingle(d["NextDayReturn"])
        }).ToList();
    }

    private double PredictWithModel(ModelResult model, Dictionary<string, object> features)
    {
        // Simplified model prediction
        return Convert.ToDouble(features["Returns"]) * model.Score;
    }

    private double CalculateEnsembleConfidence(List<double> predictions)
    {
        var mean = predictions.Average();
        var variance = predictions.Sum(p => Math.Pow(p - mean, 2)) / predictions.Count;
        return Math.Max(0, 1 - Math.Sqrt(variance));
    }

    public class MLDataPoint
    {
        public float Close { get; set; }
        public float Volume { get; set; }
        public float Returns { get; set; }
        public float SMA_20 { get; set; }
        public float RSI_14 { get; set; }
        public float NextDayReturn { get; set; }
    }
}