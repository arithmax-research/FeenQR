using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Distributions;

namespace QuantResearchAgent.Services;

public class CointegrationAnalysisService
{
    private readonly ILogger<CointegrationAnalysisService> _logger;

    public CointegrationAnalysisService(ILogger<CointegrationAnalysisService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs Engle-Granger cointegration test
    /// </summary>
    public EngleGrangerResult PerformEngleGrangerTest(IEnumerable<double> series1, IEnumerable<double> series2, int maxLags = 10)
    {
        try
        {
            var y = DenseVector.OfArray(series1.ToArray());
            var x = DenseVector.OfArray(series2.ToArray());

            if (y.Count != x.Count || y.Count < 10)
                throw new ArgumentException("Series must have same length and at least 10 observations");

            // Step 1: Test for unit roots in individual series (simplified - assume already tested)

            // Step 2: Estimate cointegration regression: y_t = α + β*x_t + ε_t
            var X = DenseMatrix.Create(y.Count, 2, (i, j) => j == 0 ? 1.0 : x[i]);
            var XtX = X.Transpose() * X;
            var XtXInv = XtX.Inverse();
            var Xty = X.Transpose() * y;
            var beta = XtXInv * Xty;

            // Calculate residuals
            var residuals = y - X * beta;

            // Step 3: Test for unit root in residuals using ADF test
            var adfResult = PerformADFForResiduals(residuals.ToArray(), maxLags);

            // Critical values for Engle-Granger test (simplified)
            var criticalValues = new Dictionary<double, double>
            {
                [0.01] = -4.32,
                [0.05] = -3.78,
                [0.10] = -3.50
            };

            var isCointegrated = adfResult.TestStatistic < criticalValues[0.05];

            return new EngleGrangerResult
            {
                Symbols = new List<string> { "Series1", "Series2" },
                TestStatistic = adfResult.TestStatistic,
                CriticalValues = criticalValues,
                IsCointegrated = isCointegrated,
                CointegrationVector = new double[] { beta[0], beta[1] },
                ResidualVariance = residuals.DotProduct(residuals) / (residuals.Count - 2)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing Engle-Granger test");
            throw new Exception($"Engle-Granger test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs Johansen cointegration test
    /// </summary>
    public JohansenResult PerformJohansenTest(IEnumerable<IEnumerable<double>> series, int lagOrder = 1)
    {
        try
        {
            var seriesList = series.Select(s => s.ToArray()).ToList();
            var n = seriesList.Count; // number of variables
            var T = seriesList[0].Length; // number of observations

            if (seriesList.Any(s => s.Length != T) || T < 20)
                throw new ArgumentException("All series must have same length and at least 20 observations");

            // Create data matrix (T x n)
            var Y = DenseMatrix.Create(T, n, (i, j) => seriesList[j][i]);

            // Calculate differences
            var dY = DenseMatrix.Create(T - 1, n, (i, j) => Y[i + 1, j] - Y[i, j]);

            // Create lagged levels matrix
            var YLag = Y.SubMatrix(0, T - 1, 0, n);

            // Create differenced lagged levels for VECM
            var dYLag = DenseMatrix.Create(T - lagOrder - 1, n * lagOrder, 0.0);
            for (int lag = 1; lag <= lagOrder; lag++)
            {
                var temp = Y.SubMatrix(lag, T - lag, 0, n);
                for (int i = 0; i < temp.RowCount; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        dYLag[i, (lag - 1) * n + j] = temp[i, j] - Y[i + lag - 1, j];
                    }
                }
            }

            // Adjust matrices for the lag order
            var dY_adj = dY.SubMatrix(lagOrder, dY.RowCount - lagOrder, 0, n);
            var YLag_adj = YLag.SubMatrix(lagOrder, YLag.RowCount - lagOrder, 0, n);

            // Johansen test setup
            var Z0 = dY_adj; // (T-k-lagOrder) x n
            var Z1 = YLag_adj; // (T-k-lagOrder) x n

            // Include deterministic terms if needed (simplified - no constant/deterministic terms)
            var R = n; // rank to test

            // Calculate canonical correlations
            var S00 = (Z0.Transpose() * Z0) / Z0.RowCount;
            var S01 = (Z0.Transpose() * Z1) / Z0.RowCount;
            var S10 = (Z1.Transpose() * Z0) / Z0.RowCount;
            var S11 = (Z1.Transpose() * Z1) / Z0.RowCount;

            // Solve generalized eigenvalue problem
            var S = S10 * S00.Inverse() * S01;
            var eigen = S.Evd();

            var eigenvalues = eigen.EigenValues.Real().ToArray();
            Array.Sort(eigenvalues);
            Array.Reverse(eigenvalues);

            // Calculate trace statistics
            var traceStats = new double[R];
            var maxEigenStats = new double[R];

            for (int r = 0; r < R; r++)
            {
                // Trace statistic
                var traceSum = 0.0;
                for (int i = r; i < eigenvalues.Length; i++)
                {
                    traceSum += Math.Log(1 - eigenvalues[i]);
                }
                traceStats[r] = -Z0.RowCount * traceSum;

                // Max eigenvalue statistic
                if (r < eigenvalues.Length)
                {
                    maxEigenStats[r] = -Z0.RowCount * Math.Log(1 - eigenvalues[r]);
                }
            }

            // Critical values (simplified - these are approximate)
            var criticalValues = new Dictionary<double, double[]>
            {
                [0.05] = new double[] { 25.32, 18.17, 12.25 }, // for trace test
                [0.01] = new double[] { 30.45, 22.00, 15.67 }
            };

            // Determine cointegration rank
            var rank = 0;
            for (int i = 0; i < traceStats.Length; i++)
            {
                if (traceStats[i] > criticalValues[0.05][i])
                {
                    rank = i + 1;
                }
                else
                {
                    break;
                }
            }

            // Calculate cointegration vectors (simplified)
            var cointegrationVectors = new double[rank][];
            for (int i = 0; i < rank; i++)
            {
                cointegrationVectors[i] = new double[n];
                // This is a simplified calculation - real implementation would be more complex
                for (int j = 0; j < n; j++)
                {
                    cointegrationVectors[i][j] = (j == i) ? 1.0 : 0.0;
                }
            }

            return new JohansenResult
            {
                Symbols = Enumerable.Range(1, n).Select(i => $"Series{i}").ToList(),
                Rank = rank,
                Eigenvalues = eigenvalues,
                TraceStatistics = traceStats,
                MaxEigenvalueStatistics = maxEigenStats,
                CriticalValues = criticalValues,
                CointegrationVectors = cointegrationVectors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing Johansen test");
            throw new Exception($"Johansen test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs Granger causality test
    /// </summary>
    public GrangerCausalityResult PerformGrangerCausalityTest(IEnumerable<double> causeSeries, IEnumerable<double> effectSeries, int lagOrder = 5)
    {
        try
        {
            var x = causeSeries.ToArray(); // potential cause
            var y = effectSeries.ToArray(); // effect

            if (x.Length != y.Length || x.Length < lagOrder + 10)
                throw new ArgumentException("Series must have same length and sufficient observations");

            // Fit unrestricted model: y_t = α + ∑β_i*y_{t-i} + ∑γ_j*x_{t-j} + ε_t
            var unrestrictedSSE = FitVARModel(y, x, lagOrder);

            // Fit restricted model: y_t = α + ∑β_i*y_{t-i} + ε_t
            var restrictedSSE = FitVARModel(y, null, lagOrder);

            // Calculate F-statistic
            var n = y.Length - lagOrder;
            var k = lagOrder; // number of restrictions (x coefficients)
            var fStat = ((restrictedSSE - unrestrictedSSE) / k) / (unrestrictedSSE / (n - 2 * lagOrder - 1));

            // Calculate p-value (simplified approximation using normal distribution)
            var pValue = 2.0 * (1.0 - Normal.CDF(0, 1, Math.Abs(fStat))); // Rough approximation

            var grangerCauses = pValue < 0.05;

            return new GrangerCausalityResult
            {
                CauseSymbol = "CauseSeries",
                EffectSymbol = "EffectSeries",
                LagOrder = lagOrder,
                FStatistic = fStat,
                PValue = pValue,
                GrangerCauses = grangerCauses,
                SignificanceLevel = 0.05
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing Granger causality test");
            throw new Exception($"Granger causality test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes lead-lag relationships between two time series
    /// </summary>
    public LeadLagResult AnalyzeLeadLagRelationship(IEnumerable<double> series1, IEnumerable<double> series2, int maxLag = 10)
    {
        try
        {
            var x = series1.ToArray();
            var y = series2.ToArray();

            if (x.Length != y.Length || x.Length < maxLag + 10)
                throw new ArgumentException("Series must have same length and sufficient observations");

            // Calculate cross-correlations at different lags
            var crossCorrelations = new double[2 * maxLag + 1];

            for (int lag = -maxLag; lag <= maxLag; lag++)
            {
                var correlation = 0.0;
                var count = 0;

                if (lag >= 0)
                {
                    // x leads y
                    var xLag = x.Skip(lag);
                    var ySubset = y.Take(y.Length - lag);
                    correlation = Correlation.Pearson(xLag, ySubset);
                }
                else
                {
                    // y leads x
                    var yLag = y.Skip(-lag);
                    var xSubset = x.Take(x.Length + lag);
                    correlation = Correlation.Pearson(xSubset, yLag);
                }

                crossCorrelations[lag + maxLag] = correlation;
                count++;
            }

            // Find optimal lag
            var maxCorr = crossCorrelations.Max();
            var maxCorrIndex = Array.IndexOf(crossCorrelations, maxCorr);
            var optimalLag = maxCorrIndex - maxLag;

            var symbol1Leads = optimalLag > 0;

            return new LeadLagResult
            {
                Symbol1 = "Series1",
                Symbol2 = "Series2",
                OptimalLag = optimalLag,
                CrossCorrelation = maxCorr,
                Symbol1Leads = symbol1Leads,
                LagPeriods = Math.Abs(optimalLag),
                CrossCorrelations = crossCorrelations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing lead-lag relationship");
            throw new Exception($"Lead-lag analysis failed: {ex.Message}");
        }
    }

    // Helper methods
    private StationarityTestResult PerformADFForResiduals(double[] residuals, int maxLags)
    {
        // Simplified ADF test for residuals
        var dy = new List<double>();
        for (int i = 1; i < residuals.Length; i++)
        {
            dy.Add(residuals[i] - residuals[i - 1]);
        }

        var yLag = residuals.Skip(1).Take(dy.Count).ToArray();
        var dyArray = dy.ToArray();

        // Simple regression: dy = γ*y_{t-1} + ε
        var gamma = Correlation.Pearson(dyArray, yLag);
        var se = Math.Sqrt((1 - gamma * gamma) / (dyArray.Length - 2));

        var testStat = gamma / se;

        return new StationarityTestResult
        {
            TestType = "ADF",
            TestStatistic = testStat,
            CriticalValues = new Dictionary<double, double>
            {
                [0.01] = -3.43,
                [0.05] = -2.86,
                [0.10] = -2.57
            },
            IsStationary = testStat < -2.86,
            LagOrder = 0
        };
    }

    private double FitVARModel(double[] y, double[]? x, int lagOrder)
    {
        var n = y.Length - lagOrder;
        var k = lagOrder + (x != null ? lagOrder : 0) + 1; // intercept + lags

        var X = DenseMatrix.Create(n, k, 0.0);
        var Y = DenseVector.OfArray(y.Skip(lagOrder).ToArray());

        // Fill design matrix
        for (int i = 0; i < n; i++)
        {
            X[i, 0] = 1.0; // intercept

            // y lags
            for (int j = 1; j <= lagOrder; j++)
            {
                X[i, j] = y[i + lagOrder - j];
            }

            // x lags (if provided)
            if (x != null)
            {
                for (int j = 1; j <= lagOrder; j++)
                {
                    X[i, lagOrder + j] = x[i + lagOrder - j];
                }
            }
        }

        // OLS estimation
        var XtX = X.Transpose() * X;
        var XtXInv = XtX.Inverse();
        var Xty = X.Transpose() * Y;
        var beta = XtXInv * Xty;

        // Calculate residuals
        var residuals = Y - X * beta;
        var sse = residuals.DotProduct(residuals);

        return sse;
    }
}