using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class DynamicFactorPlugin
    {
        private readonly DynamicFactorService _factorService;

        public DynamicFactorPlugin(DynamicFactorService factorService)
        {
            _factorService = factorService;
        }

        [KernelFunction("compute_dynamic_factors")]
        [Description("Compute dynamic factor model for portfolio analysis")]
        public async Task<string> ComputeDynamicFactorsAsync(
            [Description("Comma-separated list of stock symbols")] string symbols,
            [Description("Lookback period in days")] int lookbackDays = 252)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["symbols"] = symbols,
                    ["lookbackDays"] = lookbackDays
                };

                return await _factorService.ComputeDynamicFactorsAsync(data);
            }
            catch (Exception ex)
            {
                return $"Error computing dynamic factors: {ex.Message}";
            }
        }

        [KernelFunction("analyze_dynamic_factors")]
        [Description("Analyze dynamic factors for portfolio analysis")]
        public string AnalyzeDynamicFactors(
            [Description("Comma-separated list of stock symbols")] string symbols,
            [Description("Lookback period in days")] int lookbackDays = 252)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["symbols"] = symbols,
                    ["lookbackDays"] = lookbackDays
                };

                return Task.Run(() => _factorService.ComputeDynamicFactorsAsync(data)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return $"Error analyzing dynamic factors: {ex.Message}";
            }
        }

        [KernelFunction("factor_attribution")]
        [Description("Perform factor attribution analysis for a portfolio")]
        public async Task<string> PerformFactorAttributionAsync(
            [Description("Comma-separated list of stock symbols")] string symbols,
            [Description("Portfolio weights (comma-separated, optional)")] string weights = "")
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["symbols"] = symbols,
                    ["weights"] = weights,
                    ["analysisType"] = "attribution"
                };

                var factors = await _factorService.ComputeDynamicFactorsAsync(data);
                return $"Factor Attribution Analysis:\n{factors}";
            }
            catch (Exception ex)
            {
                return $"Error performing factor attribution: {ex.Message}";
            }
        }

        [KernelFunction("regime_adjusted_factors")]
        [Description("Compute regime-adjusted factor exposures")]
        public async Task<string> ComputeRegimeAdjustedFactorsAsync(
            [Description("Comma-separated list of stock symbols")] string symbols)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["symbols"] = symbols,
                    ["analysisType"] = "regime_adjusted"
                };

                var factors = await _factorService.ComputeDynamicFactorsAsync(data);
                return $"Regime-Adjusted Factor Analysis:\n{factors}";
            }
            catch (Exception ex)
            {
                return $"Error computing regime-adjusted factors: {ex.Message}";
            }
        }

        [KernelFunction("factor_risk_decomposition")]
        [Description("Decompose portfolio risk by factors")]
        public async Task<string> DecomposeFactorRiskAsync(
            [Description("Comma-separated list of stock symbols")] string symbols,
            [Description("Portfolio weights (comma-separated)")] string weights)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["symbols"] = symbols,
                    ["weights"] = weights,
                    ["analysisType"] = "risk_decomposition"
                };

                var factors = await _factorService.ComputeDynamicFactorsAsync(data);
                return $"Factor Risk Decomposition:\n{factors}";
            }
            catch (Exception ex)
            {
                return $"Error decomposing factor risk: {ex.Message}";
            }
        }

        [KernelFunction("predictive_factor_model")]
        [Description("Build predictive factor model for return forecasting")]
        public async Task<string> BuildPredictiveFactorModelAsync(
            [Description("Target stock symbol")] string targetSymbol,
            [Description("Predictor symbols (comma-separated)")] string predictors,
            [Description("Forecast horizon in days")] int horizon = 30)
        {
            try
            {
                var allSymbols = $"{targetSymbol},{predictors}";
                var data = new Dictionary<string, object>
                {
                    ["symbols"] = allSymbols,
                    ["target"] = targetSymbol,
                    ["horizon"] = horizon,
                    ["analysisType"] = "predictive"
                };

                var factors = await _factorService.ComputeDynamicFactorsAsync(data);
                return $"Predictive Factor Model for {targetSymbol} ({horizon}-day horizon):\n{factors}";
            }
            catch (Exception ex)
            {
                return $"Error building predictive factor model: {ex.Message}";
            }
        }

        [KernelFunction("factor_timing_strategy")]
        [Description("Generate factor timing strategy recommendations")]
        public async Task<string> GenerateFactorTimingStrategyAsync(
            [Description("Comma-separated list of factors to time")] string factors = "Market,Value,Momentum")
        {
            try
            {
                var factorList = factors.Split(',').Select(f => f.Trim()).ToList();

                var strategy = new System.Text.StringBuilder();
                strategy.AppendLine("Factor Timing Strategy Recommendations:");
                strategy.AppendLine();

                foreach (var factor in factorList)
                {
                    strategy.AppendLine($"{factor} Factor:");
                    strategy.AppendLine("- Timing Signal: [Based on current factor momentum]");
                    strategy.AppendLine("- Recommended Position: [Long/Short/Neutral]");
                    strategy.AppendLine("- Confidence Level: [High/Medium/Low]");
                    strategy.AppendLine();
                }

                return strategy.ToString();
            }
            catch (Exception ex)
            {
                return $"Error generating factor timing strategy: {ex.Message}";
            }
        }

        [KernelFunction("multi_factor_portfolio")]
        [Description("Construct multi-factor portfolio with optimal factor exposures")]
        public async Task<string> ConstructMultiFactorPortfolioAsync(
            [Description("Available stock symbols")] string symbols,
            [Description("Target factor exposures (JSON format)")] string targetExposures = "{}")
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["symbols"] = symbols,
                    ["targetExposures"] = targetExposures,
                    ["analysisType"] = "portfolio_construction"
                };

                var factors = await _factorService.ComputeDynamicFactorsAsync(data);
                return $"Multi-Factor Portfolio Construction:\n{factors}";
            }
            catch (Exception ex)
            {
                return $"Error constructing multi-factor portfolio: {ex.Message}";
            }
        }
    }
}
