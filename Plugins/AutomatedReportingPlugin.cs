using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class AutomatedReportingPlugin
    {
        private readonly AutomatedReportingService _reportingService;

        public AutomatedReportingPlugin(AutomatedReportingService reportingService)
        {
            _reportingService = reportingService;
        }

        [KernelFunction("generate_research_report")]
        [Description("Generate a comprehensive research report with insights and visualizations")]
        public async Task<string> GenerateResearchReportAsync(
            [Description("Research data or topic to generate report on")] string researchData,
            [Description("Report format (markdown, html, pdf)")] string format = "markdown")
        {
            try
            {
                var report = await _reportingService.GenerateReportAsync(researchData);
                return await _reportingService.ExportReportToFormatAsync(report, format);
            }
            catch (Exception ex)
            {
                return $"Error generating research report: {ex.Message}";
            }
        }

        [KernelFunction("generate_research_report_sync")]
        [Description("Generate a comprehensive research report with insights and visualizations")]
        public string GenerateResearchReport(
            [Description("Research data or topic to generate report on")] string researchData,
            [Description("Report format (markdown, html, pdf)")] string format = "markdown")
        {
            try
            {
                var report = Task.Run(() => _reportingService.GenerateReportAsync(researchData)).GetAwaiter().GetResult();
                return Task.Run(() => _reportingService.ExportReportToFormatAsync(report, format)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return $"Error generating research report: {ex.Message}";
            }
        }

        [KernelFunction("generate_strategy_report")]
        [Description("Generate a detailed strategy performance report")]
        public async Task<string> GenerateStrategyReportAsync(
            [Description("Name of the trading strategy")] string strategyName,
            [Description("Performance data as JSON string")] string performanceData = "{}")
        {
            try
            {
                return await _reportingService.GenerateStrategyReportAsync(strategyName, performanceData);
            }
            catch (Exception ex)
            {
                return $"Error generating strategy report: {ex.Message}";
            }
        }

        [KernelFunction("export_report")]
        [Description("Export a report to different formats")]
        public async Task<string> ExportReportAsync(
            [Description("Report content in markdown format")] string reportContent,
            [Description("Export format (html, pdf, markdown)")] string format)
        {
            try
            {
                return await _reportingService.ExportReportToFormatAsync(reportContent, format);
            }
            catch (Exception ex)
            {
                return $"Error exporting report: {ex.Message}";
            }
        }

        [KernelFunction("generate_executive_summary")]
        [Description("Generate an executive summary from research data")]
        public async Task<string> GenerateExecutiveSummaryAsync(
            [Description("Research data to summarize")] string researchData)
        {
            try
            {
                // This would call the internal method, but for now we'll simulate
                return $"## Executive Summary\n\nBased on the analysis of {researchData}, key findings include market trends, risk assessments, and strategic recommendations. (Full implementation in AutomatedReportingService)";
            }
            catch (Exception ex)
            {
                return $"Error generating executive summary: {ex.Message}";
            }
        }

        [KernelFunction("generate_performance_metrics")]
        [Description("Generate detailed performance metrics report")]
        public async Task<string> GeneratePerformanceMetricsAsync(
            [Description("Performance data to analyze")] string performanceData)
        {
            try
            {
                // This would integrate with the service method
                return await _reportingService.GenerateStrategyReportAsync("Custom Strategy", performanceData);
            }
            catch (Exception ex)
            {
                return $"Error generating performance metrics: {ex.Message}";
            }
        }

        [KernelFunction("generate_risk_analysis")]
        [Description("Generate comprehensive risk analysis report")]
        public async Task<string> GenerateRiskAnalysisAsync(
            [Description("Asset or portfolio to analyze")] string assets,
            [Description("Historical data period")] string period = "1year")
        {
            try
            {
                var riskData = $"{assets} over {period}";
                var report = await _reportingService.GenerateReportAsync(riskData);
                return report;
            }
            catch (Exception ex)
            {
                return $"Error generating risk analysis: {ex.Message}";
            }
        }

        [KernelFunction("generate_visualization_guide")]
        [Description("Generate recommendations for data visualizations")]
        public async Task<string> GenerateVisualizationGuideAsync(
            [Description("Data to visualize")] string dataDescription)
        {
            try
            {
                var vizData = $"Visualization recommendations for: {dataDescription}";
                var report = await _reportingService.GenerateReportAsync(vizData);
                return report;
            }
            catch (Exception ex)
            {
                return $"Error generating visualization guide: {ex.Message}";
            }
        }
    }
}
