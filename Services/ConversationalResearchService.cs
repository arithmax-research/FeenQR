using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using MathNet.Numerics.Statistics;

namespace QuantResearchAgent.Services
{
    public class ConversationalResearchService
    {
        private readonly MarketDataService _marketDataService;
        private readonly StatisticalTestingService _statisticalService;
        private readonly Kernel _kernel;

        public ConversationalResearchService(
            MarketDataService marketDataService,
            StatisticalTestingService statisticalService,
            Kernel kernel)
        {
            _marketDataService = marketDataService;
            _statisticalService = statisticalService;
            _kernel = kernel;
        }

        // Handles natural language research queries and executes them
        public async Task<string> ExecuteResearchQueryAsync(string query)
        {
            try
            {
                // Parse the natural language query to understand intent
                var intent = await ParseQueryIntentAsync(query);

                switch (intent.Type)
                {
                    case "market_analysis":
                        return await PerformMarketAnalysisAsync(intent.Parameters);
                    case "statistical_test":
                        return await PerformStatisticalAnalysisAsync(intent.Parameters);
                    case "portfolio_optimization":
                        return await PerformPortfolioAnalysisAsync(intent.Parameters);
                    case "risk_assessment":
                        return await PerformRiskAssessmentAsync(intent.Parameters);
                    default:
                        return await GenerateGeneralResearchResponseAsync(query);
                }
            }
            catch (Exception ex)
            {
                return $"Error processing research query: {ex.Message}";
            }
        }

        private async Task<QueryIntent> ParseQueryIntentAsync(string query)
        {
            // Use AI to parse the query intent
            var prompt = $@"
Analyze this research query and extract the intent and parameters:
Query: {query}

Return a JSON object with:
- type: The type of analysis (market_analysis, statistical_test, portfolio_optimization, risk_assessment, general)
- parameters: Key-value pairs of extracted parameters
- confidence: Confidence score (0-1)

Example:
{{
    ""type"": ""market_analysis"",
    ""parameters"": {{""symbol"": ""AAPL"", ""period"": ""1year""}},
    ""confidence"": 0.95
}}";

            var result = await _kernel.InvokePromptAsync(prompt);
            return ParseIntentResult(result.ToString());
        }

        private async Task<string> PerformMarketAnalysisAsync(Dictionary<string, string> parameters)
        {
            var symbol = parameters.GetValueOrDefault("symbol", "SPY");
            var period = parameters.GetValueOrDefault("period", "1year");

            // Get market data
            var data = await _marketDataService.GetHistoricalDataAsync(symbol, 365);
            if (data == null || !data.Any())
                return $"No data available for {symbol}";

            // Perform basic analysis
            var prices = data.Select(d => d.Price).ToList();
            var returns = CalculateReturns(prices);
            var volatility = CalculateVolatility(returns);
            var trend = CalculateTrend(returns);

            return $@"
Market Analysis for {symbol} ({period}):
- Current Price: ${prices.Last():F2}
- Average Price: ${prices.Average():F2}
- Volatility: {volatility:P2}
- Trend: {trend}
- Data Points: {data.Count}
";
        }

        private async Task<string> PerformStatisticalAnalysisAsync(Dictionary<string, string> parameters)
        {
            // Extract parameters
            var testType = parameters.GetValueOrDefault("test_type", "t-test");
            var data1 = parameters.GetValueOrDefault("data1", "");
            var data2 = parameters.GetValueOrDefault("data2", "");

            // Parse data arrays
            var sample1 = ParseDataArray(data1);
            var sample2 = ParseDataArray(data2);

            if (!sample1.Any())
                return "No valid data provided for statistical test";

            // Perform statistical test
            var result = _statisticalService.PerformTTest(sample1, sample2.Any() ? sample2 : null);

            return FormatStatisticalResult(result, testType);
        }

        private async Task<string> PerformPortfolioAnalysisAsync(Dictionary<string, string> parameters)
        {
            var assets = parameters.GetValueOrDefault("assets", "AAPL,MSFT,GOOGL");
            var assetList = assets.Split(',').Select(a => a.Trim()).ToList();

            // This would integrate with portfolio optimization service
            return $"Portfolio analysis for assets: {string.Join(", ", assetList)}. (Full implementation pending)";
        }

        private async Task<string> PerformRiskAssessmentAsync(Dictionary<string, string> parameters)
        {
            var symbol = parameters.GetValueOrDefault("symbol", "SPY");

            // Get data and calculate risk metrics
            var data = await _marketDataService.GetHistoricalDataAsync(symbol, 252); // 1 year
            if (data == null || !data.Any())
                return $"No data available for risk assessment of {symbol}";

            var returns = CalculateReturns(data.Select(d => d.Price).ToList());
            var var95 = CalculateVaR(returns, 0.05);
            var sharpe = CalculateSharpeRatio(returns);

            return $@"
Risk Assessment for {symbol}:
- Value at Risk (95%): {var95:P2}
- Sharpe Ratio: {sharpe:F2}
- Max Drawdown: {CalculateMaxDrawdown(returns):P2}
";
        }

