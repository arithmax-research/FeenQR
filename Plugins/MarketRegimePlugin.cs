using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class MarketRegimePlugin
    {
        private readonly MarketRegimeService _regimeService;

        public MarketRegimePlugin(MarketRegimeService regimeService)
        {
            _regimeService = regimeService;
        }

        [KernelFunction("detect_market_regime")]
        [Description("Detect the current market regime using multiple analytical methods")]
        public async Task<string> DetectMarketRegimeAsync(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Lookback period in days")] int lookbackDays = 252)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["symbol"] = symbol,
                    ["lookbackDays"] = lookbackDays
                };

                return await _regimeService.IdentifyMarketRegimeAsync(data);
            }
            catch (Exception ex)
            {
                return $"Error detecting market regime: {ex.Message}";
            }
        }

        [KernelFunction("detect_market_regime_sync")]
        [Description("Detect the current market regime using multiple analytical methods")]
        public string DetectMarketRegime(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Lookback period in days")] int lookbackDays = 252)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["symbol"] = symbol,
                    ["lookbackDays"] = lookbackDays
                };

                return Task.Run(() => _regimeService.IdentifyMarketRegimeAsync(data)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return $"Error detecting market regime: {ex.Message}";
            }
        }

        [KernelFunction("analyze_regime_transitions")]
        [Description("Analyze historical regime transitions for a given symbol")]
        public async Task<string> AnalyzeRegimeTransitionsAsync(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Lookback period in days")] int lookbackDays = 504,
            [Description("Window size for transition detection")] int windowSize = 20)
        {
            try
            {
                // This would need to be implemented in the service
                return $"Regime transition analysis for {symbol} over {lookbackDays} days (feature in development)";
            }
            catch (Exception ex)
            {
                return $"Error analyzing regime transitions: {ex.Message}";
            }
        }

        [KernelFunction("get_regime_indicators")]
        [Description("Get key regime indicators for market analysis")]
        public async Task<string> GetRegimeIndicatorsAsync(
            [Description("Stock symbol to analyze")] string symbol)
        {
            try
            {
                var data = new Dictionary<string, object> { ["symbol"] = symbol };
                var regime = await _regimeService.IdentifyMarketRegimeAsync(data);

                // Extract key indicators from the regime analysis
                return $"Regime indicators for {symbol}: {regime}";
            }
            catch (Exception ex)
            {
                return $"Error getting regime indicators: {ex.Message}";
            }
        }

        [KernelFunction("regime_based_strategy")]
        [Description("Suggest trading strategies based on current market regime")]
        public async Task<string> SuggestRegimeBasedStrategyAsync(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Risk tolerance (low, medium, high)")] string riskTolerance = "medium")
        {
            try
            {
                var data = new Dictionary<string, object> { ["symbol"] = symbol };
                var regime = await _regimeService.IdentifyMarketRegimeAsync(data);

                // Generate strategy recommendations based on regime
                var strategy = GenerateStrategyFromRegime(regime, riskTolerance);
                return $"Strategy recommendation for {symbol} ({riskTolerance} risk):\n{strategy}";
            }
            catch (Exception ex)
            {
                return $"Error generating regime-based strategy: {ex.Message}";
            }
        }

        private string GenerateStrategyFromRegime(string regimeAnalysis, string riskTolerance)
        {
            // Simple strategy generation based on regime keywords
            if (regimeAnalysis.Contains("Bull"))
            {
                return riskTolerance switch
                {
                    "low" => "Conservative long position with stop-loss",
                    "medium" => "Core long position with moderate leverage",
                    "high" => "Aggressive long position with options enhancement",
                    _ => "Standard long position"
                };
            }
            else if (regimeAnalysis.Contains("Bear"))
            {
                return riskTolerance switch
                {
                    "low" => "Cash or defensive assets",
                    "medium" => "Reduced exposure with hedging",
                    "high" => "Short positions with leverage",
                    _ => "Defensive positioning"
                };
            }
            else
            {
                return "Range-bound strategy: pairs trading or options strategies";
            }
        }
    }
}
