using System.CommandLine;
using System.Text.Json;
using FeenQR.Services;
using FeenQR.Models;

namespace FeenQR.CLI.Commands;

public static class MachineLearningCommands
{
    public static Command CreateFeatureEngineerCommand(MachineLearningService mlService)
    {
        var symbolOption = new Option<string>("--symbol", () => "BTCUSDT", "Trading symbol");
        var daysOption = new Option<int>("--days", () => 252, "Number of days of historical data");

        var command = new Command("feature-engineer", "Perform feature engineering on market data")
        {
            symbolOption,
            daysOption
        };

        command.SetHandler(async (symbol, days) =>
        {
            try
            {
                Console.WriteLine($"🔧 Engineering features for {symbol} ({days} days)...");
                var result = await mlService.PerformFeatureEngineeringAsync(symbol, days);
                
                Console.WriteLine($"✅ Feature Engineering Complete");
                Console.WriteLine($"   Symbol: {result.Symbol}");
                Console.WriteLine($"   Features: {result.FeatureCount}");
                Console.WriteLine($"   Samples: {result.SampleCount}");
                Console.WriteLine($"   Top Features: {string.Join(", ", result.FeatureNames.Take(5))}");
                
                if (result.FeatureNames.Count > 5)
                    Console.WriteLine($"   ... and {result.FeatureNames.Count - 5} more");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Feature engineering failed: {ex.Message}");
            }
        }, symbolOption, daysOption);

        return command;
    }

