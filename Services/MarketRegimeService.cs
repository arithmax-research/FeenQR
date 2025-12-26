using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class MarketRegimeService
    {
        private readonly MarketDataService _marketDataService;
        private readonly Kernel _kernel;

        public MarketRegimeService(MarketDataService marketDataService, Kernel kernel)
        {
            _marketDataService = marketDataService;
            _kernel = kernel;
        }

        // Detects market regimes using ML models
        public async Task<string> IdentifyMarketRegimeAsync(object marketData)
        {
            try
            {
                // Extract market data
                var data = marketData as Dictionary<string, object> ?? new Dictionary<string, object>();
                var symbol = data.GetValueOrDefault("symbol", "SPY").ToString();
                var lookbackDays = Convert.ToInt32(data.GetValueOrDefault("lookbackDays", 252));

                // Get historical data
                var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol, lookbackDays);
                if (historicalData == null || !historicalData.Any())
                    return $"No historical data available for {symbol}";

                // Calculate regime indicators
                var regime = await DetectRegimeUsingMultipleMethodsAsync(historicalData);

                return FormatRegimeAnalysis(regime, symbol);
            }
            catch (Exception ex)
            {
                return $"Error detecting market regime: {ex.Message}";
            }
        }

        public async Task<MarketRegime> DetectRegimeUsingMultipleMethodsAsync(List<MarketData> data)
        {
            var prices = data.Select(d => d.Price).ToList();
            var returns = CalculateReturns(prices);
            var volumes = data.Select(d => d.Volume).ToList();

            // Method 1: Volatility-based regime detection
            var volatilityRegime = DetectVolatilityRegime(returns);

            // Method 2: Trend-based regime detection
            var trendRegime = DetectTrendRegime(returns);

            // Method 3: Volume-based regime detection
            var volumeRegime = DetectVolumeRegime(volumes);

            // Method 4: ML-based regime detection using clustering
            var mlRegime = await DetectRegimeUsingClusteringAsync(returns);

            // Combine results using voting system
            var regimes = new[] { volatilityRegime, trendRegime, volumeRegime, mlRegime };
            var finalRegime = regimes.GroupBy(r => r)
                                    .OrderByDescending(g => g.Count())
                                    .First().Key;

            return new MarketRegime
            {
                PrimaryRegime = finalRegime,
                Confidence = (double)regimes.Count(r => r == finalRegime) / regimes.Length,
                VolatilityRegime = volatilityRegime,
                TrendRegime = trendRegime,
                VolumeRegime = volumeRegime,
                MLRegime = mlRegime,
                Indicators = CalculateRegimeIndicators(returns, volumes)
            };
        }

        private MarketRegimeType DetectVolatilityRegime(List<double> returns)
        {
            var volatility = returns.StandardDeviation();
            var historicalVolatility = 0.15; // Typical market volatility

            if (volatility > historicalVolatility * 1.5) return MarketRegimeType.HighVolatility;
            if (volatility < historicalVolatility * 0.7) return MarketRegimeType.LowVolatility;
            return MarketRegimeType.NormalVolatility;
        }

        private MarketRegimeType DetectTrendRegime(List<double> returns)
        {
            var sma20 = returns.TakeLast(20).Average();
            var sma50 = returns.TakeLast(50).Average();

            if (sma20 > sma50 * 1.02) return MarketRegimeType.BullMarket;
            if (sma20 < sma50 * 0.98) return MarketRegimeType.BearMarket;
            return MarketRegimeType.Sideways;
        }

        private MarketRegimeType DetectVolumeRegime(List<double> volumes)
        {
            var avgVolume = volumes.Average();
            var recentVolume = volumes.TakeLast(20).Average();

            if (recentVolume > avgVolume * 1.5) return MarketRegimeType.HighVolume;
            if (recentVolume < avgVolume * 0.7) return MarketRegimeType.LowVolume;
            return MarketRegimeType.NormalVolume;
        }

        private async Task<MarketRegimeType> DetectRegimeUsingClusteringAsync(List<double> returns)
        {
            // Simple clustering approach using returns distribution
            var mean = returns.Average();
            var std = returns.StandardDeviation();

            // Classify based on recent performance vs historical
            var recentReturns = returns.TakeLast(20).Average();
            var zScore = (recentReturns - mean) / std;

            if (zScore > 1.5) return MarketRegimeType.BullMarket;
            if (zScore < -1.5) return MarketRegimeType.BearMarket;
            if (Math.Abs(zScore) < 0.5) return MarketRegimeType.Sideways;

            return Math.Sign(zScore) > 0 ? MarketRegimeType.Recovery : MarketRegimeType.Correction;
        }

        private Dictionary<string, double> CalculateRegimeIndicators(List<double> returns, List<double> volumes)
        {
            return new Dictionary<string, double>
            {
                ["Volatility"] = returns.StandardDeviation(),
                ["AverageReturn"] = returns.Average(),
                ["Skewness"] = returns.Skewness(),
                ["Kurtosis"] = returns.Kurtosis(),
                ["VolumeTrend"] = CalculateVolumeTrend(volumes),
                ["Momentum"] = CalculateMomentum(returns),
                ["RSI"] = CalculateRSI(returns)
            };
        }

        private double CalculateVolumeTrend(List<double> volumes)
        {
            if (volumes.Count < 20) return 0;

            var recent = volumes.TakeLast(10).Average();
            var previous = volumes.Skip(volumes.Count - 20).Take(10).Average();

            return previous > 0 ? (recent - previous) / previous : 0;
        }

        private double CalculateMomentum(List<double> returns)
        {
            if (returns.Count < 20) return 0;

            var recent = returns.TakeLast(10).Sum();
            var previous = returns.Skip(returns.Count - 20).Take(10).Sum();

            return recent - previous;
        }

        private double CalculateRSI(List<double> returns)
        {
            if (returns.Count < 14) return 50;

            var gains = returns.Where(r => r > 0).ToList();
            var losses = returns.Where(r => r < 0).Select(Math.Abs).ToList();

            var avgGain = gains.Any() ? gains.Average() : 0;
            var avgLoss = losses.Any() ? losses.Average() : 0;

            if (avgLoss == 0) return 100;

            var rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
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

        private string FormatRegimeAnalysis(MarketRegime regime, string symbol)
        {
            var result = new System.Text.StringBuilder();

            result.AppendLine($"Market Regime Analysis for {symbol}");
            result.AppendLine($"Primary Regime: {regime.PrimaryRegime}");
            result.AppendLine($"Confidence: {regime.Confidence:P1}");
            result.AppendLine();

            result.AppendLine("Regime Breakdown:");
            result.AppendLine($"- Volatility: {regime.VolatilityRegime}");
            result.AppendLine($"- Trend: {regime.TrendRegime}");
            result.AppendLine($"- Volume: {regime.VolumeRegime}");
            result.AppendLine($"- ML Classification: {regime.MLRegime}");
            result.AppendLine();

            result.AppendLine("Key Indicators:");
            foreach (var indicator in regime.Indicators)
            {
                result.AppendLine($"- {indicator.Key}: {indicator.Value:F4}");
            }

            result.AppendLine();
            result.AppendLine("Interpretation:");
            result.AppendLine(GetRegimeInterpretation(regime));

            return result.ToString();
        }

        private string GetRegimeInterpretation(MarketRegime regime)
        {
            switch (regime.PrimaryRegime)
            {
                case MarketRegimeType.BullMarket:
                    return "Strong upward trend with positive momentum. Favorable for long positions.";
                case MarketRegimeType.BearMarket:
                    return "Strong downward trend. Consider defensive strategies and short positions.";
                case MarketRegimeType.Sideways:
                    return "Range-bound market with low trend strength. Focus on mean-reversion strategies.";
                case MarketRegimeType.HighVolatility:
                    return "Increased uncertainty and risk. Implement risk management measures.";
                case MarketRegimeType.LowVolatility:
                    return "Stable market conditions. Opportunities for carry trades and arbitrage.";
                case MarketRegimeType.Recovery:
                    return "Market showing signs of recovery from previous downturn.";
                case MarketRegimeType.Correction:
                    return "Temporary pullback within a broader trend.";
                default:
                    return "Mixed signals. Monitor closely for regime changes.";
            }
        }

        public async Task<List<RegimeTransition>> DetectRegimeTransitionsAsync(List<MarketData> data, int windowSize = 20)
        {
            var transitions = new List<RegimeTransition>();

            for (int i = windowSize; i < data.Count - windowSize; i++)
            {
                var previousWindow = data.Skip(i - windowSize).Take(windowSize).ToList();
                var currentWindow = data.Skip(i).Take(windowSize).ToList();

                var previousRegime = await DetectRegimeUsingMultipleMethodsAsync(previousWindow);
                var currentRegime = await DetectRegimeUsingMultipleMethodsAsync(currentWindow);

                if (previousRegime.PrimaryRegime != currentRegime.PrimaryRegime)
                {
                    transitions.Add(new RegimeTransition
                    {
                        Date = data[i].Timestamp,
                        FromRegime = previousRegime.PrimaryRegime,
                        ToRegime = currentRegime.PrimaryRegime,
                        Confidence = Math.Min(previousRegime.Confidence, currentRegime.Confidence)
                    });
                }
            }

            return transitions;
        }
    }

    public enum MarketRegimeType
    {
        BullMarket,
        BearMarket,
        Sideways,
        HighVolatility,
        LowVolatility,
        NormalVolatility,
        HighVolume,
        LowVolume,
        NormalVolume,
        Recovery,
        Correction
    }

    public class MarketRegime
    {
        public MarketRegimeType PrimaryRegime { get; set; }
        public double Confidence { get; set; }
        public MarketRegimeType VolatilityRegime { get; set; }
        public MarketRegimeType TrendRegime { get; set; }
        public MarketRegimeType VolumeRegime { get; set; }
        public MarketRegimeType MLRegime { get; set; }
        public Dictionary<string, double> Indicators { get; set; }
    }

    public class RegimeTransition
    {
        public DateTime Date { get; set; }
        public MarketRegimeType FromRegime { get; set; }
        public MarketRegimeType ToRegime { get; set; }
        public double Confidence { get; set; }
    }
}
