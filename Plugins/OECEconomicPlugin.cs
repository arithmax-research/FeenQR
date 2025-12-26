using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class OECEconomicPlugin
    {
        private readonly OECDService _oecdService;

        public OECEconomicPlugin(OECDService oecdService)
        {
            _oecdService = oecdService;
        }

        [KernelFunction("get_oecd_economic_series")]
        [Description("Retrieves OECD economic data series for a specific indicator and country. Use this for getting advanced economic data from OECD countries.")]
        public async Task<string> GetEconomicSeriesAsync(
            [Description("The OECD indicator key (e.g., QNA|USA.B1_GE.GYSA|OECD for GDP growth)")] string indicatorKey,
            [Description("ISO 3-letter country code (e.g., USA, DEU, FRA, GBR). Defaults to USA if not specified")] string countryCode = "USA",
            [Description("Start year for data retrieval (optional)")] int? startYear = null,
            [Description("End year for data retrieval (optional)")] int? endYear = null)
        {
            try
            {
                var dataPoints = await _oecdService.GetSeriesDataAsync(indicatorKey, countryCode, startYear, endYear);

                if (dataPoints == null || dataPoints.Count == 0)
                {
                    return $"No data found for indicator {indicatorKey} in country {countryCode}";
                }

                var result = $"OECD Data for {indicatorKey} ({countryCode}):\n";
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
                return $"Error retrieving OECD data: {ex.Message}";
            }
        }

        [KernelFunction("search_oecd_indicators")]
        [Description("Searches for OECD economic indicators by name or dataset. Use this to find available OECD economic data series.")]
        public async Task<string> SearchEconomicIndicatorsAsync(
            [Description("Search query for economic indicators (e.g., 'GDP', 'unemployment', 'trade')")] string query,
            [Description("Maximum number of results to return (default: 10)")] int maxResults = 10)
        {
            try
            {
                var indicators = await _oecdService.SearchIndicatorsAsync(query, maxResults);

                if (indicators == null || indicators.Count == 0)
                {
                    return $"No OECD indicators found matching '{query}'";
                }

                var result = $"OECD Indicators matching '{query}':\n\n";
                foreach (var indicator in indicators)
                {
                    result += $"• {indicator.Key}: {indicator.Name}\n";
                    if (!string.IsNullOrEmpty(indicator.Description))
                    {
                        // Truncate long descriptions
                        var description = indicator.Description.Length > 100
                            ? indicator.Description.Substring(0, 100) + "..."
                            : indicator.Description;
                        result += $"  Description: {description}\n";
                    }
                    result += "\n";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error searching OECD indicators: {ex.Message}";
            }
        }

        [KernelFunction("get_oecd_popular_indicators")]
        [Description("Retrieves a list of popular OECD economic indicators with their latest values. Use this to get an overview of key OECD economic metrics.")]
        public async Task<string> GetPopularEconomicIndicatorsAsync()
        {
            try
            {
                var indicators = await _oecdService.GetPopularIndicatorsAsync();

                if (indicators == null || indicators.Count == 0)
                {
                    return "No popular OECD indicators available";
                }

                var result = "Popular OECD Economic Indicators:\n\n";
                foreach (var indicator in indicators)
                {
                    result += $"• {indicator.Key}: {indicator.Name}\n";
                }

                result += "\nTo get data for any indicator, use the 'get_oecd_economic_series' function with the indicator key.";

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error retrieving popular indicators: {ex.Message}";
            }
        }

        [KernelFunction("get_oecd_indicator_info")]
        [Description("Gets detailed information about a specific OECD economic indicator.")]
        public async Task<string> GetIndicatorInfoAsync(
            [Description("The OECD indicator key (e.g., QNA|USA.B1_GE.GYSA|OECD)")] string indicatorKey)
        {
            try
            {
                var indicator = await _oecdService.GetIndicatorInfoAsync(indicatorKey);

                if (indicator == null)
                {
                    return $"Indicator {indicatorKey} not found";
                }

                var result = $"OECD Indicator: {indicator.Key}\n";
                result += $"Dataset: {indicator.Dataset}\n";
                result += $"Name: {indicator.Name}\n";

                if (!string.IsNullOrEmpty(indicator.Description))
                {
                    result += $"Description: {indicator.Description}\n";
                }

                result += $"Agency: {indicator.Agency}\n";

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error retrieving indicator info: {ex.Message}";
            }
        }
    }
}