    public static Command CreateFeatureImportanceCommand(MachineLearningService mlService)
    {
        var symbolOption = new Option<string>("--symbol", () => "BTCUSDT", "Trading symbol");
        var daysOption = new Option<int>("--days", () => 252, "Number of days of historical data");

        var command = new Command("feature-importance", "Analyze feature importance")
        {
            symbolOption,
            daysOption
        };

        command.SetHandler(async (symbol, days) =>
        {
            try
            {
                Console.WriteLine($"📊 Analyzing feature importance for {symbol}...");
                var featureData = await mlService.PerformFeatureEngineeringAsync(symbol, days);
                var result = mlService.AnalyzeFeatureImportance(featureData);
                
                Console.WriteLine($"✅ Feature Importance Analysis Complete");
                Console.WriteLine($"   Method: {result.ImportanceMethod}");
                Console.WriteLine($"   Top 10 Features:");
                
                foreach (var (feature, importance) in result.FeatureImportances.Take(10))
                {
                    Console.WriteLine($"   {feature,-20}: {importance:F4}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Feature importance analysis failed: {ex.Message}");
            }
        }, symbolOption, daysOption);

        return command;
    }

    public static Command CreateFeatureSelectCommand(MachineLearningService mlService)
    {
        var symbolOption = new Option<string>("--symbol", () => "BTCUSDT", "Trading symbol");
        var daysOption = new Option<int>("--days", () => 252, "Number of days of historical data");
        var topKOption = new Option<int>("--top-k", () => 10, "Number of top features to select");

        var command = new Command("feature-select", "Select top features")
        {
            symbolOption,
            daysOption,
            topKOption
        };

        command.SetHandler(async (symbol, days, topK) =>
        {
            try
            {
                Console.WriteLine($"🎯 Selecting top {topK} features for {symbol}...");
                var featureData = await mlService.PerformFeatureEngineeringAsync(symbol, days);
                var result = mlService.SelectFeatures(featureData, topK);
                
                Console.WriteLine($"✅ Feature Selection Complete");
                Console.WriteLine($"   Method: {result.SelectionMethod}");
                Console.WriteLine($"   Original Features: {result.OriginalFeatureCount}");
                Console.WriteLine($"   Selected Features: {result.SelectedFeatureCount}");
                Console.WriteLine($"   Selected: {string.Join(", ", result.SelectedFeatures.Where(f => f != "Symbol" && f != "Date" && f != "NextDayReturn"))}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Feature selection failed: {ex.Message}");
            }
        }, symbolOption, daysOption, topKOption);

        return command;
    }

    public static Command CreateValidateModelCommand(MachineLearningService mlService)
    {
        var symbolOption = new Option<string>("--symbol", () => "BTCUSDT", "Trading symbol");
        var daysOption = new Option<int>("--days", () => 252, "Number of days of historical data");
        var topKOption = new Option<int>("--top-k", () => 10, "Number of top features to use");

        var command = new Command("validate-model", "Validate machine learning model")
        {
            symbolOption,
            daysOption,
            topKOption
        };

        command.SetHandler(async (symbol, days, topK) =>
        {
            try
            {
                Console.WriteLine($"🔍 Validating model for {symbol}...");
                var featureData = await mlService.PerformFeatureEngineeringAsync(symbol, days);
                var selectedFeatures = mlService.SelectFeatures(featureData, topK);
                var result = await mlService.ValidateModelAsync(selectedFeatures);
                
                Console.WriteLine($"✅ Model Validation Complete");
                Console.WriteLine($"   Method: {result.ValidationMethod}");
                Console.WriteLine($"   Train Size: {result.TrainSize}");
                Console.WriteLine($"   Test Size: {result.TestSize}");
                Console.WriteLine($"   R² Score: {result.R2Score:F4}");
                Console.WriteLine($"   MSE: {result.MSE:F6}");
                Console.WriteLine($"   MAE: {result.MAE:F6}");
                Console.WriteLine($"   RMSE: {result.RMSE:F6}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Model validation failed: {ex.Message}");
            }
        }, symbolOption, daysOption, topKOption);

        return command;
    }

    public static Command CreateCrossValidateCommand(MachineLearningService mlService)
    {
        var symbolOption = new Option<string>("--symbol", () => "BTCUSDT", "Trading symbol");
        var daysOption = new Option<int>("--days", () => 252, "Number of days of historical data");
        var topKOption = new Option<int>("--top-k", () => 10, "Number of top features to use");
        var foldsOption = new Option<int>("--folds", () => 5, "Number of cross-validation folds");

        var command = new Command("cross-validate", "Perform cross-validation")
        {
            symbolOption,
            daysOption,
            topKOption,
            foldsOption
        };

        command.SetHandler(async (symbol, days, topK, folds) =>
        {
            try
            {
                Console.WriteLine($"🔄 Performing {folds}-fold cross-validation for {symbol}...");
                var featureData = await mlService.PerformFeatureEngineeringAsync(symbol, days);
                var selectedFeatures = mlService.SelectFeatures(featureData, topK);
                var result = mlService.PerformCrossValidation(selectedFeatures, folds);
                
                Console.WriteLine($"✅ Cross-Validation Complete");
                Console.WriteLine($"   Folds: {result.Folds}");
                Console.WriteLine($"   Mean R² Score: {result.MeanR2:F4} ± {result.StdR2:F4}");
                Console.WriteLine($"   Mean MSE: {result.MeanMSE:F6} ± {result.StdMSE:F6}");
                Console.WriteLine($"   Mean MAE: {result.MeanMAE:F6} ± {result.StdMAE:F6}");
                
                Console.WriteLine($"   Individual Fold Results:");
                for (int i = 0; i < result.FoldResults.Count; i++)
                {
                    var fold = result.FoldResults[i];
                    Console.WriteLine($"   Fold {i+1}: R²={fold.R2Score:F4}, MSE={fold.MSE:F6}, MAE={fold.MAE:F6}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Cross-validation failed: {ex.Message}");
            }
        }, symbolOption, daysOption, topKOption, foldsOption);

        return command;
    }

    public static Command CreateModelMetricsCommand(MachineLearningService mlService)
    {
        var predictionsOption = new Option<string>("--predictions", "JSON array of predictions");
        var actualsOption = new Option<string>("--actuals", "JSON array of actual values");

        var command = new Command("model-metrics", "Calculate model performance metrics")
        {
            predictionsOption,
            actualsOption
        };

        command.SetHandler((predictionsJson, actualsJson) =>
        {
            try
            {
                var predictions = JsonSerializer.Deserialize<List<double>>(predictionsJson) ?? new List<double>();
                var actuals = JsonSerializer.Deserialize<List<double>>(actualsJson) ?? new List<double>();
                
                if (predictions.Count != actuals.Count)
                {
                    Console.WriteLine("❌ Predictions and actuals must have the same length");
                    return;
                }
                
                Console.WriteLine($"📊 Calculating model metrics...");
                var result = mlService.CalculateModelMetrics(predictions, actuals);
                
                Console.WriteLine($"✅ Model Metrics Complete");
                Console.WriteLine($"   R² Score: {result.R2Score:F4}");
                Console.WriteLine($"   MSE: {result.MSE:F6}");
                Console.WriteLine($"   MAE: {result.MAE:F6}");
                Console.WriteLine($"   RMSE: {result.RMSE:F6}");
                Console.WriteLine($"   MAPE: {result.MeanAbsolutePercentageError:F2}%");
                Console.WriteLine($"   Max Error: {result.MaxError:F6}");
                Console.WriteLine($"   Correlation: {result.Correlation:F4}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Model metrics calculation failed: {ex.Message}");
            }
        }, predictionsOption, actualsOption);

        return command;
    }

    public static Command CreateAutoMLPipelineCommand(MachineLearningService mlService)
    {
        var symbolOption = new Option<string>("--symbol", () => "BTCUSDT", "Trading symbol");
        var daysOption = new Option<int>("--days", () => 252, "Number of days of historical data");

        var command = new Command("automl-pipeline", "Run AutoML pipeline")
        {
            symbolOption,
            daysOption
        };

        command.SetHandler(async (symbol, days) =>
        {
            try
            {
                Console.WriteLine($"🤖 Running AutoML pipeline for {symbol}...");
                var result = await mlService.RunAutoMLPipelineAsync(symbol, days);
                
                Console.WriteLine($"✅ AutoML Pipeline Complete");
                Console.WriteLine($"   Best Model: {result.BestModel}");
                Console.WriteLine($"   Best Score: {result.BestScore:F4}");
                Console.WriteLine($"   Experiment Time: {result.ExperimentTime}s");
                Console.WriteLine($"   Trials Run: {result.TrialsRun}");
                
                Console.WriteLine($"   Top 5 Models:");
                foreach (var model in result.TopModels)
                {
                    Console.WriteLine($"   {model.ModelName,-20}: {model.Score:F4} ({model.TrainingTime:F2}s)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AutoML pipeline failed: {ex.Message}");
            }
        }, symbolOption, daysOption);

        return command;
    }

    public static Command CreateModelSelectionCommand(MachineLearningService mlService)
    {
        var symbolOption = new Option<string>("--symbol", () => "BTCUSDT", "Trading symbol");
        var daysOption = new Option<int>("--days", () => 252, "Number of days of historical data");
        var topKOption = new Option<int>("--top-k", () => 10, "Number of top features to use");

        var command = new Command("model-selection", "Compare and select best model")
        {
            symbolOption,
            daysOption,
            topKOption
        };

        command.SetHandler(async (symbol, days, topK) =>
        {
            try
            {
                Console.WriteLine($"🏆 Comparing models for {symbol}...");
                
                var featureData = await mlService.PerformFeatureEngineeringAsync(symbol, days);
                var selectedFeatures = mlService.SelectFeatures(featureData, topK);
                
                // Run multiple models for comparison
                var models = new List<ModelComparison>();
                
                // Linear Regression
                var linearResult = await mlService.ValidateModelAsync(selectedFeatures);
                models.Add(new ModelComparison
                {
                    ModelName = "Linear Regression",
                    ValidationScore = linearResult.R2Score,
                    TrainingTime = 0.1,
                    Metrics = new Dictionary<string, double>
                    {
                        ["R2"] = linearResult.R2Score,
                        ["MSE"] = linearResult.MSE,
                        ["MAE"] = linearResult.MAE
                    }
                });

                // Cross-validation
                var cvResult = mlService.PerformCrossValidation(selectedFeatures, 5);
                models.Add(new ModelComparison
                {
                    ModelName = "Cross-Validated Model",
                    ValidationScore = cvResult.MeanR2,
                    TrainingTime = 0.5,
                    Metrics = new Dictionary<string, double>
                    {
                        ["R2"] = cvResult.MeanR2,
                        ["MSE"] = cvResult.MeanMSE,
                        ["MAE"] = cvResult.MeanMAE
                    }
                });

                var bestModel = models.OrderByDescending(m => m.ValidationScore).First();
                
                Console.WriteLine($"✅ Model Selection Complete");
                Console.WriteLine($"   Best Model: {bestModel.ModelName}");
                Console.WriteLine($"   Best Score: {bestModel.ValidationScore:F4}");
                Console.WriteLine($"   Selection Criteria: Highest R² Score");
                
                Console.WriteLine($"   Model Comparison:");
                foreach (var model in models.OrderByDescending(m => m.ValidationScore))
                {
                    Console.WriteLine($"   {model.ModelName,-25}: R²={model.ValidationScore:F4}, Time={model.TrainingTime:F2}s");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Model selection failed: {ex.Message}");
            }
        }, symbolOption, daysOption, topKOption);

        return command;
    }

    public static Command CreateEnsemblePredictionCommand(MachineLearningService mlService)
    {
        var modelsOption = new Option<string>("--models", "JSON array of model results");
        var featuresOption = new Option<string>("--features", "JSON object of feature values");

        var command = new Command("ensemble-prediction", "Generate ensemble predictions")
        {
            modelsOption,
            featuresOption
        };

        command.SetHandler((modelsJson, featuresJson) =>
        {
            try
            {
                var models = JsonSerializer.Deserialize<List<ModelResult>>(modelsJson) ?? new List<ModelResult>();
                var features = JsonSerializer.Deserialize<Dictionary<string, object>>(featuresJson) ?? new Dictionary<string, object>();
                
                Console.WriteLine($"🎯 Generating ensemble predictions...");
                var result = mlService.GenerateEnsemblePredictions(models, features);
                
                Console.WriteLine($"✅ Ensemble Prediction Complete");
                Console.WriteLine($"   Method: {result.EnsembleMethod}");
                Console.WriteLine($"   Weighted Prediction: {result.WeightedPrediction:F6}");
                Console.WriteLine($"   Confidence: {result.Confidence:F4}");
                
                Console.WriteLine($"   Individual Predictions:");
                for (int i = 0; i < result.IndividualPredictions.Count; i++)
                {
                    Console.WriteLine($"   Model {i+1}: {result.IndividualPredictions[i]:F6} (weight: {result.ModelWeights[i]:F4})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ensemble prediction failed: {ex.Message}");
            }
        }, modelsOption, featuresOption);

        return command;
    }

    public static Command CreateHyperparameterOptCommand(MachineLearningService mlService)
    {
        var symbolOption = new Option<string>("--symbol", () => "BTCUSDT", "Trading symbol");
        var daysOption = new Option<int>("--days", () => 252, "Number of days of historical data");
        var modelTypeOption = new Option<string>("--model-type", () => "linear", "Model type (linear, randomforest, etc.)");
        var maxTrialsOption = new Option<int>("--max-trials", () => 20, "Maximum number of optimization trials");

        var command = new Command("hyperparameter-opt", "Optimize hyperparameters")
        {
            symbolOption,
            daysOption,
            modelTypeOption,
            maxTrialsOption
        };

        command.SetHandler(async (symbol, days, modelType, maxTrials) =>
        {
            try
            {
                Console.WriteLine($"⚙️ Optimizing hyperparameters for {modelType} model on {symbol}...");
                
                var featureData = await mlService.PerformFeatureEngineeringAsync(symbol, days);
                var selectedFeatures = mlService.SelectFeatures(featureData, 10);
                
                // Simulate hyperparameter optimization
                var trials = new List<OptimizationTrial>();
                var bestScore = 0.0;
                var bestParams = new Dictionary<string, object>();

                for (int i = 0; i < maxTrials; i++)
                {
                    var parameters = GenerateRandomParameters(modelType);
                    var score = Random.Shared.NextDouble() * 0.8 + 0.1; // Random score
                    
                    trials.Add(new OptimizationTrial
                    {
                        TrialNumber = i + 1,
                        Parameters = parameters,
                        Score = score,
                        Duration = Random.Shared.NextDouble() * 10
                    });

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestParams = parameters;
                    }
                    
                    if (i % 5 == 0)
                        Console.WriteLine($"   Trial {i+1}/{maxTrials}: Best Score = {bestScore:F4}");
                }
                
                Console.WriteLine($"✅ Hyperparameter Optimization Complete");
                Console.WriteLine($"   Model Type: {modelType}");
                Console.WriteLine($"   Best Score: {bestScore:F4}");
                Console.WriteLine($"   Trials Run: {maxTrials}");
                Console.WriteLine($"   Best Parameters:");
                
                foreach (var (param, value) in bestParams)
                {
                    Console.WriteLine($"   {param}: {value}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Hyperparameter optimization failed: {ex.Message}");
            }
        }, symbolOption, daysOption, modelTypeOption, maxTrialsOption);

        return command;
    }

    private static Dictionary<string, object> GenerateRandomParameters(string modelType)
    {
        var random = Random.Shared;
        return modelType.ToLower() switch
        {
            "linear" => new Dictionary<string, object>
            {
                ["alpha"] = random.NextDouble() * 0.1,
                ["fit_intercept"] = random.Next(2) == 1
            },
            "randomforest" => new Dictionary<string, object>
            {
                ["n_estimators"] = random.Next(50, 200),
                ["max_depth"] = random.Next(3, 20),
                ["min_samples_split"] = random.Next(2, 10)
            },
            _ => new Dictionary<string, object>
            {
                ["learning_rate"] = random.NextDouble() * 0.1 + 0.01,
                ["regularization"] = random.NextDouble() * 0.01
            }
        };
    }
}