using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for patent analysis operations
/// </summary>
public class PatentAnalysisPlugin
{
    private readonly PatentAnalysisService _patentAnalysisService;

    public PatentAnalysisPlugin(PatentAnalysisService patentAnalysisService)
    {
        _patentAnalysisService = patentAnalysisService;
    }

    [KernelFunction, Description("Search for company patents")]
    public async Task<string> SearchCompanyPatents(
        [Description("The company name to search patents for")] string companyName)
    {
        try
        {
            var patents = await _patentAnalysisService.SearchCompanyPatentsAsync(companyName);

            if (patents.Any())
            {
                return $"Found {patents.Count} patents for {companyName}:\n" +
                       string.Join("\n", patents.Select(p =>
                           $"{p.PatentId}: {p.Title} (Filed: {p.PublicationDate:yyyy-MM-dd}, Citations: {p.CitationCount})"));
            }
            else
            {
                return $"No patents found for {companyName}";
            }
        }
        catch (Exception ex)
        {
            return $"Error searching company patents: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze innovation trends for a company")]
    public async Task<string> AnalyzeInnovationTrends(
        [Description("The company name to analyze innovation for")] string companyName)
    {
        try
        {
            var metrics = await _patentAnalysisService.AnalyzeInnovationTrendsAsync(companyName);

            return $"Innovation metrics for {companyName}:\n" +
                   $"Innovation Rate: {metrics.InnovationRate:F2}\n" +
                   $"Top Technology Areas: {string.Join(", ", metrics.TopTechnologyAreas)}\n" +
                   $"R&D Intensity: {metrics.RDIntensity:F2}\n" +
                   $"Patent Quality Score: {metrics.PatentQualityScore:F2}";
        }
        catch (Exception ex)
        {
            return $"Error analyzing innovation trends: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze patent citations")]
    public async Task<string> AnalyzePatentCitations(
        [Description("The patent ID to analyze citations for")] string patentId)
    {
        try
        {
            var citations = await _patentAnalysisService.AnalyzePatentCitationsAsync(patentId);

            if (citations.Any())
            {
                var citation = citations.First();
                return $"Citation analysis for patent {patentId}:\n" +
                       $"Forward Citations: {citation.ForwardCitations}\n" +
                       $"Backward Citations: {citation.BackwardCitations}\n" +
                       $"Citation Score: {citation.CitationScore:F2}\n" +
                       $"Technology Impact: {citation.TechnologyImpact:F2}";
            }
            else
            {
                return $"No citation data available for patent {patentId}";
            }
        }
        catch (Exception ex)
        {
            return $"Error analyzing patent citations: {ex.Message}";
        }
    }

    [KernelFunction, Description("Estimate patent value")]
    public async Task<string> EstimatePatentValue(
        [Description("The patent ID to estimate value for")] string patentId)
    {
        try
        {
            var valuation = await _patentAnalysisService.EstimatePatentValueAsync(patentId);

            return $"Patent valuation for {patentId}:\n" +
                   $"Estimated Value: ${valuation.EstimatedValue:N0}\n" +
                   $"Confidence Level: {valuation.ConfidenceLevel:F2}\n" +
                   $"Key Factors: {string.Join(", ", valuation.KeyFactors)}";
        }
        catch (Exception ex)
        {
            return $"Error estimating patent value: {ex.Message}";
        }
    }
}