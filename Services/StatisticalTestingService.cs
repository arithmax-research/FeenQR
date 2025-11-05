using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;

namespace QuantResearchAgent.Services;

public class StatisticalTestingService
{
    private readonly ILogger<StatisticalTestingService> _logger;

    public StatisticalTestingService(ILogger<StatisticalTestingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs a two-sample t-test
    /// </summary>
    public StatisticalTest PerformTTest(IEnumerable<double> sample1, IEnumerable<double> sample2,
        bool equalVariance = true, double significanceLevel = 0.05)
    {
        var s1 = sample1.ToArray();
        var s2 = sample2.ToArray();

        if (s1.Length < 2 || s2.Length < 2)
            throw new ArgumentException("Each sample must have at least 2 observations");

        // Calculate means and variances
        double mean1 = s1.Average();
        double mean2 = s2.Average();
        double var1 = s1.Variance();
        double var2 = s2.Variance();

        // Calculate t-statistic
        double pooledVariance, se, tStatistic;
        if (equalVariance)
        {
            pooledVariance = ((s1.Length - 1) * var1 + (s2.Length - 1) * var2) / (s1.Length + s2.Length - 2);
            se = Math.Sqrt(pooledVariance * (1.0 / s1.Length + 1.0 / s2.Length));
            tStatistic = (mean1 - mean2) / se;
        }
        else
        {
            se = Math.Sqrt(var1 / s1.Length + var2 / s2.Length);
            tStatistic = (mean1 - mean2) / se;
        }

        // Calculate degrees of freedom
        double df = equalVariance ?
            s1.Length + s2.Length - 2 :
            Math.Pow(var1 / s1.Length + var2 / s2.Length, 2) /
            (Math.Pow(var1 / s1.Length, 2) / (s1.Length - 1) + Math.Pow(var2 / s2.Length, 2) / (s2.Length - 1));

        // Calculate p-value using Student's t-distribution
        var tDist = new StudentT(0, 1, df);
        double pValue = 2 * (1 - tDist.CumulativeDistribution(Math.Abs(tStatistic)));

        bool isSignificant = pValue < significanceLevel;

        string interpretation = isSignificant
            ? $"Significant difference between groups (p = {pValue:F4})"
            : $"No significant difference between groups (p = {pValue:F4})";

        return new StatisticalTest
        {
            TestName = "Two-Sample T-Test",
            TestType = equalVariance ? "Equal Variance" : "Unequal Variance",
            TestStatistic = tStatistic,
            PValue = pValue,
            SignificanceLevel = significanceLevel,
            IsSignificant = isSignificant,
            NullHypothesis = "μ₁ = μ₂ (means are equal)",
            AlternativeHypothesis = "μ₁ ≠ μ₂ (means are different)",
            Parameters = new Dictionary<string, double>
            {
                ["Mean1"] = mean1,
                ["Mean2"] = mean2,
                ["Variance1"] = var1,
                ["Variance2"] = var2,
                ["SampleSize1"] = s1.Length,
                ["SampleSize2"] = s2.Length,
                ["DegreesOfFreedom"] = df
            },
            Interpretation = interpretation
        };
    }

    /// <summary>
    /// Performs one-way ANOVA test
    /// </summary>
    public StatisticalTest PerformANOVA(IEnumerable<IEnumerable<double>> groups, double significanceLevel = 0.05)
    {
        var groupArrays = groups.Select(g => g.ToArray()).ToArray();

        if (groupArrays.Length < 2)
            throw new ArgumentException("At least 2 groups are required for ANOVA");

        int totalN = groupArrays.Sum(g => g.Length);
        double grandMean = groupArrays.SelectMany(g => g).Average();

        // Calculate SSB (between groups sum of squares)
        double ssb = 0;
        foreach (var group in groupArrays)
        {
            double groupMean = group.Average();
            ssb += group.Length * Math.Pow(groupMean - grandMean, 2);
        }

        // Calculate SSW (within groups sum of squares)
        double ssw = 0;
        foreach (var group in groupArrays)
        {
            double groupMean = group.Average();
            ssw += group.Sum(x => Math.Pow(x - groupMean, 2));
        }

        // Degrees of freedom
        double dfb = groupArrays.Length - 1;
        double dfw = totalN - groupArrays.Length;

        // Mean squares
        double msb = ssb / dfb;
        double msw = ssw / dfw;

        // F-statistic
        double fStatistic = msb / msw;

        // P-value using F-distribution
        var fDist = new FisherSnedecor(dfb, dfw);
        double pValue = 1 - fDist.CumulativeDistribution(fStatistic);

        bool isSignificant = pValue < significanceLevel;

        string interpretation = isSignificant
            ? $"Significant differences between group means (p = {pValue:F4})"
            : $"No significant differences between group means (p = {pValue:F4})";

        return new StatisticalTest
        {
            TestName = "One-Way ANOVA",
            TestType = "ANOVA",
            TestStatistic = fStatistic,
            PValue = pValue,
            SignificanceLevel = significanceLevel,
            IsSignificant = isSignificant,
            NullHypothesis = "All group means are equal",
            AlternativeHypothesis = "At least one group mean is different",
            Parameters = new Dictionary<string, double>
            {
                ["SSB"] = ssb,
                ["SSW"] = ssw,
                ["DFB"] = dfb,
                ["DFW"] = dfw,
                ["MSB"] = msb,
                ["MSW"] = msw,
                ["TotalN"] = totalN,
                ["Groups"] = groupArrays.Length
            },
            Interpretation = interpretation
        };
    }

    /// <summary>
    /// Performs chi-square test for independence
    /// </summary>
    public StatisticalTest PerformChiSquareTest(double[,] contingencyTable, double significanceLevel = 0.05)
    {
        int rows = contingencyTable.GetLength(0);
        int cols = contingencyTable.GetLength(1);

        // Calculate row and column totals
        double[] rowTotals = new double[rows];
        double[] colTotals = new double[cols];
        double grandTotal = 0;

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                rowTotals[i] += contingencyTable[i, j];
                colTotals[j] += contingencyTable[i, j];
                grandTotal += contingencyTable[i, j];
            }
        }

