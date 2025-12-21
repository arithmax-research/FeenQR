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
    public class SECAnalysisPlugin
    {
        private readonly SECFilingsService _secService;

        public SECAnalysisPlugin(SECFilingsService secService)
        {
            _secService = secService;
        }

        [KernelFunction, Description("Analyzes the latest SEC filing for a given ticker symbol")]
        public async Task<string> AnalyzeLatestFiling(
            [Description("Stock ticker symbol (e.g., AAPL, MSFT)")] string ticker,
            [Description("Type of filing to analyze (10-K, 10-Q, 8-K)")] string filingType = "10-K")
        {
            try
            {
                var analysis = await _secService.AnalyzeLatestFilingAsync(ticker, filingType);

                var result = $"SEC Filing Analysis for {ticker}\n\n";
                result += $"Filing Date: {analysis.Filing.FilingDate:yyyy-MM-dd}\n";
                result += $"Filing Type: {analysis.Filing.FilingType}\n";
                result += $"Accession Number: {analysis.Filing.AccessionNumber}\n\n";

                result += "Key Findings:\n";
                foreach (var finding in analysis.KeyFindings)
                {
                    result += $"- {finding}\n";
                }
                result += "\n";

                result += $"Overall Sentiment: {analysis.ContentSentiment.OverallScore:F2} " +
                         $"(Confidence: {analysis.ContentSentiment.Confidence:P1})\n\n";

                result += "Analysis Insights:\n";
                foreach (var insight in analysis.Insights)
                {
                    result += $"{insight.Key}: {insight.Value}\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing SEC filing: {ex.Message}";
            }
        }

        [KernelFunction, Description("Retrieves SEC filing history for a company")]
        public async Task<string> GetFilingHistory(
            [Description("Stock ticker symbol")] string ticker,
            [Description("Type of filings to retrieve")] string filingType = "10-K",
            [Description("Number of filings to retrieve")] int limit = 3)
        {
            try
            {
                var filings = await _secService.GetFilingHistoryAsync(ticker, filingType, limit);

                var result = $"SEC Filing History for {ticker} ({filingType})\n\n";

                foreach (var filing in filings)
                {
                    result += $"Date: {filing.FilingDate:yyyy-MM-dd}\n";
                    result += $"Accession: {filing.AccessionNumber}\n";
                    result += $"Content Length: {filing.Content.Length} characters\n\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error retrieving filing history: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes risk factors from SEC filings")]
        public async Task<string> AnalyzeRiskFactors(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _secService.AnalyzeLatestFilingAsync(ticker);

                var result = $"Risk Factor Analysis for {ticker}\n\n";

                if (analysis.Filing.RiskFactors.Any())
                {
                    result += "Identified Risk Factors:\n";
                    foreach (var risk in analysis.Filing.RiskFactors)
                    {
                        result += $"- {risk.Category}: {risk.Description}\n";
                        result += $"  Severity: {risk.Severity:F1}/10\n";
                        if (risk.Keywords.Any())
                        {
                            result += $"  Keywords: {string.Join(", ", risk.Keywords)}\n";
                        }
                        result += "\n";
                    }
                }
                else
                {
                    result += "No risk factors identified in the analysis.\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing risk factors: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes management discussion and analysis section")]
        public async Task<string> AnalyzeManagementDiscussion(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _secService.AnalyzeLatestFilingAsync(ticker);

                var result = $"Management Discussion & Analysis for {ticker}\n\n";

                result += $"Summary: {analysis.Filing.MDandA.Summary}\n\n";

                result += "Strategic Initiatives:\n";
                foreach (var initiative in analysis.Filing.MDandA.StrategicInitiatives)
                {
                    result += $"- {initiative}\n";
                }
                result += "\n";

                result += "Key Metrics:\n";
                foreach (var metric in analysis.Filing.MDandA.KeyMetrics)
                {
                    result += $"- {metric.Key}: {metric.Value}\n";
                }
                result += "\n";

                result += $"Sentiment Score: {analysis.Filing.MDandA.Sentiment.OverallScore:F2}\n";

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing management discussion: {ex.Message}";
            }
        }

        [KernelFunction, Description("Extracts financial metrics from SEC filings")]
        public async Task<string> ExtractFinancialMetrics(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _secService.AnalyzeLatestFilingAsync(ticker);

                var result = $"Financial Metrics from SEC Filings for {ticker}\n\n";

                var financialMetrics = analysis.Insights.GetValueOrDefault("FinancialMetrics", new Dictionary<string, object>()) as Dictionary<string, object>;

                if (financialMetrics != null)
                {
                    foreach (var metric in financialMetrics)
                    {
                        result += $"{metric.Key}: {metric.Value}\n";
                    }
                }
                else
                {
                    result += "No financial metrics extracted.\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error extracting financial metrics: {ex.Message}";
            }
        }

        [KernelFunction, Description("Performs comprehensive SEC filing analysis")]
        public async Task<string> ComprehensiveFilingAnalysis(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _secService.AnalyzeLatestFilingAsync(ticker);

                var result = $"COMPREHENSIVE SEC ANALYSIS: {ticker}\n";
                result += $"Analysis Date: {analysis.AnalysisDate:yyyy-MM-dd HH:mm}\n\n";

                result += "=== FILING OVERVIEW ===\n";
                result += $"Type: {analysis.Filing.FilingType}\n";
                result += $"Date: {analysis.Filing.FilingDate:yyyy-MM-dd}\n";
                result += $"Accession: {analysis.Filing.AccessionNumber}\n\n";

                result += "=== KEY FINDINGS ===\n";
                foreach (var finding in analysis.KeyFindings.Take(5))
                {
                    result += $"- {finding}\n";
                }
                result += "\n";

                result += "=== SENTIMENT ANALYSIS ===\n";
                result += $"Overall Score: {analysis.ContentSentiment.OverallScore:F2} " +
                         $"({GetSentimentLabel(analysis.ContentSentiment.OverallScore)})\n";
                result += $"Confidence: {analysis.ContentSentiment.Confidence:P1}\n\n";

                result += "=== RISK ANALYSIS ===\n";
                var riskAnalysis = analysis.Insights.GetValueOrDefault("RiskAnalysis", new Dictionary<string, object>()) as Dictionary<string, object>;
                if (riskAnalysis != null)
                {
                    foreach (var risk in riskAnalysis)
                    {
                        result += $"{risk.Key}: {risk.Value}\n";
                    }
                }
                result += "\n";

                result += "=== BUSINESS STRATEGY ===\n";
                var strategyAnalysis = analysis.Insights.GetValueOrDefault("StrategyAnalysis", new Dictionary<string, object>()) as Dictionary<string, object>;
                if (strategyAnalysis != null)
                {
                    result += strategyAnalysis.GetValueOrDefault("StrategySummary", "Not available").ToString();
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error performing comprehensive analysis: {ex.Message}";
            }
        }

        private string GetSentimentLabel(double score)
        {
            if (score > 0.2) return "Positive";
            if (score < -0.2) return "Negative";
            return "Neutral";
        }
    }
}