using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class AnomalyDetectionService
    {
        private readonly MarketDataService _marketDataService;
        private readonly Kernel _kernel;

        public AnomalyDetectionService(MarketDataService marketDataService, Kernel kernel)
        {
            _marketDataService = marketDataService;
            _kernel = kernel;
        }

        // Detects anomalies in market data
        public async Task<string> DetectAnomaliesAsync(object marketData)
        {
            try
            {
                var data = marketData as Dictionary<string, object> ?? new Dictionary<string, object>();
                var symbol = data.GetValueOrDefault("symbol", "SPY").ToString();
                var lookbackDays = Convert.ToInt32(data.GetValueOrDefault("lookbackDays", 252));

                var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol, lookbackDays);
                if (historicalData == null || !historicalData.Any())
                    return $"No data available for anomaly detection in {symbol}";

                var anomalies = await DetectMultipleAnomalyTypesAsync(historicalData);
                return FormatAnomalyReport(anomalies, symbol);
            }
            catch (Exception ex)
            {
                return $"Error detecting anomalies: {ex.Message}";
            }
        }

        public async Task<List<Anomaly>> DetectMultipleAnomalyTypesAsync(List<MarketData> data)
        {
            var anomalies = new List<Anomaly>();

            // Price anomalies
            anomalies.AddRange(await DetectPriceAnomaliesAsync(data));

            // Volume anomalies
            anomalies.AddRange(await DetectVolumeAnomaliesAsync(data));

            // Return anomalies
            anomalies.AddRange(await DetectReturnAnomaliesAsync(data));

            // Volatility anomalies
            anomalies.AddRange(await DetectVolatilityAnomaliesAsync(data));

            // Pattern-based anomalies
            anomalies.AddRange(await DetectPatternAnomaliesAsync(data));

            return anomalies.OrderByDescending(a => a.Severity).ToList();
        }

        private async Task<List<Anomaly>> DetectPriceAnomaliesAsync(List<MarketData> data)
        {
            var anomalies = new List<Anomaly>();
            var prices = data.Select(d => d.Price).ToList();

            // Z-score based anomaly detection
            var mean = prices.Average();
            var std = prices.StandardDeviation();

            for (int i = 0; i < prices.Count; i++)
            {
                var zScore = Math.Abs((prices[i] - mean) / std);
                if (zScore > 3.0) // 3 standard deviations
                {
                    anomalies.Add(new Anomaly
                    {
                        Type = AnomalyType.PriceSpike,
                        Date = data[i].Timestamp,
                        Value = prices[i],
                        ExpectedValue = mean,
                        ZScore = zScore,
                        Severity = CalculateSeverity(zScore),
                        Description = $"Price spike detected: {prices[i]:F2} vs expected {mean:F2}"
                    });
                }
            }

            return anomalies;
        }

        private async Task<List<Anomaly>> DetectVolumeAnomaliesAsync(List<MarketData> data)
        {
            var anomalies = new List<Anomaly>();
            var volumes = data.Select(d => d.Volume).ToList();

            // Moving average comparison
            var ma20 = CalculateMovingAverage(volumes, 20);

            for (int i = 20; i < volumes.Count; i++)
            {
                var ratio = volumes[i] / ma20[i - 20];
                if (ratio > 3.0) // 3x average volume
                {
                    anomalies.Add(new Anomaly
                    {
                        Type = AnomalyType.VolumeSpike,
                        Date = data[i].Timestamp,
                        Value = volumes[i],
                        ExpectedValue = ma20[i - 20],
                        ZScore = Math.Log(ratio),
                        Severity = CalculateSeverity(Math.Log(ratio)),
                        Description = $"Volume spike: {volumes[i]:N0} vs average {ma20[i - 20]:N0}"
                    });
                }
            }

            return anomalies;
        }

        private async Task<List<Anomaly>> DetectReturnAnomaliesAsync(List<MarketData> data)
        {
            var anomalies = new List<Anomaly>();
            var returns = CalculateReturns(data.Select(d => d.Price).ToList());

            // Statistical outlier detection
            var q1 = returns.Quantile(0.25);
            var q3 = returns.Quantile(0.75);
            var iqr = q3 - q1;
            var lowerBound = q1 - 1.5 * iqr;
            var upperBound = q3 + 1.5 * iqr;

            for (int i = 0; i < returns.Count; i++)
            {
                if (returns[i] < lowerBound || returns[i] > upperBound)
                {
                    anomalies.Add(new Anomaly
                    {
                        Type = AnomalyType.ReturnOutlier,
                        Date = data[i + 1].Timestamp, // Returns are offset by 1
                        Value = returns[i],
                        ExpectedValue = returns.Median(),
                        ZScore = Math.Abs(returns[i] - returns.Median()) / returns.StandardDeviation(),
                        Severity = CalculateSeverity(Math.Abs(returns[i])),
                        Description = $"{(returns[i] > 0 ? "Positive" : "Negative")} return outlier: {returns[i]:P2}"
                    });
                }
            }

            return anomalies;
        }

        private async Task<List<Anomaly>> DetectVolatilityAnomaliesAsync(List<MarketData> data)
        {
            var anomalies = new List<Anomaly>();

            // Rolling volatility calculation
            var returns = CalculateReturns(data.Select(d => d.Price).ToList());
            var rollingVolatility = CalculateRollingVolatility(returns, 20);

            for (int i = 20; i < rollingVolatility.Count; i++)
            {
                var avgVol = rollingVolatility.Take(i).Average();
                var ratio = rollingVolatility[i] / avgVol;

                if (ratio > 2.0) // 2x average volatility
                {
                    anomalies.Add(new Anomaly
                    {
                        Type = AnomalyType.VolatilitySpike,
                        Date = data[i + 20].Timestamp,
                        Value = rollingVolatility[i],
                        ExpectedValue = avgVol,
                        ZScore = Math.Log(ratio),
                        Severity = CalculateSeverity(Math.Log(ratio)),
                        Description = $"Volatility spike: {rollingVolatility[i]:P2} vs average {avgVol:P2}"
                    });
                }
            }

            return anomalies;
        }

        private async Task<List<Anomaly>> DetectPatternAnomaliesAsync(List<MarketData> data)
        {
            var anomalies = new List<Anomaly>();

            // Gap detection
            for (int i = 1; i < data.Count; i++)
            {
                var prevClose = data[i - 1].Price;
                var open = data[i].Price;
                var gapPercent = Math.Abs((open - prevClose) / prevClose);

                if (gapPercent > 0.05) // 5% gap
                {
                    anomalies.Add(new Anomaly
                    {
                        Type = AnomalyType.Gap,
                        Date = data[i].Timestamp,
                        Value = gapPercent,
                        ExpectedValue = 0.01, // Typical small movement
                        ZScore = gapPercent / 0.01,
                        Severity = gapPercent > 0.10 ? AnomalySeverity.High : AnomalySeverity.Medium,
                        Description = $"{(open > prevClose ? "Upward" : "Downward")} gap: {gapPercent:P2}"
                    });
                }
            }

            return anomalies;
        }

        private List<double> CalculateReturns(List<double> prices)
        {
            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                returns.Add((prices[i] - prices[i - 1]) / prices[i - 1]);
            }
            return returns;
        }

        private List<double> CalculateMovingAverage(List<double> data, int period)
        {
            var ma = new List<double>();
            for (int i = period - 1; i < data.Count; i++)
            {
                ma.Add(data.Skip(i - period + 1).Take(period).Average());
            }
            return ma;
        }

        private List<double> CalculateRollingVolatility(List<double> returns, int window)
        {
            var volatility = new List<double>();
            for (int i = window; i <= returns.Count; i++)
            {
                var windowReturns = returns.Skip(i - window).Take(window).ToList();
                volatility.Add(windowReturns.StandardDeviation());
            }
            return volatility;
        }

        private AnomalySeverity CalculateSeverity(double zScore)
        {
            if (zScore > 4.0) return AnomalySeverity.Critical;
            if (zScore > 3.0) return AnomalySeverity.High;
            if (zScore > 2.0) return AnomalySeverity.Medium;
            return AnomalySeverity.Low;
        }

        private string FormatAnomalyReport(List<Anomaly> anomalies, string symbol)
        {
            var result = new System.Text.StringBuilder();

            result.AppendLine($"Anomaly Detection Report for {symbol}");
            result.AppendLine($"Total Anomalies Detected: {anomalies.Count}");
            result.AppendLine();

            if (!anomalies.Any())
            {
                result.AppendLine("No significant anomalies detected in the analyzed period.");
                return result.ToString();
            }

            // Group by severity
            var bySeverity = anomalies.GroupBy(a => a.Severity);

            foreach (var group in bySeverity.OrderByDescending(g => (int)g.Key))
            {
                result.AppendLine($"{group.Key} Severity Anomalies: {group.Count()}");
                foreach (var anomaly in group.OrderByDescending(a => a.ZScore))
                {
                    result.AppendLine($"  {anomaly.Date:yyyy-MM-dd}: {anomaly.Description}");
                    result.AppendLine($"    Z-Score: {anomaly.ZScore:F2}, Value: {anomaly.Value:F4}");
                }
                result.AppendLine();
            }

            // Summary statistics
            result.AppendLine("Anomaly Summary:");
            result.AppendLine($"- Most Common Type: {anomalies.GroupBy(a => a.Type).OrderByDescending(g => g.Count()).First().Key}");
            result.AppendLine($"- Average Z-Score: {anomalies.Average(a => a.ZScore):F2}");
            result.AppendLine($"- Date Range: {anomalies.Min(a => a.Date):yyyy-MM-dd} to {anomalies.Max(a => a.Date):yyyy-MM-dd}");

            return result.ToString();
        }

        public async Task<List<AnomalyCluster>> ClusterAnomaliesAsync(List<Anomaly> anomalies)
        {
            // Simple clustering by date proximity and type
            var clusters = new List<AnomalyCluster>();
            var sorted = anomalies.OrderBy(a => a.Date).ToList();

            AnomalyCluster currentCluster = null;

            foreach (var anomaly in sorted)
            {
                if (currentCluster == null ||
                    (anomaly.Date - currentCluster.EndDate).TotalDays > 5 ||
                    anomaly.Type != currentCluster.Anomalies.First().Type)
                {
                    currentCluster = new AnomalyCluster
                    {
                        StartDate = anomaly.Date,
                        EndDate = anomaly.Date,
                        Anomalies = new List<Anomaly>(),
                        Severity = anomaly.Severity
                    };
                    clusters.Add(currentCluster);
                }

                currentCluster.Anomalies.Add(anomaly);
                currentCluster.EndDate = anomaly.Date;
            }

            return clusters.Where(c => c.Anomalies.Count > 1).ToList();
        }
    }

    public enum AnomalyType
    {
        PriceSpike,
        VolumeSpike,
        ReturnOutlier,
        VolatilitySpike,
        Gap,
        PatternAnomaly
    }

    public enum AnomalySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class Anomaly
    {
        public AnomalyType Type { get; set; }
        public DateTime Date { get; set; }
        public double Value { get; set; }
        public double ExpectedValue { get; set; }
        public double ZScore { get; set; }
        public AnomalySeverity Severity { get; set; }
        public required string Description { get; set; }
    }

    public class AnomalyCluster
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public required List<Anomaly> Anomalies { get; set; }
        public AnomalySeverity Severity { get; set; }
    }
}
