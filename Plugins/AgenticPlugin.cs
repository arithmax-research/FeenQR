using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for Agentic Orchestrator capabilities
/// </summary>
public class AgenticPlugin
{
    private readonly AgenticOrchestrator _agenticOrchestrator;

    public AgenticPlugin(AgenticOrchestrator agenticOrchestrator)
    {
        _agenticOrchestrator = agenticOrchestrator;
    }

    [KernelFunction, Description("Execute autonomous multi-agent analysis for trading decisions")]
    public async Task<string> ExecuteAutonomousAnalysisAsync(
        [Description("Stock symbol to analyze")] string symbol,
        [Description("Available capital for trading")] decimal capital = 10000)
    {
        try
        {
            var result = await _agenticOrchestrator.ExecuteAutonomousAnalysisAsync(symbol, capital);

            return FormatAgenticResult(result);
        }
        catch (Exception ex)
        {
            return $"Agentic Analysis Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze YouTube content with agentic coordination")]
    public async Task<string> AnalyzeYouTubeWithAgentsAsync(
        [Description("YouTube video URL to analyze")] string videoUrl)
    {
        try
        {
            var result = await _agenticOrchestrator.AnalyzeYouTubeContentAsync(videoUrl);

            return FormatAgenticResult(result);
        }
        catch (Exception ex)
        {
            return $"Agentic YouTube Analysis Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Learn from trading outcomes to improve future decisions")]
    public async Task<string> LearnFromTradingOutcomeAsync(
        [Description("Stock symbol")] string symbol,
        [Description("Was the trade successful?")] bool wasSuccessful,
        [Description("Description of the outcome")] string outcome)
    {
        try
        {
            await _agenticOrchestrator.LearnFromOutcomeAsync(symbol, wasSuccessful, outcome);

            return $"Learning completed for {symbol}. Outcome: {outcome}. Future analyses will incorporate these insights.";
        }
        catch (Exception ex)
        {
            return $"Learning Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get comprehensive market intelligence with agent coordination")]
    public async Task<string> GetAgenticMarketIntelligenceAsync(
        [Description("Market query or sector to analyze")] string query)
    {
        try
        {
            // Use autonomous analysis for market intelligence
            var result = await _agenticOrchestrator.ExecuteAutonomousAnalysisAsync(query);

            return $"Agentic Market Intelligence for '{query}':\n\n" +
                   $"Market Analysis: {result.MarketAnalysis}\n\n" +
                   $"Risk Assessment: {result.RiskAssessment}\n\n" +
                   $"Final Decision: {result.FinalDecision}\n\n" +
                   $"Confidence: {result.ConfidenceScore:P1}";
        }
        catch (Exception ex)
        {
            return $"Market Intelligence Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Execute coordinated trading strategy with multiple agents")]
    public async Task<string> ExecuteCoordinatedStrategyAsync(
        [Description("Primary symbol")] string symbol,
        [Description("Strategy type (momentum, mean_reversion, breakout)")] string strategyType,
        [Description("Risk tolerance (low, medium, high)")] string riskTolerance = "medium")
    {
        try
        {
            var result = await _agenticOrchestrator.ExecuteAutonomousAnalysisAsync(symbol);

            var strategyPrompt = $@"
Based on the agentic analysis, develop a coordinated {strategyType} strategy with {riskTolerance} risk tolerance:

Analysis Results:
- Market Analysis: {result.MarketAnalysis}
- Risk Assessment: {result.RiskAssessment}
- Trading Signals: {result.TradingSignals}
- Execution Plan: {result.ExecutionPlan}

Create a comprehensive strategy that coordinates all agents' inputs into actionable trading decisions.

Strategy:";

            // This would typically use another agent, but for now we'll use the kernel directly
            var strategyResult = await Microsoft.SemanticKernel.KernelExtensions.InvokePromptAsync(
                _agenticOrchestrator.GetType().GetProperty("_kernel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_agenticOrchestrator) as Kernel,
                strategyPrompt);

            return $"Coordinated {strategyType.ToUpper()} Strategy for {symbol}:\n\n{strategyResult}\n\n" +
                   $"Overall Confidence: {result.ConfidenceScore:P1}";
        }
        catch (Exception ex)
        {
            return $"Strategy Execution Error: {ex.Message}";
        }
    }

    private string FormatAgenticResult(AgenticAnalysisResult result)
    {
        var output = $"🤖 Agentic Analysis Result for {result.Symbol}\n";
        output += $"📊 Confidence Score: {result.ConfidenceScore:P1}\n";
        output += $"🕒 Analysis Time: {result.Timestamp:yyyy-MM-dd HH:mm:ss UTC}\n\n";

        output += $"📈 Market Analysis:\n{result.MarketAnalysis}\n\n";

        output += $"⚠️ Risk Assessment:\n{result.RiskAssessment}\n\n";

        output += $"🎯 Trading Signals:\n{result.TradingSignals}\n\n";

        output += $"⚡ Execution Plan:\n{result.ExecutionPlan}\n\n";

        output += $"🧠 RAG Context (Score: {result.RAGContext.ConfidenceScore:P1}):\n";
        output += $"{result.RAGContext.Analysis}\n\n";

        output += $"🎪 Final Decision:\n{result.FinalDecision}\n";

        if (result.RAGContext.TradingSignals.Any())
        {
            output += $"\n📋 RAG-Enhanced Signals:\n";
            for (int i = 0; i < result.RAGContext.TradingSignals.Count; i++)
            {
                output += $"{i + 1}. {result.RAGContext.TradingSignals[i]}\n";
            }
        }

        return output;
    }
}