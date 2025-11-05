using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

public class CointegrationPlugin
{
    private readonly CointegrationAnalysisService _cointegrationService;
    private readonly ILLMService _llmService;
    private readonly ILogger<CointegrationPlugin> _logger;

    public CointegrationPlugin(
        CointegrationAnalysisService cointegrationService,
        ILLMService llmService,
        ILogger<CointegrationPlugin> logger)
    {
        _cointegrationService = cointegrationService;
        _llmService = llmService;
        _logger = logger;
    }

    [KernelFunction("engle_granger_cointegration")]
    [Description("Performs Engle-Granger cointegration test between two time series")]
    public async Task<string> PerformEngleGrangerTestAsync(
        [Description("First time series data as comma-separated numbers")] string series1,
        [Description("Second time series data as comma-separated numbers")] string series2,
        [Description("Maximum lag order for ADF test")] int maxLags = 10)
    {
        try
        {
            var data1 = series1.Split(',').Select(double.Parse).ToArray();
            var data2 = series2.Split(',').Select(double.Parse).ToArray();

            var result = _cointegrationService.PerformEngleGrangerTest(data1, data2, maxLags);

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Interpret this Engle-Granger cointegration test result: Test statistic={result.TestStatistic:F4}, " +
                $"Critical 5%={result.CriticalValues[0.05]:F4}, Is cointegrated={result.IsCointegrated}. " +
                $"Cointegration vector: [{string.Join(", ", result.CointegrationVector.Select(x => x.ToString("F4")))}]. " +
                $"Explain what cointegration means for these financial instruments and trading implications.");

            return $"Engle-Granger Cointegration Test Results:\n" +
                   $"Test Statistic: {result.TestStatistic:F4}\n" +
                   $"Critical Values: 1%={result.CriticalValues[0.01]:F2}, 5%={result.CriticalValues[0.05]:F2}, 10%={result.CriticalValues[0.10]:F2}\n" +
                   $"Are Series Cointegrated: {result.IsCointegrated}\n" +
                   $"Cointegration Vector: [{string.Join(", ", result.CointegrationVector.Select(x => x.ToString("F4")))}]\n" +
                   $"Residual Variance: {result.ResidualVariance:F6}\n\n" +
                   $"AI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Engle-Granger test");
            return $"Error performing Engle-Granger test: {ex.Message}";
        }
    }

    [KernelFunction("johansen_cointegration")]
    [Description("Performs Johansen cointegration test for multiple time series")]
    public async Task<string> PerformJohansenTestAsync(
        [Description("Multiple time series data as semicolon-separated series, each comma-separated")] string seriesData,
        [Description("Lag order for the VECM")] int lagOrder = 1)
    {
        try
        {
            var series = seriesData.Split(';')
                .Select(s => s.Split(',').Select(double.Parse).ToArray())
                .ToArray();

            var result = _cointegrationService.PerformJohansenTest(series, lagOrder);

            var eigenvaluesText = string.Join(", ", result.Eigenvalues.Select(x => x.ToString("F4")));
            var traceStatsText = string.Join(", ", result.TraceStatistics.Select(x => x.ToString("F4")));
            var maxEigenText = string.Join(", ", result.MaxEigenvalueStatistics.Select(x => x.ToString("F4")));

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Interpret this Johansen cointegration test: {result.Symbols.Count} series tested, " +
                $"cointegration rank={result.Rank}, eigenvalues=[{eigenvaluesText}]. " +
                $"Trace statistics=[{traceStatsText}], Max eigenvalue statistics=[{maxEigenText}]. " +
                $"Explain what this means for the relationship between these financial instruments.");

            return $"Johansen Cointegration Test Results:\n" +
                   $"Number of Series: {result.Symbols.Count}\n" +
                   $"Cointegration Rank: {result.Rank}\n" +
                   $"Eigenvalues: [{eigenvaluesText}]\n" +
                   $"Trace Statistics: [{traceStatsText}]\n" +
                   $"Max Eigenvalue Statistics: [{maxEigenText}]\n" +
                   $"Critical Values (5%): [{string.Join(", ", result.CriticalValues[0.05].Select(x => x.ToString("F2")))}]\n\n" +
                   $"AI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Johansen test");
            return $"Error performing Johansen test: {ex.Message}";
        }
    }

    [KernelFunction("granger_causality")]
    [Description("Tests Granger causality between two time series")]
    public async Task<string> TestGrangerCausalityAsync(
        [Description("Potential cause series as comma-separated numbers")] string causeSeries,
        [Description("Effect series as comma-separated numbers")] string effectSeries,
        [Description("Lag order for the test")] int lagOrder = 5)
    {
        try
        {
            var causeData = causeSeries.Split(',').Select(double.Parse).ToArray();
            var effectData = effectSeries.Split(',').Select(double.Parse).ToArray();

            var result = _cointegrationService.PerformGrangerCausalityTest(causeData, effectData, lagOrder);

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Interpret this Granger causality test: F-statistic={result.FStatistic:F4}, p-value={result.PValue:F4}, " +
                $"Granger causes={result.GrangerCauses} at 5% significance. Lag order={lagOrder}. " +
                $"Explain what this means for the causal relationship between these variables and trading implications.");

            return $"Granger Causality Test Results:\n" +
                   $"Cause Series → Effect Series\n" +
                   $"Lag Order: {result.LagOrder}\n" +
                   $"F-Statistic: {result.FStatistic:F4}\n" +
                   $"P-Value: {result.PValue:F4}\n" +
                   $"Granger Causes (5% significance): {result.GrangerCauses}\n" +
                   $"Significance Level: {result.SignificanceLevel:P1}\n\n" +
                   $"AI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Granger causality test");
            return $"Error performing Granger causality test: {ex.Message}";
        }
    }

    [KernelFunction("analyze_lead_lag")]
    [Description("Analyzes lead-lag relationships between two time series")]
    public async Task<string> AnalyzeLeadLagRelationshipAsync(
        [Description("First time series as comma-separated numbers")] string series1,
        [Description("Second time series as comma-separated numbers")] string series2,
        [Description("Maximum lag to test")] int maxLag = 10)
    {
        try
        {
            var data1 = series1.Split(',').Select(double.Parse).ToArray();
            var data2 = series2.Split(',').Select(double.Parse).ToArray();

            var result = _cointegrationService.AnalyzeLeadLagRelationship(data1, data2, maxLag);

            var crossCorrText = string.Join(", ", result.CrossCorrelations
                .Select((x, i) => $"{i - maxLag}:{x:F3}")
                .Where((x, i) => Math.Abs(result.CrossCorrelations[i]) > 0.3));

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Interpret this lead-lag analysis: Optimal lag={result.OptimalLag}, " +
                $"cross-correlation={result.CrossCorrelation:F4}, {result.Symbol1} leads by {result.LagPeriods} periods. " +
                $"Explain what this means for the temporal relationship between these financial instruments.");

            return $"Lead-Lag Relationship Analysis:\n" +
                   $"{result.Symbol1} vs {result.Symbol2}\n" +
                   $"Optimal Lag: {result.OptimalLag} periods\n" +
                   $"Cross-Correlation: {result.CrossCorrelation:F4}\n" +
                   $"{result.Symbol1} Leads: {result.Symbol1Leads}\n" +
                   $"Lag Periods: {result.LagPeriods}\n" +
                   $"Significant Cross-Correlations: {crossCorrText}\n\n" +
                   $"AI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in lead-lag analysis");
            return $"Error analyzing lead-lag relationship: {ex.Message}";
        }
    }

    [KernelFunction("comprehensive_relationship_analysis")]
    [Description("Comprehensive analysis of relationships between financial instruments")]
    public async Task<string> ComprehensiveRelationshipAnalysisAsync(
        [Description("First time series as comma-separated numbers")] string series1,
        [Description("Second time series as comma-separated numbers")] string series2,
        [Description("Names of the two series")] string seriesNames = "Series1;Series2")
    {
        try
        {
            var data1 = series1.Split(',').Select(double.Parse).ToArray();
            var data2 = series2.Split(',').Select(double.Parse).ToArray();
            var names = seriesNames.Split(';');

            var results = new List<string>();

            // Engle-Granger cointegration
            var egResult = _cointegrationService.PerformEngleGrangerTest(data1, data2);
            results.Add($"Engle-Granger: Cointegrated={egResult.IsCointegrated} (stat={egResult.TestStatistic:F4})");

            // Granger causality
            var gcResult = _cointegrationService.PerformGrangerCausalityTest(data1, data2);
            results.Add($"Granger Causality ({names[0]}→{names[1]}): {gcResult.GrangerCauses} (F={gcResult.FStatistic:F4}, p={gcResult.PValue:F4})");

            var gcResultRev = _cointegrationService.PerformGrangerCausalityTest(data2, data1);
            results.Add($"Granger Causality ({names[1]}→{names[0]}): {gcResultRev.GrangerCauses} (F={gcResultRev.FStatistic:F4}, p={gcResultRev.PValue:F4})");

            // Lead-lag
            var llResult = _cointegrationService.AnalyzeLeadLagRelationship(data1, data2);
            results.Add($"Lead-Lag: {names[llResult.Symbol1Leads ? 0 : 1]} leads by {llResult.LagPeriods} periods (corr={llResult.CrossCorrelation:F4})");

            var summary = string.Join("; ", results);

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Provide a comprehensive analysis of the relationships between {names[0]} and {names[1]}: {summary}. " +
                $"Explain the economic implications of these statistical relationships for portfolio management and trading strategies.");

            return $"Comprehensive Relationship Analysis: {names[0]} vs {names[1]}\n\n" +
                   $"{string.Join("\n", results)}\n\n" +
                   $"AI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in comprehensive relationship analysis");
            return $"Error in relationship analysis: {ex.Message}";
        }
    }
}