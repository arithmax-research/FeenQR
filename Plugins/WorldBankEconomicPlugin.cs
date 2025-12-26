using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class WorldBankEconomicPlugin
    {
        private readonly WorldBankService _worldBankService;

        public WorldBankEconomicPlugin(WorldBankService worldBankService)
        {
            _worldBankService = worldBankService;
        }

        [KernelFunction("get_world_bank_economic_series")]
        [Description("Retrieves World Bank economic data series for a specific indicator and country. Use this for getting historical economic data like GDP, inflation, unemployment rates, etc.")]
        public async Task<string> GetEconomicSeriesAsync(
            [Description("The World Bank indicator code (e.g., NY.GDP.MKTP.CD for GDP, FP.CPI.TOTL.ZG for inflation)")] string indicatorCode,
            [Description("ISO 3-letter country code (e.g., USA, CHN, DEU). Defaults to USA if not specified")] string countryCode = "USA",
            [Description("Start year for data retrieval (optional)")] int? startYear = null,
            [Description("End year for data retrieval (optional)")] int? endYear = null)
        {
            try
            {
                var dataPoints = await _worldBankService.GetSeriesDataAsync(indicatorCode, countryCode, startYear, endYear);

                if (dataPoints == null || dataPoints.Count == 0)
                {
                    return $"No data found for indicator {indicatorCode} in country {countryCode}";
                }

                var result = $"World Bank Data for {indicatorCode} ({countryCode}):\n";
                result += $"Total data points: {dataPoints.Count}\n\n";

                // Show most recent 10 data points
                var recentData = dataPoints.Take(10);
                foreach (var point in recentData)
                {
                    result += $"{point.Year}: {point.Value:N2}\n";
                }

                if (dataPoints.Count > 10)
                {
                    result += $"\n... and {dataPoints.Count - 10} more data points available";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error retrieving World Bank data: {ex.Message}";
            }
        }

        [KernelFunction("search_world_bank_indicators")]
        [Description("Searches for World Bank economic indicators by name or code. Use this to find available economic data series.")]
        public async Task<string> SearchEconomicIndicatorsAsync(
            [Description("Search query for economic indicators (e.g., 'GDP', 'inflation', 'unemployment')")] string query,
            [Description("Maximum number of results to return (default: 10)")] int maxResults = 10)
        {
            try
            {
                var indicators = await _worldBankService.SearchIndicatorsAsync(query, maxResults);

                if (indicators == null || indicators.Count == 0)
                {
                    return $"No World Bank indicators found matching '{query}'";
                }

                var result = $"World Bank Indicators matching '{query}':\n\n";
                foreach (var indicator in indicators)
                {
                    result += $"• {indicator.Id}: {indicator.Name}\n";
                    if (!string.IsNullOrEmpty(indicator.SourceNote))
                    {
                        // Truncate long descriptions
                        var description = indicator.SourceNote.Length > 100
                            ? indicator.SourceNote.Substring(0, 100) + "..."
                            : indicator.SourceNote;
                        result += $"  Description: {description}\n";
                    }
                    result += "\n";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error searching World Bank indicators: {ex.Message}";
            }
        }

        [KernelFunction("get_world_bank_popular_indicators")]
        [Description("Retrieves a list of popular World Bank economic indicators with their latest values. Use this to get an overview of key economic metrics.")]
        public async Task<string> GetPopularEconomicIndicatorsAsync()
        {
            try
            {
                var indicators = await _worldBankService.GetPopularIndicatorsAsync();

                if (indicators == null || indicators.Count == 0)
                {
                    return "No popular World Bank indicators available";
                }

                var result = "Popular World Bank Economic Indicators:\n\n";
                foreach (var indicator in indicators)
                {
                    result += $"• {indicator.Id}: {indicator.Name}\n";
                }

                result += "\nTo get data for any indicator, use the 'get_world_bank_economic_series' function with the indicator code.";

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error retrieving popular indicators: {ex.Message}";
            }
        }

        [KernelFunction("get_world_bank_indicator_info")]
        [Description("Gets detailed information about a specific World Bank economic indicator.")]
        public async Task<string> GetIndicatorInfoAsync(
            [Description("The World Bank indicator code (e.g., NY.GDP.MKTP.CD)")] string indicatorCode)
        {
            try
            {
                var indicator = await _worldBankService.GetIndicatorInfoAsync(indicatorCode);

                if (indicator == null)
                {
                    return $"Indicator {indicatorCode} not found";
                }

                var result = $"World Bank Indicator: {indicator.Id}\n";
                result += $"Name: {indicator.Name}\n";

                if (!string.IsNullOrEmpty(indicator.SourceNote))
                {
                    result += $"Description: {indicator.SourceNote}\n";
                }

                if (!string.IsNullOrEmpty(indicator.SourceOrganization))
                {
                    result += $"Source: {indicator.SourceOrganization}\n";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error retrieving indicator info: {ex.Message}";
            }
        }
    }
}