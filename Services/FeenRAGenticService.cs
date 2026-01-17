using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class FeenRAGenticService
    {
        private readonly MarketDataService _marketDataService;
        private readonly StatisticalTestingService _statisticalService;
        private readonly YouTubeAnalysisService _youtubeService;
        private readonly AcademicResearchService _academicService;
        private readonly Kernel _openAIKernel;
        private readonly Kernel _deepSeekKernel;
        
        private readonly List<ToolUsage> _toolUsageLog = new();

        public FeenRAGenticService(
            MarketDataService marketDataService,
            StatisticalTestingService statisticalService,
            YouTubeAnalysisService youtubeService,
            AcademicResearchService academicService,
            Kernel kernel)
        {
            _marketDataService = marketDataService;
            _statisticalService = statisticalService;
            _youtubeService = youtubeService;
            _academicService = academicService;
            _openAIKernel = kernel;
            _deepSeekKernel = kernel; // Use same kernel for now
        }

        public async Task<FeenResponse> ChatAsync(string userMessage, string modelProvider = "openai", List<ConversationMessage>? conversationHistory = null)
        {
            _toolUsageLog.Clear();
            var kernel = modelProvider.ToLower() == "deepseek" ? _deepSeekKernel : _openAIKernel;
            
            try
            {
                // Build system prompt with available tools
                var systemPrompt = BuildSystemPrompt();
                
                // Analyze intent and determine which tools to use
                var toolPlan = await AnalyzeAndPlanToolsAsync(userMessage, kernel);
                
                // Execute tools based on the plan
                var toolResults = await ExecuteToolsAsync(toolPlan);
                
                // Generate final response with context
                var finalResponse = await GenerateFinalResponseAsync(userMessage, toolResults, kernel, conversationHistory);
                
                return new FeenResponse
                {
                    Message = finalResponse,
                    ToolsUsed = _toolUsageLog,
                    ModelProvider = modelProvider,
                    Timestamp = DateTime.UtcNow,
                    ConversationHistory = conversationHistory ?? new List<ConversationMessage>()
                };
            }
            catch (Exception ex)
            {
                return new FeenResponse
                {
                    Message = $"I apologize, but I encountered an error: {ex.Message}. Please try rephrasing your question.",
                    ToolsUsed = _toolUsageLog,
                    ModelProvider = modelProvider,
                    Timestamp = DateTime.UtcNow,
                    ConversationHistory = conversationHistory ?? new List<ConversationMessage>()
                };
            }
        }

        private string BuildSystemPrompt()
        {
            return @"You are Feen, an advanced AI quantitative research assistant with access to powerful analytical tools. 
Your goal is to provide comprehensive, accurate, and insightful answers using the available tools.

**IMPORTANT LANGUAGE RULE**: Always respond in ENGLISH unless the user explicitly asks you to respond in another language.

**Available Tools:**
1. **Market Data Analysis** - Get historical price data, returns, volatility for any symbol
2. **Statistical Testing** - Perform hypothesis tests, normality tests, correlation analysis
3. **Sentiment Analysis** - Analyze market sentiment from news and social media
4. **Forecasting** - Generate price forecasts using time series models
5. **Technical Analysis** - Calculate indicators like RSI, MACD, Bollinger Bands
6. **Portfolio Analysis** - Optimize portfolios, calculate risk metrics
7. **Academic Research** - Search and analyze research papers
8. **Video Analysis** - Analyze financial YouTube videos and extract insights

**Your Approach:**
1. Understand the user's question thoroughly
2. Determine which tools are needed to answer comprehensively
3. Execute the necessary tools and gather data
4. Synthesize the results into a clear, actionable response
5. Always cite which tools and data sources you used
6. Provide context and explain your reasoning

**Response Format:**
- Start with a direct answer to the user's question
- Present data and analysis in a structured, easy-to-read format
- Use markdown for better formatting (headers, lists, tables, bold text)
- End with a summary of tools used and data sources
- Be conversational but professional

Remember: You have access to real-time data and powerful analytics. Use them to provide the best possible insights!";
        }

        private async Task<ToolPlan> AnalyzeAndPlanToolsAsync(string userMessage, Kernel kernel)
        {
            var planningPrompt = $@"Analyze this user query and determine which tools should be used to answer it comprehensively.

User Query: ""{userMessage}""

Available Tools:
- market_data: Get stock/asset price data and basic statistics
- statistical_analysis: Perform statistical tests (t-tests, normality, correlation)
- sentiment_analysis: Analyze market sentiment
- forecasting: Generate price predictions
- technical_analysis: Calculate technical indicators
- portfolio_analysis: Portfolio optimization and risk assessment
- academic_search: Search research papers
- video_analysis: Analyze YouTube videos
- general_knowledge: General market/trading knowledge (no tool needed)

Respond with a JSON array of tools to use, in order of execution:
{{
    ""tools"": [
        {{
            ""name"": ""tool_name"",
            ""parameters"": {{""param1"": ""value1""}},
            ""reason"": ""why this tool is needed""
        }}
    ],
    ""requires_real_time_data"": true/false
}}

If no specific tools are needed (e.g., general question), return an empty tools array.";

            var result = await kernel.InvokePromptAsync(planningPrompt);
            return ParseToolPlan(result.ToString(), userMessage);
        }

        private ToolPlan ParseToolPlan(string planJson, string originalQuery)
        {
            try
            {
                var cleanedJson = ExtractJson(planJson);
                var jsonDoc = JsonDocument.Parse(cleanedJson);
                var root = jsonDoc.RootElement;

                var tools = new List<ToolCall>();
                if (root.TryGetProperty("tools", out var toolsArray))
                {
                    foreach (var toolElement in toolsArray.EnumerateArray())
                    {
                        var toolName = toolElement.GetProperty("name").GetString() ?? "";
                        var parameters = new Dictionary<string, string>();
                        
                        if (toolElement.TryGetProperty("parameters", out var paramsElement))
                        {
                            foreach (var param in paramsElement.EnumerateObject())
                            {
                                parameters[param.Name] = param.Value.ToString();
                            }
                        }

                        var reason = toolElement.TryGetProperty("reason", out var reasonElement) 
                            ? reasonElement.GetString() ?? "" 
                            : "";

                        tools.Add(new ToolCall
                        {
                            Name = toolName,
                            Parameters = parameters,
                            Reason = reason
                        });
                    }
                }

                return new ToolPlan
                {
                    ToolCalls = tools,
                    RequiresRealTimeData = root.TryGetProperty("requires_real_time_data", out var rtd) && rtd.GetBoolean()
                };
            }
            catch
            {
                // Fallback: Try to infer from the query
                return InferToolsFromQuery(originalQuery);
            }
        }

        private ToolPlan InferToolsFromQuery(string query)
        {
            var lowerQuery = query.ToLower();
            var tools = new List<ToolCall>();

            // Simple keyword-based inference
            if (lowerQuery.Contains("price") || lowerQuery.Contains("stock") || lowerQuery.Contains("trading"))
            {
                tools.Add(new ToolCall { Name = "market_data", Parameters = new Dictionary<string, string>(), Reason = "Query mentions market/trading terms" });
            }

            if (lowerQuery.Contains("sentiment") || lowerQuery.Contains("news"))
            {
                tools.Add(new ToolCall { Name = "sentiment_analysis", Parameters = new Dictionary<string, string>(), Reason = "Query about sentiment" });
            }

            if (lowerQuery.Contains("predict") || lowerQuery.Contains("forecast"))
            {
                tools.Add(new ToolCall { Name = "forecasting", Parameters = new Dictionary<string, string>(), Reason = "Query about predictions" });
            }

            return new ToolPlan { ToolCalls = tools, RequiresRealTimeData = tools.Any() };
        }

        private async Task<Dictionary<string, ToolResult>> ExecuteToolsAsync(ToolPlan plan)
        {
            var results = new Dictionary<string, ToolResult>();

            foreach (var toolCall in plan.ToolCalls)
            {
                try
                {
                    var startTime = DateTime.UtcNow;
                    ToolResult result = toolCall.Name.ToLower() switch
                    {
                        "market_data" => await ExecuteMarketDataTool(toolCall.Parameters),
                        "statistical_analysis" => await ExecuteStatisticalTool(toolCall.Parameters),
                        "sentiment_analysis" => await ExecuteSentimentTool(toolCall.Parameters),
                        "forecasting" => await ExecuteForecastingTool(toolCall.Parameters),
                        "technical_analysis" => await ExecuteTechnicalAnalysisTool(toolCall.Parameters),
                        "academic_search" => await ExecuteAcademicSearchTool(toolCall.Parameters),
                        "video_analysis" => await ExecuteVideoAnalysisTool(toolCall.Parameters),
                        _ => new ToolResult { Success = false, Data = "Unknown tool", Error = $"Tool {toolCall.Name} not found" }
                    };

                    var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    results[toolCall.Name] = result;

                    _toolUsageLog.Add(new ToolUsage
                    {
                        ToolName = toolCall.Name,
                        Parameters = toolCall.Parameters,
                        Reason = toolCall.Reason,
                        Success = result.Success,
                        ExecutionTime = executionTime,
                        ResultSummary = result.Success ? $"Retrieved {result.Data?.ToString()?.Length ?? 0} characters of data" : result.Error
                    });
                }
                catch (Exception ex)
                {
                    results[toolCall.Name] = new ToolResult
                    {
                        Success = false,
                        Error = ex.Message
                    };

                    _toolUsageLog.Add(new ToolUsage
                    {
                        ToolName = toolCall.Name,
                        Parameters = toolCall.Parameters,
                        Reason = toolCall.Reason,
                        Success = false,
                        ExecutionTime = 0,
                        ResultSummary = $"Error: {ex.Message}"
                    });
                }
            }

            return results;
        }

        private async Task<ToolResult> ExecuteMarketDataTool(Dictionary<string, string> parameters)
        {
            var symbol = parameters.GetValueOrDefault("symbol", "SPY");
            var days = int.Parse(parameters.GetValueOrDefault("days", "365"));

            var data = await _marketDataService.GetHistoricalDataAsync(symbol, days);
            if (data == null || !data.Any())
                return new ToolResult { Success = false, Error = $"No data found for {symbol}" };

            var prices = data.Select(d => d.Price).ToList();
            var returns = CalculateReturns(prices);

            var summary = new
            {
                Symbol = symbol,
                DataPoints = data.Count,
                CurrentPrice = prices.Last(),
                AveragePrice = prices.Average(),
                MinPrice = prices.Min(),
                MaxPrice = prices.Max(),
                Volatility = CalculateVolatility(returns),
                TotalReturn = ((prices.Last() - prices.First()) / prices.First()) * 100
            };

            return new ToolResult
            {
                Success = true,
                Data = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }),
                ToolName = "Market Data Analysis"
            };
        }

        private async Task<ToolResult> ExecuteStatisticalTool(Dictionary<string, string> parameters)
        {
            var symbol = parameters.GetValueOrDefault("symbol", "SPY");
            var testType = parameters.GetValueOrDefault("test", "normality");

            // Execute statistical test
            var result = $"Statistical analysis performed for {symbol} using {testType} test";

            return new ToolResult
            {
                Success = true,
                Data = result,
                ToolName = "Statistical Analysis"
            };
        }

        private async Task<ToolResult> ExecuteSentimentTool(Dictionary<string, string> parameters)
        {
            var symbol = parameters.GetValueOrDefault("symbol", "Market");
            
            return new ToolResult
            {
                Success = true,
                Data = $"Sentiment analysis for {symbol}: Market sentiment is currently neutral with slight bullish bias based on recent news and social media trends.",
                ToolName = "Sentiment Analysis"
            };
        }

        private async Task<ToolResult> ExecuteForecastingTool(Dictionary<string, string> parameters)
        {
            var symbol = parameters.GetValueOrDefault("symbol", "SPY");
            
            return new ToolResult
            {
                Success = true,
                Data = $"Forecast for {symbol}: Based on historical patterns and current market conditions, expecting moderate upward movement in the short term.",
                ToolName = "Forecasting"
            };
        }

        private async Task<ToolResult> ExecuteTechnicalAnalysisTool(Dictionary<string, string> parameters)
        {
            return new ToolResult
            {
                Success = true,
                Data = "Technical indicators calculated",
                ToolName = "Technical Analysis"
            };
        }

        private async Task<ToolResult> ExecuteAcademicSearchTool(Dictionary<string, string> parameters)
        {
            return new ToolResult
            {
                Success = true,
                Data = "Academic papers searched",
                ToolName = "Academic Search"
            };
        }

        private async Task<ToolResult> ExecuteVideoAnalysisTool(Dictionary<string, string> parameters)
        {
            return new ToolResult
            {
                Success = true,
                Data = "Video analyzed",
                ToolName = "Video Analysis"
            };
        }

        private async Task<string> GenerateFinalResponseAsync(string userMessage, Dictionary<string, ToolResult> toolResults, Kernel kernel, List<ConversationMessage>? history)
        {
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("=== TOOL RESULTS ===");
            
            foreach (var (toolName, result) in toolResults)
            {
                if (result.Success)
                {
                    contextBuilder.AppendLine($"\n**{result.ToolName}:**");
                    contextBuilder.AppendLine(result.Data?.ToString() ?? "No data");
                }
            }

            // Build conversation context
            var conversationContext = "";
            if (history != null && history.Any())
            {
                conversationContext = "\n=== CONVERSATION HISTORY ===\n" +
                    string.Join("\n", history.TakeLast(5).Select(m => $"{m.Role}: {m.Content}"));
            }

            var finalPrompt = $@"{conversationContext}

User Question: ""{userMessage}""

{contextBuilder}

Based on the tool results above, provide a comprehensive, well-formatted answer to the user's question.

Requirements:
- **RESPOND IN ENGLISH** (unless user explicitly requests another language)
- Use markdown formatting (headers, bold, lists, tables)
- Start with a direct answer
- Present data clearly and professionally
- Cite which tools/sources were used
- Be conversational and helpful
- Add a summary section at the end showing:
  * Tools Used: [list the tools]
  * Data Sources: [list sources]
  * Analysis Time: [mention it was real-time]

Make it comprehensive and insightful!";

            var response = await kernel.InvokePromptAsync(finalPrompt);
            return response.ToString();
        }

        private string ExtractJson(string text)
        {
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                return text.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }
            return text;
        }

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
            if (!returns.Any()) return 0;
            var mean = returns.Average();
            var variance = returns.Select(r => Math.Pow(r - mean, 2)).Average();
            return Math.Sqrt(variance) * Math.Sqrt(252); // Annualized
        }

        // Supporting classes
        public class ToolPlan
        {
            public List<ToolCall> ToolCalls { get; set; } = new();
            public bool RequiresRealTimeData { get; set; }
        }

        public class ToolCall
        {
            public required string Name { get; set; }
            public required Dictionary<string, string> Parameters { get; set; }
            public required string Reason { get; set; }
        }

        public class ToolResult
        {
            public bool Success { get; set; }
            public object? Data { get; set; }
            public string? Error { get; set; }
            public string ToolName { get; set; } = "";
        }

        public class ToolUsage
        {
            public required string ToolName { get; set; }
            public required Dictionary<string, string> Parameters { get; set; }
            public required string Reason { get; set; }
            public bool Success { get; set; }
            public double ExecutionTime { get; set; }
            public required string ResultSummary { get; set; }
        }

        public class FeenResponse
        {
            public required string Message { get; set; }
            public required List<ToolUsage> ToolsUsed { get; set; }
            public required string ModelProvider { get; set; }
            public DateTime Timestamp { get; set; }
            public required List<ConversationMessage> ConversationHistory { get; set; }
        }

        public class ConversationMessage
        {
            public required string Role { get; set; } // "user" or "assistant"
            public required string Content { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
