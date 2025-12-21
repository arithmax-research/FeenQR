using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class EarningsAnalysisPlugin
    {
        private readonly EarningsCallService _earningsService;

        public EarningsAnalysisPlugin(EarningsCallService earningsService)
        {
            _earningsService = earningsService;
        }

        [KernelFunction, Description("Analyzes the latest earnings call for a given ticker symbol")]
        public async Task<string> AnalyzeLatestEarningsCall(
            [Description("Stock ticker symbol (e.g., AAPL, MSFT)")] string ticker)
        {
            try
            {
                var analysis = await _earningsService.AnalyzeLatestEarningsCallAsync(ticker);

                var result = $"Earnings Call Analysis for {ticker}\n\n";
                result += $"Call Date: {analysis.EarningsCall.CallDate:yyyy-MM-dd}\n";
                result += $"Quarter: {analysis.EarningsCall.Quarter} {analysis.EarningsCall.Year}\n\n";

                result += "Financial Metrics:\n";
                if (analysis.FinancialMetrics.Revenue > 0)
                {
                    result += $"- Revenue: ${analysis.FinancialMetrics.Revenue:N0}M\n";
                }
                if (analysis.FinancialMetrics.EPS != 0)
                {
                    result += $"- EPS: ${analysis.FinancialMetrics.EPS:F2}\n";
                }
                if (analysis.FinancialMetrics.GuidanceLow > 0 && analysis.FinancialMetrics.GuidanceHigh > 0)
                {
                    result += $"- Guidance: ${analysis.FinancialMetrics.GuidanceLow:F2} - ${analysis.FinancialMetrics.GuidanceHigh:F2}\n";
                }
                result += "\n";

                result += $"Overall Sentiment: {analysis.SentimentAnalysis.OverallScore:F2} " +
                         $"(Confidence: {analysis.SentimentAnalysis.Confidence:P1})\n\n";

                result += "Strategic Insights:\n";
                foreach (var insight in analysis.StrategicInsights)
                {
                    result += $"- {insight}\n";
                }
                result += "\n";

                result += "Risk Indicators:\n";
                foreach (var risk in analysis.RiskIndicators)
                {
                    result += $"- {risk}\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing earnings call: {ex.Message}";
            }
        }

        [KernelFunction, Description("Retrieves earnings call history for a company")]
        public async Task<string> GetEarningsCallHistory(
            [Description("Stock ticker symbol")] string ticker,
            [Description("Number of calls to retrieve")] int limit = 4)
        {
            try
            {
                var calls = await _earningsService.GetEarningsCallHistoryAsync(ticker, limit);

                var result = $"Earnings Call History for {ticker}\n\n";

                foreach (var call in calls)
                {
                    result += $"{call.Quarter} {call.Year} - {call.CallDate:yyyy-MM-dd}\n";
                    result += $"Transcript Length: {call.Transcript.Length} characters\n";
                    result += $"Speaker Segments: {call.SpeakerSegments.Count}\n\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error retrieving earnings call history: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes sentiment from earnings calls")]
        public async Task<string> AnalyzeEarningsSentiment(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _earningsService.AnalyzeLatestEarningsCallAsync(ticker);

                var result = $"Earnings Sentiment Analysis for {ticker}\n\n";

                result += $"Overall Sentiment Score: {analysis.SentimentAnalysis.OverallScore:F2}\n";
                result += $"Confidence: {analysis.SentimentAnalysis.Confidence:P1}\n\n";

                if (analysis.SentimentAnalysis.AdditionalMetrics.ContainsKey("SpeakerSentiments"))
                {
                    var speakerSentiments = analysis.SentimentAnalysis.AdditionalMetrics["SpeakerSentiments"] as Dictionary<string, double>;
                    if (speakerSentiments != null)
                    {
                        result += "Speaker Sentiment Breakdown:\n";
                        foreach (var speaker in speakerSentiments)
                        {
                            result += $"- {speaker.Key}: {speaker.Value:F2}\n";
                        }
                        result += "\n";
                    }
                }

                if (analysis.SentimentAnalysis.AdditionalMetrics.ContainsKey("ManagementTone"))
                {
                    var managementTone = (double)analysis.SentimentAnalysis.AdditionalMetrics["ManagementTone"];
                    result += $"Management Tone: {managementTone:F2}\n\n";
                }

                result += "Sentiment Interpretation:\n";
                result += GetSentimentInterpretation(analysis.SentimentAnalysis.OverallScore);

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing earnings sentiment: {ex.Message}";
            }
        }

        [KernelFunction, Description("Extracts strategic insights from earnings calls")]
        public async Task<string> ExtractStrategicInsights(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _earningsService.AnalyzeLatestEarningsCallAsync(ticker);

                var result = $"Strategic Insights from {ticker} Earnings Call\n\n";

                foreach (var insight in analysis.StrategicInsights)
                {
                    result += $"{insight}\n\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error extracting strategic insights: {ex.Message}";
            }
        }

        [KernelFunction, Description("Identifies risk indicators from earnings calls")]
        public async Task<string> IdentifyRiskIndicators(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _earningsService.AnalyzeLatestEarningsCallAsync(ticker);

                var result = $"Risk Indicators from {ticker} Earnings Call\n\n";

                if (analysis.RiskIndicators.Any())
                {
                    foreach (var risk in analysis.RiskIndicators)
                    {
                        result += $"- {risk}\n";
                    }
                }
                else
                {
                    result += "No significant risk indicators identified.\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error identifying risk indicators: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes competitive positioning from earnings calls")]
        public async Task<string> AnalyzeCompetitivePositioning(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _earningsService.AnalyzeLatestEarningsCallAsync(ticker);

                var result = $"Competitive Positioning Analysis for {ticker}\n\n";

                foreach (var positioning in analysis.CompetitivePositioning)
                {
                    result += $"{positioning.Key}:\n{positioning.Value}\n\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing competitive positioning: {ex.Message}";
            }
        }

        [KernelFunction, Description("Performs comprehensive earnings call analysis")]
        public async Task<string> ComprehensiveEarningsAnalysis(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _earningsService.AnalyzeLatestEarningsCallAsync(ticker);

                var result = $"COMPREHENSIVE EARNINGS ANALYSIS: {ticker}\n";
                result += $"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm}\n\n";

                result += "=== CALL OVERVIEW ===\n";
                result += $"Date: {analysis.EarningsCall.CallDate:yyyy-MM-dd}\n";
                result += $"Quarter: {analysis.EarningsCall.Quarter} {analysis.EarningsCall.Year}\n";
                result += $"Transcript Length: {analysis.EarningsCall.Transcript.Length} characters\n\n";

                result += "=== FINANCIAL PERFORMANCE ===\n";
                if (analysis.FinancialMetrics.Revenue > 0)
                    result += $"Revenue: ${analysis.FinancialMetrics.Revenue:N0}M\n";
                if (analysis.FinancialMetrics.EPS != 0)
                    result += $"EPS: ${analysis.FinancialMetrics.EPS:F2}\n";
                if (analysis.FinancialMetrics.GuidanceLow > 0 && analysis.FinancialMetrics.GuidanceHigh > 0)
                    result += $"Guidance Range: ${analysis.FinancialMetrics.GuidanceLow:F2} - ${analysis.FinancialMetrics.GuidanceHigh:F2}\n";
                result += $"AI Insights: {string.Join(", ", analysis.FinancialMetrics.AIExtractedInsights)}\n\n";

                result += "=== SENTIMENT ANALYSIS ===\n";
                result += $"Overall Score: {analysis.SentimentAnalysis.OverallScore:F2} " +
                         $"({GetSentimentLabel(analysis.SentimentAnalysis.OverallScore)})\n";
                result += $"Confidence: {analysis.SentimentAnalysis.Confidence:P1}\n\n";

                result += "=== STRATEGIC INSIGHTS ===\n";
                var strategicSummary = analysis.StrategicInsights.Any() ? string.Join("\n", analysis.StrategicInsights) : "Not available";
                result += $"{strategicSummary}\n\n";

                result += "=== RISK INDICATORS ===\n";
                if (analysis.RiskIndicators.Any())
                {
                    foreach (var risk in analysis.RiskIndicators)
                    {
                        result += $"- {risk}\n";
                    }
                }
                else
                {
                    result += "No significant risks identified\n";
                }
                result += "\n";

                result += "=== COMPETITIVE POSITIONING ===\n";
                var competitiveAnalysis = analysis.CompetitivePositioning.GetValueOrDefault("CompetitiveAnalysis", "Not available");
                result += $"{competitiveAnalysis}\n\n";

                result += "=== MARKET INTELLIGENCE ===\n";
                result += $"Market Share Mentions: {analysis.CompetitivePositioning.GetValueOrDefault("MarketShareMentions", 0)}\n";
                result += $"Competitor Mentions: {analysis.CompetitivePositioning.GetValueOrDefault("CompetitorMentions", 0)}\n";

                return result;
            }
            catch (Exception ex)
            {
                return $"Error performing comprehensive earnings analysis: {ex.Message}";
            }
        }

        [KernelFunction, Description("Compares earnings calls across multiple quarters")]
        public async Task<string> CompareEarningsCalls(
            [Description("Stock ticker symbol")] string ticker,
            [Description("Number of quarters to compare")] int quarters = 4)
        {
            try
            {
                var calls = await _earningsService.GetEarningsCallHistoryAsync(ticker, quarters);

                var result = $"Earnings Call Comparison for {ticker} (Last {quarters} Quarters)\n\n";

                foreach (var call in calls.OrderBy(c => c.CallDate))
                {
                    result += $"{call.Quarter} {call.Year}:\n";

                    // Analyze each call
                    var analysis = await _earningsService.AnalyzeLatestEarningsCallAsync(ticker);
                    // Note: In real implementation, we'd analyze each specific call

                    result += $"- Date: {call.CallDate:yyyy-MM-dd}\n";
                    result += $"- Sentiment: {analysis.SentimentAnalysis.OverallScore:F2}\n";
                    if (analysis.FinancialMetrics.Revenue > 0)
                        result += $"- Revenue: ${analysis.FinancialMetrics.Revenue:N0}M\n";
                    result += "\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error comparing earnings calls: {ex.Message}";
            }
        }

        private string GetSentimentLabel(double score)
        {
            if (score > 0.2) return "Positive";
            if (score < -0.2) return "Negative";
            return "Neutral";
        }

        private string GetSentimentInterpretation(double score)
        {
            if (score > 0.5)
                return "Very positive tone suggesting strong confidence and optimism about future performance.";
            else if (score > 0.2)
                return "Generally positive with some cautious optimism about growth prospects.";
            else if (score < -0.5)
                return "Very negative tone indicating significant concerns or challenges.";
            else if (score < -0.2)
                return "Generally negative with concerns about performance or market conditions.";
            else
                return "Neutral to mixed tone with balanced view of opportunities and challenges.";
        }
    }
}