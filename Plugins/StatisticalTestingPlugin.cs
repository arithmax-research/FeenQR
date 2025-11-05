using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

public class StatisticalTestingPlugin
{
    private readonly StatisticalTestingService _statisticalTestingService;

    public StatisticalTestingPlugin(StatisticalTestingService statisticalTestingService)
    {
        _statisticalTestingService = statisticalTestingService;
    }

    [KernelFunction, Description("Perform a two-sample t-test to compare means of two groups")]
    public async Task<string> PerformTTest(
        [Description("First sample data as comma-separated values")] string sample1Data,
        [Description("Second sample data as comma-separated values")] string sample2Data,
        [Description("Assume equal variance (true) or unequal variance (false)")] bool equalVariance = true,
        [Description("Significance level (default 0.05)")] double significanceLevel = 0.05)
    {
        try
        {
            var sample1 = sample1Data.Split(',').Select(double.Parse).ToArray();
            var sample2 = sample2Data.Split(',').Select(double.Parse).ToArray();

            var result = _statisticalTestingService.PerformTTest(sample1, sample2, equalVariance, significanceLevel);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error performing t-test: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform one-way ANOVA to compare means across multiple groups")]
    public async Task<string> PerformANOVA(
        [Description("Groups data as JSON array of arrays, e.g., [[1,2,3],[4,5,6]]")] string groupsData,
        [Description("Significance level (default 0.05)")] double significanceLevel = 0.05)
    {
        try
        {
            var groups = JsonSerializer.Deserialize<double[][]>(groupsData);
            if (groups == null)
                return "Invalid groups data format";

            var result = _statisticalTestingService.PerformANOVA(groups, significanceLevel);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error performing ANOVA: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform chi-square test for independence on contingency table")]
    public async Task<string> PerformChiSquareTest(
        [Description("Contingency table as JSON array of arrays, e.g., [[10,5],[8,12]]")] string contingencyTableData,
        [Description("Significance level (default 0.05)")] double significanceLevel = 0.05)
    {
        try
        {
            var table = JsonSerializer.Deserialize<double[][]>(contingencyTableData);
            if (table == null || table.Length == 0)
                return "Invalid contingency table format";

            int rows = table.Length;
            int cols = table[0].Length;
            double[,] contingencyTable = new double[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    contingencyTable[i, j] = table[i][j];
                }
            }

            var result = _statisticalTestingService.PerformChiSquareTest(contingencyTable, significanceLevel);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error performing chi-square test: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform Mann-Whitney U test (non-parametric alternative to t-test)")]
    public async Task<string> PerformMannWhitneyTest(
        [Description("First sample data as comma-separated values")] string sample1Data,
        [Description("Second sample data as comma-separated values")] string sample2Data,
        [Description("Significance level (default 0.05)")] double significanceLevel = 0.05)
    {
        try
        {
            var sample1 = sample1Data.Split(',').Select(double.Parse).ToArray();
            var sample2 = sample2Data.Split(',').Select(double.Parse).ToArray();

            var result = _statisticalTestingService.PerformMannWhitneyTest(sample1, sample2, significanceLevel);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error performing Mann-Whitney test: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform power analysis to determine required sample size or statistical power")]
    public async Task<string> PerformPowerAnalysis(
        [Description("Effect size (Cohen's d)")] double effectSize,
        [Description("Sample size per group")] int sampleSize,
        [Description("Significance level (default 0.05)")] double significanceLevel = 0.05,
        [Description("Test type (default 'two-sample')")] string testType = "two-sample")
    {
        try
        {
            var result = _statisticalTestingService.PerformPowerAnalysis(effectSize, sampleSize, significanceLevel, testType);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error performing power analysis: {ex.Message}";
        }
    }

    [KernelFunction, Description("Run hypothesis test with custom null and alternative hypotheses")]
    public async Task<string> RunHypothesisTest(
        [Description("Test type: t-test, anova, chi-square, mann-whitney")] string testType,
        [Description("Data for the test (format depends on test type)")] string data,
        [Description("Null hypothesis description")] string nullHypothesis,
        [Description("Alternative hypothesis description")] string alternativeHypothesis,
        [Description("Significance level (default 0.05)")] double significanceLevel = 0.05)
    {
        try
        {
            string result = testType.ToLower() switch
            {
                "t-test" => await PerformTTest(data.Split('|')[0], data.Split('|')[1], true, significanceLevel),
                "anova" => await PerformANOVA(data, significanceLevel),
                "chi-square" => await PerformChiSquareTest(data, significanceLevel),
                "mann-whitney" => await PerformMannWhitneyTest(data.Split('|')[0], data.Split('|')[1], significanceLevel),
                _ => "Unsupported test type"
            };

            if (result.Contains("Error"))
                return result;

            // Parse and update hypotheses
            var testResult = JsonSerializer.Deserialize<QuantResearchAgent.Core.StatisticalTest>(result);
            if (testResult != null)
            {
                testResult.NullHypothesis = nullHypothesis;
                testResult.AlternativeHypothesis = alternativeHypothesis;
                return JsonSerializer.Serialize(testResult, new JsonSerializerOptions { WriteIndented = true });
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error running hypothesis test: {ex.Message}";
        }
    }
}