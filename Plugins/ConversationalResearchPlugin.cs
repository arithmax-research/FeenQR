using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class ConversationalResearchPlugin
    {
        private readonly ConversationalResearchService _researchService;

        public ConversationalResearchPlugin(ConversationalResearchService researchService)
        {
            _researchService = researchService;
        }

        [KernelFunction("research_query")]
        [Description("Execute natural language research queries for market analysis, statistical testing, and portfolio optimization")]
        public async Task<string> ExecuteResearchQueryAsync(
            [Description("Natural language research query (e.g., 'Analyze AAPL stock performance over the last year')")] string query)
        {
            try
            {
                return await _researchService.ExecuteResearchQueryAsync(query);
            }
            catch (Exception ex)
            {
                return $"Error executing research query: {ex.Message}";
            }
        }

        [KernelFunction("execute_research_query")]
        [Description("Execute natural language research queries for market analysis, statistical testing, and portfolio optimization")]
        public string ExecuteResearchQuery(
            [Description("Natural language research query (e.g., 'Analyze AAPL stock performance over the last year')")] string query)
        {
            try
            {
                return Task.Run(() => _researchService.ExecuteResearchQueryAsync(query)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return $"Error executing research query: {ex.Message}";
            }
        }

        [KernelFunction("market_analysis")]
        [Description("Perform comprehensive market analysis for a given symbol")]
        public async Task<string> PerformMarketAnalysisAsync(
            [Description("Stock symbol to analyze")] string symbol,
            [Description("Analysis period (1month, 3months, 6months, 1year, 2years)")] string period = "1year")
        {
            try
            {
                var query = $"Analyze {symbol} market performance over the last {period}";
                return await _researchService.ExecuteResearchQueryAsync(query);
            }
            catch (Exception ex)
            {
                return $"Error performing market analysis: {ex.Message}";
            }
        }

        [KernelFunction("statistical_test")]
        [Description("Run statistical tests on provided data")]
        public async Task<string> RunStatisticalTestAsync(
            [Description("Type of statistical test (t-test, anova, correlation)")] string testType,
            [Description("First data set as comma-separated values")] string data1,
            [Description("Second data set as comma-separated values (optional)")] string data2 = "")
        {
            try
            {
                var query = $"Run {testType} statistical test on data: {data1}";
                if (!string.IsNullOrEmpty(data2))
                    query += $" compared to: {data2}";

                return await _researchService.ExecuteResearchQueryAsync(query);
            }
            catch (Exception ex)
            {
                return $"Error running statistical test: {ex.Message}";
            }
        }

        [KernelFunction("portfolio_analysis")]
        [Description("Analyze portfolio composition and performance")]
        public async Task<string> AnalyzePortfolioAsync(
            [Description("Comma-separated list of asset symbols")] string assets,
            [Description("Portfolio weights as comma-separated values (optional, defaults to equal weight)")] string weights = "")
        {
            try
            {
                var query = $"Analyze portfolio with assets: {assets}";
                if (!string.IsNullOrEmpty(weights))
                    query += $" and weights: {weights}";

                return await _researchService.ExecuteResearchQueryAsync(query);
            }
            catch (Exception ex)
            {
                return $"Error analyzing portfolio: {ex.Message}";
            }
        }

        [KernelFunction("risk_assessment")]
        [Description("Perform comprehensive risk assessment for an asset or portfolio")]
        public async Task<string> AssessRiskAsync(
            [Description("Asset symbol or comma-separated portfolio")] string assets,
            [Description("Confidence level for VaR calculation (0.95, 0.99)")] double confidenceLevel = 0.95)
        {
            try
            {
                var query = $"Assess risk for {assets} at {confidenceLevel:P0} confidence level";
                return await _researchService.ExecuteResearchQueryAsync(query);
            }
            catch (Exception ex)
            {
                return $"Error assessing risk: {ex.Message}";
            }
        }

        [KernelFunction("generate_research_summary")]
        [Description("Generate a summary of research findings and insights")]
        public async Task<string> GenerateResearchSummaryAsync(
            [Description("Research topic or data to summarize")] string topic,
            [Description("Key findings or data points")] string findings = "")
        {
            try
            {
                var query = $"Summarize research on {topic}";
                if (!string.IsNullOrEmpty(findings))
                    query += $" with findings: {findings}";

                return await _researchService.ExecuteResearchQueryAsync(query);
            }
            catch (Exception ex)
            {
                return $"Error generating research summary: {ex.Message}";
            }
        }
    }
}
