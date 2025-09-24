using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class StrategyGeneratorService
    {
        private readonly LLMRouterService _llmRouter;
        private readonly ILogger<StrategyGeneratorService> _logger;

        public StrategyGeneratorService(LLMRouterService llmRouter, ILogger<StrategyGeneratorService> logger)
        {
            _llmRouter = llmRouter;
            _logger = logger;
        }

        public async Task<string> GenerateStrategyAsync(string inputData, string sourceType)
        {
            string prompt = BuildStrategyPrompt(inputData, sourceType);
            string strategy = await _llmRouter.GetChatCompletionAsync(prompt, "deepseek");

            // Save to file
            string fileName = $"{sourceType}_Strategy_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string filePath = Path.Combine("Extracted_Strategies", fileName);
            await File.WriteAllTextAsync(filePath, strategy);

            _logger.LogInformation($"Strategy saved to {filePath}");
            return strategy;
        }

        private string BuildStrategyPrompt(string inputData, string sourceType)
        {
            return $@"
You are an expert quantitative researcher specializing in algorithmic trading strategies.

Based on the following {sourceType} data, extract key trading signals, market insights, and develop a comprehensive trading strategy in the format similar to the sample provided.

Sample Format:
[Include the sample structure here, but abbreviated for brevity]

Input Data:
{inputData}

Generate a detailed strategy including:
- Strategy Parameters
- Entry Conditions
- Exit Framework
- Risk Controls
- Advanced Indicators
- Data Pipeline
- Backtest Setup
- Known Constraints
- Implementation Requirements
- Disclaimer

Ensure the strategy is actionable, with specific parameters, and includes risk management.
";
        }
    }
}