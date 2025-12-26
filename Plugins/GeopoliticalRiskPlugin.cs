using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class GeopoliticalRiskPlugin
    {
        private readonly GeopoliticalRiskService _geopoliticalRiskService;

        public GeopoliticalRiskPlugin(GeopoliticalRiskService geopoliticalRiskService)
        {
            _geopoliticalRiskService = geopoliticalRiskService;
        }

        [KernelFunction, Description("Get recent geopolitical events and developments")]
        public async Task<string> GetRecentGeopoliticalEvents(
            [Description("Number of events to retrieve")] int count = 10,
            [Description("Filter by region or country")] string? region = null)
        {
            try
            {
                var events = await _geopoliticalRiskService.GetRecentGeopoliticalEventsAsync(count, region);
                
                if (events.Count == 0)
                {
                    return $"No geopolitical events found{(string.IsNullOrEmpty(region) ? "" : $" for region '{region}'")}";
                }

                var result = $"Retrieved {events.Count} recent geopolitical events{(string.IsNullOrEmpty(region) ? "" : $" for region '{region}'")}\n\n";
                
                foreach (var evt in events)
                {
                    result += $"Region: {evt.Region}\n";
                    result += $"Type: {evt.EventType}\n";
                    result += $"Description: {evt.Description}\n";
                    result += $"Impact: {evt.ImpactLevel} (Risk Score: {evt.RiskScore:P1})\n";
                    result += $"Date: {evt.Date:yyyy-MM-dd}\n";
                    result += "---\n";
                }

                return result.TrimEnd('-', '\n');
            }
            catch (Exception ex)
            {
                return $"Error retrieving geopolitical events: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get current sanctions data and restrictions")]
        public async Task<string> GetSanctionsData()
        {
            try
            {
                var sanctions = await _geopoliticalRiskService.GetSanctionsDataAsync();
                return $"Retrieved sanctions data for {sanctions.Count} entities";
            }
            catch (Exception ex)
            {
                return $"Error retrieving sanctions data: {ex.Message}";
            }
        }

        [KernelFunction, Description("Calculate geopolitical risk index for regions or countries")]
        public async Task<string> CalculateGeopoliticalRiskIndex(
            [Description("Region or country to analyze")] string region = "global")
        {
            try
            {
                var riskIndex = await _geopoliticalRiskService.CalculateGeopoliticalRiskIndexAsync(region);
                return $"Geopolitical risk index for {region}: {riskIndex:F2}";
            }
            catch (Exception ex)
            {
                return $"Error calculating geopolitical risk index: {ex.Message}";
            }
        }

        [KernelFunction, Description("Monitor ongoing conflicts and their economic impact")]
        public async Task<string> MonitorOngoingConflicts()
        {
            try
            {
                var conflicts = await _geopoliticalRiskService.MonitorOngoingConflictsAsync();
                return $"Monitoring {conflicts.Count} ongoing conflicts";
            }
            catch (Exception ex)
            {
                return $"Error monitoring ongoing conflicts: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyze political stability for countries or regions")]
        public async Task<string> AnalyzePoliticalStability()
        {
            try
            {
                var stability = await _geopoliticalRiskService.AnalyzePoliticalStabilityAsync();
                return $"Analyzed political stability for {stability.Count} regions/countries";
            }
            catch (Exception ex)
            {
                return $"Error analyzing political stability: {ex.Message}";
            }
        }
    }
}