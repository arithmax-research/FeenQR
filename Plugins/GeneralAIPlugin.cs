using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

public class GeneralAIPlugin
{
    private readonly ILogger<GeneralAIPlugin> _logger;
    private readonly Kernel _kernel;

    public GeneralAIPlugin(
        Kernel kernel,
        ILogger<GeneralAIPlugin>? logger = null)
    {
        _kernel = kernel;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GeneralAIPlugin>.Instance;
    }

    [KernelFunction, Description("Summarize technical indicators and provide trading insights")]
    public async Task<string> Summarize(
        [Description("The technical indicators and analysis data to summarize")] string input)
    {
        try
        {
            var prompt = $@"
Analyze these technical indicators and provide a concise trading analysis:

{input}

Please provide:
1. Overall Market Sentiment (Bullish/Bearish/Neutral)
2. Key Technical Signals
3. Support & Resistance Levels
4. Momentum Analysis
5. Risk Factors
6. Short-term Trading Recommendation

Format the analysis in a clear, structured way.
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze technical indicators");
            return "Unable to generate AI analysis at this time.";
        }
    }

    [KernelFunction, Description("Analyze risk metrics and provide risk management insights")]
    public async Task<string> AnalyzeRisk(string riskMetrics)
    {
        try
        {
            var prompt = $@"
Analyze these risk metrics and provide risk management insights:

{riskMetrics}

Please provide:
1. Overall Risk Assessment (Low/Medium/High)
2. Key Risk Factors
3. Risk Mitigation Suggestions
4. Position Sizing Recommendations
5. Stop Loss Recommendations
6. Risk/Reward Analysis

Format the analysis in a clear, structured way.
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze risk metrics");
            return "Unable to generate risk analysis at this time.";
        }
    }
}
