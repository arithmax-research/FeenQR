using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for Federal Reserve data operations
/// </summary>
public class FederalReservePlugin
{
    private readonly FederalReserveService _federalReserveService;

    public FederalReservePlugin(FederalReserveService federalReserveService)
    {
        _federalReserveService = federalReserveService;
    }

    [KernelFunction, Description("Get recent FOMC announcements")]
    public async Task<string> GetRecentFOMCAnnouncements()
    {
        try
        {
            var announcements = await _federalReserveService.GetRecentFOMCAnnouncementsAsync();

            if (announcements.Any())
            {
                return $"Recent FOMC Announcements:\n" +
                       string.Join("\n", announcements.Select(a =>
                           $"{a.Date:yyyy-MM-dd}: {a.Title} (Market Impact: {a.MarketImpact:F2}%)"));
            }
            else
            {
                return "No recent FOMC announcements found";
            }
        }
        catch (Exception ex)
        {
            return $"Error fetching FOMC announcements: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get interest rate decisions")]
    public async Task<string> GetInterestRateDecisions()
    {
        try
        {
            var decisions = await _federalReserveService.GetInterestRateDecisionsAsync();

            if (decisions.Any())
            {
                var decision = decisions.First();
                return $"Latest Interest Rate Decision:\n" +
                       $"Meeting Date: {decision.MeetingDate:yyyy-MM-dd}\n" +
                       $"Current Rate: {decision.CurrentRate:F2}%\n" +
                       $"Previous Rate: {decision.PreviousRate:F2}%\n" +
                       $"Rate Change: {decision.RateChange:F2}%\n" +
                       $"Next Meeting: {decision.NextMeeting:yyyy-MM-dd}\n" +
                       $"Rate Path: {decision.RatePath}";
            }
            else
            {
                return "No interest rate decisions found";
            }
        }
        catch (Exception ex)
        {
            return $"Error fetching interest rate decisions: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get economic projections")]
    public async Task<string> GetEconomicProjections()
    {
        try
        {
            var projections = await _federalReserveService.GetEconomicProjectionsAsync();

            if (projections.Any())
            {
                var projection = projections.First();
                return $"Federal Reserve Economic Projections:\n" +
                       $"GDP Growth: {projection.GDPGrowth:F1}%\n" +
                       $"Inflation: {projection.Inflation:F1}%\n" +
                       $"Unemployment: {projection.Unemployment:F1}%\n" +
                       $"Projection Date: {projection.ProjectionDate:yyyy-MM-dd}";
            }
            else
            {
                return "No economic projections found";
            }
        }
        catch (Exception ex)
        {
            return $"Error fetching economic projections: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get recent Fed speeches")]
    public async Task<string> GetRecentFedSpeeches()
    {
        try
        {
            var speeches = await _federalReserveService.GetRecentFedSpeechesAsync();

            if (speeches.Any())
            {
                return $"Recent Federal Reserve Speeches:\n" +
                       string.Join("\n", speeches.Select(s =>
                           $"{s.Speaker} ({s.Date:yyyy-MM-dd}): {s.Title}"));
            }
            else
            {
                return "No recent Fed speeches found";
            }
        }
        catch (Exception ex)
        {
            return $"Error fetching Fed speeches: {ex.Message}";
        }
    }
}