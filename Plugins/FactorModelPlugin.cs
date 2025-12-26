using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for factor model analysis
/// </summary>
public class FactorModelPlugin
{
    private readonly FactorModelService _factorModelService;

    public FactorModelPlugin(FactorModelService factorModelService)
    {
        _factorModelService = factorModelService;
    }

    [KernelFunction, Description("Run Fama-French 3-Factor model analysis on an asset")]
    public async Task<string> AnalyzeFamaFrench3FactorAsync(
        [Description("The asset symbol to analyze (e.g., AAPL, MSFT)")] string symbol,
        [Description("Start date for analysis (YYYY-MM-DD)")] string startDate,
        [Description("End date for analysis (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            var result = await _factorModelService.AnalyzeFamaFrench3FactorAsync(symbol, start, end);

            var response = $"FAMA-FRENCH 3-FACTOR ANALYSIS: {symbol}\n\n" +
                          $"Analysis Date: {result.AnalysisDate:yyyy-MM-dd}\n" +
                          $"R²: {result.R2:P3}\n" +
                          $"Alpha: {result.Alpha:P3}\n\n" +
                          $"Factor Exposures:\n" +
                          $"- Market Beta: {result.MarketBeta:F4}\n" +
                          $"- Size Beta (SMB): {result.SizeBeta:F4}\n" +
                          $"- Value Beta (HML): {result.ValueBeta:F4}\n\n" +
                          $"Factor Returns:\n";

            foreach (var factorReturn in result.FactorReturns)
            {
                response += $"- {factorReturn.Key}: {factorReturn.Value:P3}\n";
            }

            return response.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to analyze Fama-French 3-Factor for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Run Carhart 4-Factor model analysis on an asset")]
    public async Task<string> AnalyzeCarhart4FactorAsync(
        [Description("The asset symbol to analyze (e.g., AAPL, MSFT)")] string symbol,
        [Description("Start date for analysis (YYYY-MM-DD)")] string startDate,
        [Description("End date for analysis (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            var result = await _factorModelService.AnalyzeCarhart4FactorAsync(symbol, start, end);

            var response = $"CARHART 4-FACTOR ANALYSIS: {symbol}\n\n" +
                          $"Analysis Date: {result.AnalysisDate:yyyy-MM-dd}\n" +
                          $"R²: {result.R2:P3}\n" +
                          $"Alpha: {result.Alpha:P3}\n\n" +
                          $"Factor Exposures:\n" +
                          $"- Market Beta: {result.MarketBeta:F4}\n" +
                          $"- Size Beta (SMB): {result.SizeBeta:F4}\n" +
                          $"- Value Beta (HML): {result.ValueBeta:F4}\n" +
                          $"- Momentum Beta (MOM): {result.MomentumBeta:F4}\n\n" +
                          $"Factor Returns:\n";

            foreach (var factorReturn in result.FactorReturns)
            {
                response += $"- {factorReturn.Key}: {factorReturn.Value:P3}\n";
            }

            return response.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to analyze Carhart 4-Factor for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Run Fama-French 5-Factor model analysis on an asset")]
    public async Task<string> AnalyzeFamaFrench5FactorAsync(
        [Description("The asset symbol to analyze (e.g., AAPL, MSFT)")] string symbol,
        [Description("Start date for analysis (YYYY-MM-DD)")] string startDate,
        [Description("End date for analysis (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            var result = await _factorModelService.AnalyzeFamaFrench5FactorAsync(symbol, start, end);

            var response = $"FAMA-FRENCH 5-FACTOR ANALYSIS: {symbol}\n\n" +
                          $"Analysis Date: {result.AnalysisDate:yyyy-MM-dd}\n" +
                          $"R²: {result.R2:P3}\n" +
                          $"Alpha: {result.Alpha:P3}\n\n" +
                          $"Factor Exposures:\n" +
                          $"- Market Beta: {result.MarketBeta:F4}\n" +
                          $"- Size Beta (SMB): {result.SizeBeta:F4}\n" +
                          $"- Value Beta (HML): {result.ValueBeta:F4}\n" +
                          $"- Profitability Beta (RMW): {result.ProfitabilityBeta:F4}\n" +
                          $"- Investment Beta (CMA): {result.InvestmentBeta:F4}\n\n" +
                          $"Factor Returns:\n";

            foreach (var factorReturn in result.FactorReturns)
            {
                response += $"- {factorReturn.Key}: {factorReturn.Value:P3}\n";
            }

            return response.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to analyze Fama-French 5-Factor for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Create and analyze a custom factor model")]
    public async Task<string> CreateCustomFactorModelAsync(
        [Description("Name of the custom factor model")] string modelName,
        [Description("Description of the factor model")] string description,
        [Description("Comma-separated list of factor names")] string factors,
        [Description("The asset symbol to analyze")] string symbol,
        [Description("Start date for analysis (YYYY-MM-DD)")] string startDate,
        [Description("End date for analysis (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            var factorList = factors.Split(',').Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f)).ToList();
            if (!factorList.Any())
            {
                return "ERROR: No valid factors provided.";
            }

            var result = await _factorModelService.CreateCustomFactorModelAsync(modelName, description, factorList, symbol, start, end);

            var response = $"CUSTOM FACTOR MODEL: {result.Name}\n\n" +
                          $"Description: {result.Description}\n" +
                          $"Asset: {symbol}\n" +
                          $"Analysis Date: {result.AnalysisDate:yyyy-MM-dd}\n" +
                          $"R²: {result.R2:P3}\n" +
                          $"Alpha: {result.Alpha:P3}\n\n" +
                          $"Factor Exposures:\n";

            foreach (var exposure in result.FactorExposures)
            {
                response += $"- {exposure.Key}: {exposure.Value:F4}\n";
            }

            response += $"\nFactor Returns:\n";
            foreach (var factorReturn in result.FactorReturns)
            {
                response += $"- {factorReturn.Key}: {factorReturn.Value:P3}\n";
            }

            return response.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to create custom factor model {modelName}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform factor attribution analysis across multiple assets")]
    public async Task<string> PerformFactorAttributionAsync(
        [Description("Comma-separated list of asset symbols")] string assets,
        [Description("Comma-separated list of factor names")] string factors,
        [Description("Start date for analysis (YYYY-MM-DD)")] string startDate,
        [Description("End date for analysis (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            var assetList = assets.Split(',').Select(a => a.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToList();
            var factorList = factors.Split(',').Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f)).ToList();

            if (!assetList.Any() || !factorList.Any())
            {
                return "ERROR: No valid assets or factors provided.";
            }

            var results = await _factorModelService.PerformFactorAttributionAsync(assetList, factorList, start, end);

            if (!results.Any())
            {
                return "ERROR: No attribution results generated.";
            }

            var response = $"FACTOR ATTRIBUTION ANALYSIS\n\n" +
                          $"Period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}\n" +
                          $"Factors: {string.Join(", ", factorList)}\n\n";

            foreach (var result in results)
            {
                response += $"Asset: {result.Asset}\n" +
                           $"Total Attribution: {result.TotalAttribution:P3}\n" +
                           $"Residual Return: {result.ResidualReturn:P3}\n" +
                           $"Factor Contributions:\n";

                foreach (var contribution in result.FactorContributions)
                {
                    response += $"  - {contribution.Key}: {contribution.Value:P3}\n";
                }

                response += "\n";
            }

            return response.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to perform factor attribution analysis: {ex.Message}";
        }
    }

    [KernelFunction, Description("Compare factor model performance across different models")]
    public async Task<string> CompareFactorModelsAsync(
        [Description("The asset symbol to analyze")] string symbol,
        [Description("Start date for analysis (YYYY-MM-DD)")] string startDate,
        [Description("End date for analysis (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            // Run all three models
            var ff3Result = await _factorModelService.AnalyzeFamaFrench3FactorAsync(symbol, start, end);
            var carhartResult = await _factorModelService.AnalyzeCarhart4FactorAsync(symbol, start, end);

            var response = $"FACTOR MODEL COMPARISON: {symbol}\n\n" +
                          $"Analysis Period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}\n\n" +
                          $"Fama-French 3-Factor Model:\n" +
                          $"- R²: {ff3Result.R2:P3}\n" +
                          $"- Alpha: {ff3Result.Alpha:P3}\n" +
                          $"- Market Beta: {ff3Result.MarketBeta:F4}\n" +
                          $"- Size Beta: {ff3Result.SizeBeta:F4}\n" +
                          $"- Value Beta: {ff3Result.ValueBeta:F4}\n\n" +
                          $"Carhart 4-Factor Model:\n" +
                          $"- R²: {carhartResult.R2:P3}\n" +
                          $"- Alpha: {carhartResult.Alpha:P3}\n" +
                          $"- Market Beta: {carhartResult.MarketBeta:F4}\n" +
                          $"- Size Beta: {carhartResult.SizeBeta:F4}\n" +
                          $"- Value Beta: {carhartResult.ValueBeta:F4}\n" +
                          $"- Momentum Beta: {carhartResult.MomentumBeta:F4}\n\n";

            // Model comparison
            var ff3Explained = ff3Result.R2;
            var carhartExplained = carhartResult.R2;

            response += $"Model Comparison:\n" +
                       $"- Fama-French explains {ff3Explained:P1} of return variation\n" +
                       $"- Carhart explains {carhartExplained:P1} of return variation\n" +
                       $"- Additional explanatory power from momentum: {(carhartExplained - ff3Explained):P1}\n\n";

            if (carhartExplained > ff3Explained)
            {
                response += $"Recommendation: Carhart 4-Factor model provides better fit for {symbol}";
            }
            else
            {
                response += $"Recommendation: Fama-French 3-Factor model is sufficient for {symbol}";
            }

            return response;
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to compare factor models for {symbol}: {ex.Message}";
        }
        }
    }
