using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace QuantResearchAgent.Services;

public class TimeSeriesAnalysisService
{
    private readonly ILogger<TimeSeriesAnalysisService> _logger;

    public TimeSeriesAnalysisService(ILogger<TimeSeriesAnalysisService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs Augmented Dickey-Fuller (ADF) stationarity test
    /// </summary>
    public StationarityTestResult PerformADFTest(IEnumerable<double> data, int maxLags = 10)
    {
        try
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < 10)
                throw new ArgumentException("Need at least 10 data points for ADF test");

            // Calculate optimal lag length using AIC
            var optimalLag = CalculateOptimalLag(dataArray, maxLags);

            // Perform ADF regression: Δy_t = α + β*t + γ*y_{t-1} + ∑δ_i*Δy_{t-i} + ε_t
            var y = DenseVector.OfArray(dataArray);
            var dy = DenseVector.OfArray(Difference(dataArray).ToArray());

            // Create lagged differences
            var laggedDiffs = new List<double[]>();
            for (int lag = 1; lag <= optimalLag; lag++)
            {
                laggedDiffs.Add(Difference(dataArray, lag).Skip(lag).ToArray());
            }

            // Create design matrix
            var n = dy.Count;
            var X = DenseMatrix.Create(n, optimalLag + 2, 0.0); // intercept, trend, lagged diffs

            for (int i = 0; i < n; i++)
            {
                X[i, 0] = 1.0; // intercept
                X[i, 1] = i + 1; // trend
                for (int lag = 0; lag < optimalLag; lag++)
                {
                    if (i < laggedDiffs[lag].Length)
                        X[i, lag + 2] = laggedDiffs[lag][i];
                }
            }

            // Dependent variable (y_{t-1})
            var yLag = DenseVector.OfArray(dataArray.Skip(1).Take(n).ToArray());

            // Perform OLS regression
            var XtX = X.Transpose() * X;
            var XtXInv = XtX.Inverse();
            var Xty = X.Transpose() * yLag;
            var beta = XtXInv * Xty;

            // Calculate residuals and standard errors
            var residuals = yLag - X * beta;
            var sigma2 = residuals.DotProduct(residuals) / (n - optimalLag - 2);
            var se = sigma2 * XtXInv.Diagonal();

            var gamma = beta[optimalLag + 1]; // coefficient of y_{t-1}
            var seGamma = Math.Sqrt(se[optimalLag + 1]);

            // ADF test statistic
            var adfStatistic = gamma / seGamma;

            // Critical values (approximate for large samples)
            var criticalValues = new Dictionary<double, double>
            {
                [0.01] = -3.43,
                [0.05] = -2.86,
                [0.10] = -2.57
            };

            var isStationary = adfStatistic < criticalValues[0.05];

            return new StationarityTestResult
            {
                TestType = "ADF",
                TestStatistic = adfStatistic,
                CriticalValues = criticalValues,
                IsStationary = isStationary,
                LagOrder = optimalLag,
                SignificanceLevel = 0.05
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing ADF test");
            throw new Exception($"ADF test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs KPSS stationarity test
    /// </summary>
    public StationarityTestResult PerformKPSSTest(IEnumerable<double> data, string trendType = "c")
    {
        try
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < 10)
                throw new ArgumentException("Need at least 10 data points for KPSS test");

            // Calculate optimal lag length
            var lag = Math.Max(1, (int)Math.Ceiling(Math.Pow(dataArray.Length / 100.0, 1.0 / 4.0)));

            // Detrend the series based on trend type
            var detrended = DetrendSeries(dataArray, trendType);

            // Calculate cumulative sum of residuals
            var cumsum = new List<double> { 0 };
            for (int i = 1; i < detrended.Length; i++)
            {
                cumsum.Add(cumsum[i - 1] + detrended[i]);
            }

            // Calculate S_t^2 (cumulative sum squared)
            var s2 = cumsum.Select(x => x * x / detrended.Length).Sum();

            // Calculate η (autocovariance estimate)
            var eta = 0.0;
            for (int l = 1; l <= lag; l++)
            {
                var weight = 1.0 - (l / (lag + 1.0));
                var autocov = 0.0;
                for (int t = l; t < detrended.Length; t++)
                {
                    autocov += detrended[t] * detrended[t - l];
                }
                autocov /= detrended.Length;
                eta += weight * autocov;
            }

            // KPSS test statistic
            var kpssStatistic = s2 / (detrended.Length * detrended.Length) / (eta + 1e-8);

            // Critical values
            var criticalValues = new Dictionary<double, double>
            {
                [0.01] = trendType == "c" ? 0.739 : 0.216,
                [0.05] = trendType == "c" ? 0.463 : 0.146,
                [0.10] = trendType == "c" ? 0.347 : 0.119
            };

            var isStationary = kpssStatistic < criticalValues[0.05];

            return new StationarityTestResult
            {
                TestType = "KPSS",
                TestStatistic = kpssStatistic,
                CriticalValues = criticalValues,
                IsStationary = isStationary,
                LagOrder = lag,
                SignificanceLevel = 0.05
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing KPSS test");
            throw new Exception($"KPSS test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates autocorrelation function (ACF)
    /// </summary>
    public AutocorrelationResult CalculateAutocorrelation(IEnumerable<double> data, int maxLags = 20)
    {
        try
        {
            var dataArray = data.ToArray();
            var autocorrelations = new List<double>();
            var pacf = new List<double>();

            // Calculate ACF
            for (int lag = 1; lag <= Math.Min(maxLags, dataArray.Length / 4); lag++)
            {
                var correlation = Correlation.Pearson(dataArray.Skip(lag), dataArray.Take(dataArray.Length - lag));
                autocorrelations.Add(correlation);
            }

            // Calculate PACF using Yule-Walker equations
            pacf = CalculatePACF(dataArray, maxLags);

            return new AutocorrelationResult
            {
                Autocorrelations = autocorrelations,
                PartialAutocorrelations = pacf,
                MaxLags = maxLags,
                LjungBoxStatistic = CalculateLjungBoxStatistic(autocorrelations, dataArray.Length)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating autocorrelation");
            throw new Exception($"Autocorrelation calculation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs seasonal decomposition using moving averages
    /// </summary>
    public SeasonalDecompositionResult PerformSeasonalDecomposition(IEnumerable<double> data, int seasonalPeriod)
    {
        try
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < seasonalPeriod * 2)
                throw new ArgumentException("Need at least 2 seasonal periods of data");

            // Calculate trend using moving average
            var trend = CalculateMovingAverageTrend(dataArray, seasonalPeriod);

            // Calculate seasonal component
            var seasonal = CalculateSeasonalComponent(dataArray, trend, seasonalPeriod);

            // Calculate residual
            var residual = new double[dataArray.Length];
            for (int i = 0; i < dataArray.Length; i++)
            {
                residual[i] = dataArray[i] - trend[i] - seasonal[i % seasonalPeriod];
            }

            return new SeasonalDecompositionResult
            {
                OriginalData = dataArray,
                Trend = trend,
                Seasonal = seasonal,
                Residual = residual,
                SeasonalPeriod = seasonalPeriod
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing seasonal decomposition");
            throw new Exception($"Seasonal decomposition failed: {ex.Message}");
        }
    }

    // Helper methods
    private int CalculateOptimalLag(double[] data, int maxLags)
    {
        var minAIC = double.MaxValue;
        var optimalLag = 1;

        for (int lag = 1; lag <= Math.Min(maxLags, data.Length / 4); lag++)
        {
            try
            {
                var aic = CalculateAIC(data, lag);
                if (aic < minAIC)
                {
                    minAIC = aic;
                    optimalLag = lag;
                }
            }
            catch
            {
                continue;
            }
        }

        return optimalLag;
    }

    private double CalculateAIC(double[] data, int lag)
    {
        // Simplified AIC calculation for ADF test
        var n = data.Length - lag - 1;
        return n * Math.Log(2 * Math.PI) + n + 2 * (lag + 2);
    }

    private IEnumerable<double> Difference(double[] data, int lag = 1)
    {
        for (int i = lag; i < data.Length; i++)
        {
            yield return data[i] - data[i - lag];
        }
    }

    private double[] DetrendSeries(double[] data, string trendType)
    {
        var n = data.Length;
        var detrended = new double[n];

        if (trendType == "c") // constant only
        {
            var mean = data.Average();
            for (int i = 0; i < n; i++)
            {
                detrended[i] = data[i] - mean;
            }
        }
        else if (trendType == "ct") // constant and trend
        {
            var x = DenseMatrix.Create(n, 2, (i, j) => j == 0 ? 1.0 : i + 1);
            var y = DenseVector.OfArray(data);
            var beta = (x.Transpose() * x).Inverse() * x.Transpose() * y;

            for (int i = 0; i < n; i++)
            {
                var fitted = beta[0] + beta[1] * (i + 1);
                detrended[i] = data[i] - fitted;
            }
        }

        return detrended;
    }

    private List<double> CalculatePACF(double[] data, int maxLags)
    {
        var pacf = new List<double>();
        var phi = new double[maxLags + 1, maxLags + 1];

        for (int k = 1; k <= Math.Min(maxLags, data.Length / 4); k++)
        {
            // Calculate partial autocorrelation using Durbin-Levinson algorithm
            var r = new double[k + 1];
            for (int i = 0; i <= k; i++)
            {
                r[i] = Correlation.Pearson(data.Skip(i), data.Take(data.Length - i));
            }

            if (k == 1)
            {
                phi[1, 1] = r[1];
            }
            else
            {
                // Get previous phi values
                var prevPhi = new double[k];
                for (int j = 1; j < k; j++)
                {
                    prevPhi[j - 1] = phi[k - 1, j];
                }

                // Get r values
                var rSubset = new double[k];
                for (int j = 0; j < k; j++)
                {
                    rSubset[j] = r[j];
                }

                phi[k, k] = (r[k] - SumProduct(prevPhi, rSubset.Reverse().ToArray())) /
                           (1 - SumProduct(prevPhi, rSubset));

                for (int j = 1; j < k; j++)
                {
                    phi[k, j] = phi[k - 1, j] - phi[k, k] * phi[k - 1, k - j];
                }
            }

            pacf.Add(phi[k, k]);
        }

        return pacf;
    }

    private double CalculateLjungBoxStatistic(List<double> autocorrelations, int n)
    {
        var q = 0.0;
        for (int i = 0; i < autocorrelations.Count; i++)
        {
            var r = autocorrelations[i];
            q += r * r / (n - i - 1);
        }
        q *= n * (n + 2);
        return q;
    }

    private double[] CalculateMovingAverageTrend(double[] data, int period)
    {
        var trend = new double[data.Length];

        for (int i = period / 2; i < data.Length - period / 2; i++)
        {
            var sum = 0.0;
            for (int j = i - period / 2; j <= i + period / 2; j++)
            {
                sum += data[j];
            }
            trend[i] = sum / period;
        }

        // Handle edges with available data
        for (int i = 0; i < period / 2; i++)
        {
            trend[i] = data[i];
        }
        for (int i = data.Length - period / 2; i < data.Length; i++)
        {
            trend[i] = data[i];
        }

        return trend;
    }

    private double[] CalculateSeasonalComponent(double[] data, double[] trend, int period)
    {
        var seasonal = new double[period];
        var counts = new int[period];

        // Calculate seasonal indices
        for (int i = 0; i < data.Length; i++)
        {
            var seasonalIdx = i % period;
            seasonal[seasonalIdx] += data[i] - trend[i];
            counts[seasonalIdx]++;
        }

        // Average the seasonal effects
        for (int i = 0; i < period; i++)
        {
            seasonal[i] /= counts[i];
        }

        // Center the seasonal component
        var seasonalMean = seasonal.Average();
        for (int i = 0; i < period; i++)
        {
            seasonal[i] -= seasonalMean;
        }

        return seasonal;
    }

    private double SumProduct(double[] a, double[] b)
    {
        return a.Zip(b, (x, y) => x * y).Sum();
    }
}