        private async Task<string> GenerateGeneralResearchResponseAsync(string query)
        {
            // Use AI to generate a general research response
            var prompt = $"Provide a helpful research response to this query: {query}";
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.ToString();
        }

        // Helper methods
        private List<double> CalculateReturns(List<double> prices)
        {
            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                returns.Add((prices[i] - prices[i - 1]) / prices[i - 1]);
            }
            return returns;
        }

        private double CalculateVolatility(List<double> returns)
        {
            return returns.Any() ? Math.Sqrt(returns.Variance()) : 0;
        }

        private string CalculateTrend(List<double> returns)
        {
            if (!returns.Any()) return "Unknown";

            var recent = returns.TakeLast(20).ToList();
            var slope = CalculateLinearRegressionSlope(recent);

            if (slope > 0.001) return "Upward";
            if (slope < -0.001) return "Downward";
            return "Sideways";
        }

        private double CalculateLinearRegressionSlope(List<double> data)
        {
            if (!data.Any()) return 0;

            var n = data.Count;
            var sumX = (n * (n - 1.0)) / 2.0;
            var sumY = data.Sum();
            var sumXY = data.Select((y, i) => y * i).Sum();
            var sumXX = data.Select((y, i) => i * i).Sum();

            return (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        }

        private double CalculateVaR(List<double> returns, double confidence)
        {
            if (!returns.Any()) return 0;
            var sorted = returns.OrderBy(r => r).ToList();
            var index = (int)((1 - confidence) * sorted.Count);
            return -sorted[index]; // Negative because VaR is loss
        }

        private double CalculateSharpeRatio(List<double> returns)
        {
            if (!returns.Any()) return 0;
            var avgReturn = returns.Average();
            var volatility = CalculateVolatility(returns);
            var riskFreeRate = 0.02 / 252; // Daily risk-free rate
            return volatility > 0 ? (avgReturn - riskFreeRate) / volatility : 0;
        }

        private double CalculateMaxDrawdown(List<double> returns)
        {
            if (!returns.Any()) return 0;

            var cumulative = new List<double> { 1 };
            foreach (var ret in returns)
            {
                cumulative.Add(cumulative.Last() * (1 + ret));
            }

            var peak = cumulative[0];
            var maxDrawdown = 0.0;

            foreach (var value in cumulative)
            {
                if (value > peak) peak = value;
                var drawdown = (peak - value) / peak;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }

            return maxDrawdown;
        }

        private List<double> ParseDataArray(string data)
        {
            try
            {
                return data.Split(',')
                    .Select(s => double.Parse(s.Trim()))
                    .Where(d => !double.IsNaN(d))
                    .ToList();
            }
            catch
            {
                return new List<double>();
            }
        }

        private QueryIntent ParseIntentResult(string result)
        {
            try
            {
                // Try to parse JSON response from AI
                var cleanedResult = result.Trim();
                
                // Handle JSON in code blocks
                if (cleanedResult.Contains("```json"))
                {
                    var start = cleanedResult.IndexOf("```json") + 7;
                    var end = cleanedResult.IndexOf("```", start);
                    if (end > start)
                    {
                        cleanedResult = cleanedResult.Substring(start, end - start).Trim();
                    }
                }
                else if (cleanedResult.Contains("```"))
                {
                    var start = cleanedResult.IndexOf("```") + 3;
                    var end = cleanedResult.IndexOf("```", start);
                    if (end > start)
                    {
                        cleanedResult = cleanedResult.Substring(start, end - start).Trim();
                    }
                }
                
                // Find JSON object in the response
                var jsonStart = cleanedResult.IndexOf('{');
                var jsonEnd = cleanedResult.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    cleanedResult = cleanedResult.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var json = System.Text.Json.JsonDocument.Parse(cleanedResult);
                    var root = json.RootElement;
                    
                    var type = root.TryGetProperty("type", out var typeElement) 
                        ? typeElement.GetString() ?? "general" 
                        : "general";
                    
                    var parameters = new Dictionary<string, string>();
                    if (root.TryGetProperty("parameters", out var paramsElement))
                    {
                        foreach (var prop in paramsElement.EnumerateObject())
                        {
                            parameters[prop.Name] = prop.Value.ToString();
                        }
                    }
                    
                    var confidence = root.TryGetProperty("confidence", out var confElement) 
                        ? confElement.GetDouble() 
                        : 0.8;
                    
                    return new QueryIntent
                    {
                        Type = type,
                        Parameters = parameters,
                        Confidence = confidence
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing intent: {ex.Message}");
            }
            
            // Fallback: classify as general query
            return new QueryIntent
            {
                Type = "general",
                Parameters = new Dictionary<string, string>(),
                Confidence = 0.5
            };
        }

        private string FormatStatisticalResult(object result, string testType)
        {
            return $"Statistical test result for {testType}: {result}";
        }

        private class QueryIntent
        {
            public required string Type { get; set; }
            public required Dictionary<string, string> Parameters { get; set; }
            public double Confidence { get; set; }
        }
    }
}
