using System.ComponentModel;
using Microsoft.SemanticKernel;
using Feen.Services;

namespace Feen.Plugins
{
    /// <summary>
    /// Plugin for options flow analysis and unusual activity detection
    /// </summary>
    public class OptionsFlowPlugin
    {
        private readonly OptionsFlowService _optionsFlowService;

        public OptionsFlowPlugin(OptionsFlowService optionsFlowService)
        {
            _optionsFlowService = optionsFlowService;
        }

        [KernelFunction, Description("Analyzes real-time options order flow for unusual activity")]
        public async Task<string> AnalyzeOptionsFlow(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Lookback period in minutes")] int lookbackMinutes = 60)
        {
            try
            {
                var analysis = await _optionsFlowService.AnalyzeOptionsFlowAsync(symbol, lookbackMinutes);

                return $@"Options Flow Analysis for {symbol}:

Total Volume: {analysis.TotalVolume:N0}
Flow Direction: {analysis.FlowDirection}
Confidence Score: {analysis.ConfidenceScore:P2}

Unusual Activity Detected:
{string.Join("\n", analysis.UnusualActivity.Select(a =>
    $"- {a.OptionType} {a.Strike:C} {a.Expiration:MMM dd}: Volume {a.Volume:N0}, OI {a.OpenInterest:N0}, Score {a.UnusualScore:F1} ({a.ActivityType})"))}

Analysis completed at {analysis.Timestamp:yyyy-MM-dd HH:mm:ss UTC}";
            }
            catch (Exception ex)
            {
                return $"Error analyzing options flow for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Detects unusual options activity patterns")]
        public async Task<string> DetectUnusualActivity(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Minimum unusual score threshold")] decimal threshold = 7.0m)
        {
            try
            {
                var activities = await _optionsFlowService.DetectUnusualActivityAsync(symbol, threshold);

                if (!activities.Any())
                {
                    return $"No unusual options activity detected for {symbol} above threshold {threshold}";
                }

                return $@"Unusual Options Activity for {symbol} (Threshold: {threshold}):

{string.Join("\n\n", activities.Select(a => $@"{a.OptionType} {a.Strike:C} {a.Expiration:MMM dd}
- Volume: {a.Volume:N0}
- Open Interest: {a.OpenInterest:N0}
- Implied Volatility: {a.ImpliedVolatility:P2}
- Delta: {a.Delta:F3}
- Unusual Score: {a.UnusualScore:F1}
- Activity Type: {a.ActivityType}"))}";
            }
            catch (Exception ex)
            {
                return $"Error detecting unusual activity for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes options gamma exposure and positioning")]
        public async Task<string> AnalyzeGammaExposure(
            [Description("Stock symbol to analyze")] string symbol)
        {
            try
            {
                var gammaAnalysis = await _optionsFlowService.AnalyzeGammaExposureAsync(symbol);

                return $@"Gamma Exposure Analysis for {symbol}:

Total Gamma: {gammaAnalysis.TotalGamma:N2}
Net Gamma: {gammaAnalysis.NetGamma:N2}
Gamma Risk: {gammaAnalysis.GammaRisk}
Spot Gamma: {gammaAnalysis.SpotGamma:F4}

Gamma by Strike:
{string.Join("\n", gammaAnalysis.GammaByStrike.OrderBy(kvp => kvp.Key).Select(kvp =>
    $"{kvp.Key:C}: {kvp.Value:N2}"))}";
            }
            catch (Exception ex)
            {
                return $"Error analyzing gamma exposure for {symbol}: {ex.Message}";
            }
        }

        [KernelFunction, Description("Gets options order book depth and liquidity")]
        public async Task<string> GetOptionsOrderBook(
            [Description("Stock symbol")] string symbol,
            [Description("Strike price")] decimal strike,
            [Description("Expiration date (YYYY-MM-DD)")] string expirationDate,
            [Description("Option type (CALL/PUT)")] string optionType)
        {
            try
            {
                DateTime expiration;
                if (!DateTime.TryParse(expirationDate, out expiration))
                {
                    return $"Invalid expiration date format: {expirationDate}. Use YYYY-MM-DD format.";
                }

                var orderBook = await _optionsFlowService.GetOptionsOrderBookAsync(symbol, strike, expiration, optionType.ToUpper());

                return $@"Options Order Book for {symbol} {optionType.ToUpper()} {strike:C} {expiration:MMM dd}

Best Bid: {(orderBook.Bids.FirstOrDefault()?.Price ?? 0):C} ({orderBook.Bids.FirstOrDefault()?.Size ?? 0})
Best Ask: {(orderBook.Asks.FirstOrDefault()?.Price ?? 0):C} ({orderBook.Asks.FirstOrDefault()?.Size ?? 0})
Spread: {orderBook.Spread:C}
Mid Price: {orderBook.MidPrice:C}

Bid Depth (Top 3):
{string.Join("\n", orderBook.Bids.Take(3).Select(b => $"{b.Price:C} x {b.Size}"))}

Ask Depth (Top 3):
{string.Join("\n", orderBook.Asks.Take(3).Select(a => $"{a.Price:C} x {a.Size}"))}

Last Trade: {(orderBook.LastTrade?.Price ?? 0):C} x {orderBook.LastTrade?.Size ?? 0} at {orderBook.LastTrade?.Timestamp.ToString("HH:mm:ss") ?? "N/A"} ({orderBook.LastTrade?.TradeType ?? "N/A"})";
            }
            catch (Exception ex)
            {
                return $"Error getting options order book for {symbol}: {ex.Message}";
            }
        }
    }
}