        // Calculate expected frequencies and chi-square statistic
        double chiSquare = 0;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                double expected = (rowTotals[i] * colTotals[j]) / grandTotal;
                if (expected > 0)
                {
                    chiSquare += Math.Pow(contingencyTable[i, j] - expected, 2) / expected;
                }
            }
        }

        // Degrees of freedom
        double df = (rows - 1) * (cols - 1);

        // P-value using chi-square distribution
        var chiDist = new ChiSquared(df);
        double pValue = 1 - chiDist.CumulativeDistribution(chiSquare);

        bool isSignificant = pValue < significanceLevel;

        string interpretation = isSignificant
            ? $"Significant association between variables (p = {pValue:F4})"
            : $"No significant association between variables (p = {pValue:F4})";

        return new StatisticalTest
        {
            TestName = "Chi-Square Test of Independence",
            TestType = "Chi-Square",
            TestStatistic = chiSquare,
            PValue = pValue,
            SignificanceLevel = significanceLevel,
            IsSignificant = isSignificant,
            NullHypothesis = "Variables are independent",
            AlternativeHypothesis = "Variables are associated",
            Parameters = new Dictionary<string, double>
            {
                ["DegreesOfFreedom"] = df,
                ["Rows"] = rows,
                ["Columns"] = cols,
                ["GrandTotal"] = grandTotal
            },
            Interpretation = interpretation
        };
    }

    /// <summary>
    /// Performs Mann-Whitney U test (non-parametric)
    /// </summary>
    public StatisticalTest PerformMannWhitneyTest(IEnumerable<double> sample1, IEnumerable<double> sample2,
        double significanceLevel = 0.05)
    {
        var s1 = sample1.ToArray();
        var s2 = sample2.ToArray();

        if (s1.Length < 1 || s2.Length < 1)
            throw new ArgumentException("Each sample must have at least 1 observation");

        // Combine and rank all observations
        var allData = s1.Concat(s2).ToArray();
        var ranks = allData.Select((value, index) => new { Value = value, Index = index })
                          .OrderBy(x => x.Value)
                          .Select((x, rank) => new { x.Index, Rank = rank + 1 })
                          .ToArray();

        // Calculate U statistic for sample 1
        double u1 = 0;
        for (int i = 0; i < s1.Length; i++)
        {
            var rank = ranks.First(r => r.Index == i).Rank;
            u1 += rank;
        }
        u1 -= (s1.Length * (s1.Length + 1)) / 2;

        // U statistic for sample 2
        double u2 = s2.Length * s1.Length - u1;
        double uStatistic = Math.Min(u1, u2);

        // Calculate z-score approximation for large samples
        double meanU = (s1.Length * s2.Length) / 2.0;
        double varU = (s1.Length * s2.Length * (s1.Length + s2.Length + 1)) / 12.0;
        double zStatistic = (uStatistic - meanU) / Math.Sqrt(varU);

        // P-value using normal distribution
        var normalDist = new Normal(0, 1);
        double pValue = 2 * (1 - normalDist.CumulativeDistribution(Math.Abs(zStatistic)));

        bool isSignificant = pValue < significanceLevel;

        string interpretation = isSignificant
            ? $"Significant difference between groups (p = {pValue:F4})"
            : $"No significant difference between groups (p = {pValue:F4})";

        return new StatisticalTest
        {
            TestName = "Mann-Whitney U Test",
            TestType = "Non-parametric",
            TestStatistic = uStatistic,
            PValue = pValue,
            SignificanceLevel = significanceLevel,
            IsSignificant = isSignificant,
            NullHypothesis = "Distributions are identical",
            AlternativeHypothesis = "Distributions are different",
            Parameters = new Dictionary<string, double>
            {
                ["SampleSize1"] = s1.Length,
                ["SampleSize2"] = s2.Length,
                ["U1"] = u1,
                ["U2"] = u2,
                ["ZStatistic"] = zStatistic
            },
            Interpretation = interpretation
        };
    }

    /// <summary>
    /// Performs power analysis for t-test
    /// </summary>
    public PowerAnalysis PerformPowerAnalysis(double effectSize, int sampleSize,
        double significanceLevel = 0.05, string testType = "two-sample")
    {
        // Simplified power analysis using approximation
        // Power = 1 - β, where β is the probability of Type II error

        // For two-sample t-test, approximate power using normal distribution
        double criticalValue = StudentT.InvCDF(0, 1, 2 * (sampleSize - 1), 1 - significanceLevel / 2);
        double nonCentralityParameter = effectSize * Math.Sqrt(sampleSize / 2.0);

        // Approximate power using normal distribution
        double beta = 1 - Normal.CDF(criticalValue - nonCentralityParameter, 0, 1);
        double power = 1 - beta;

        return new PowerAnalysis
        {
            EffectSize = effectSize,
            SampleSize = sampleSize,
            Power = Math.Max(0, Math.Min(1, power)), // Clamp between 0 and 1
            SignificanceLevel = significanceLevel,
            TestType = testType,
            Parameters = new Dictionary<string, double>
            {
                ["CriticalValue"] = criticalValue,
                ["NonCentralityParameter"] = nonCentralityParameter,
                ["Beta"] = beta
            }
        };
    }
}