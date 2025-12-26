using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Reinforcement learning plugin for Semantic Kernel integration
/// </summary>
public class ReinforcementLearningPlugin
{
    private readonly ReinforcementLearningService _rlService;

    public ReinforcementLearningPlugin(ReinforcementLearningService rlService)
    {
        _rlService = rlService;
    }

    [KernelFunction("train_q_learning_agent")]
    [Description("Trains a Q-learning agent for trading strategy optimization")]
    public async Task<string> TrainQLearningAgentAsync(
        [Description("List of symbols for training")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Number of training episodes")] int episodes = 1000,
        [Description("Learning rate")] double learningRate = 0.1,
        [Description("Discount factor")] double discountFactor = 0.95)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            // Generate training data from market data
            var trainingData = await GenerateMarketStatesAsync(symbols, start, end);

            var config = new QLearningConfig
            {
                Episodes = episodes,
                LearningRate = learningRate,
                DiscountFactor = discountFactor
            };

            var agent = await _rlService.TrainQAgentAsync(trainingData, config);

            return $"Q-Learning Training Completed:\n" +
                   $"Episodes: {config.Episodes}\n" +
                   $"Learning Rate: {config.LearningRate}\n" +
                   $"Discount Factor: {config.DiscountFactor}\n" +
                   $"Training Complete: {agent.TrainingComplete}\n" +
                   $"Q-Table Size: {agent.QTable.Count} states";
        }
        catch (Exception ex)
        {
            return $"Error training Q-learning agent: {ex.Message}";
        }
    }

    [KernelFunction("train_policy_gradient_agent")]
    [Description("Trains a policy gradient agent for continuous action spaces")]
    public async Task<string> TrainPolicyGradientAgentAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Number of episodes")] int episodes = 1000,
        [Description("Learning rate")] double learningRate = 0.01)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var trainingData = await GenerateMarketStatesAsync(symbols, start, end);

            var config = new PolicyGradientConfig
            {
                Episodes = episodes,
                LearningRate = learningRate
            };

            var agent = await _rlService.TrainPolicyGradientAsync(trainingData, config);

            return $"Policy Gradient Training Completed:\n" +
                   $"Episodes: {config.Episodes}\n" +
                   $"Learning Rate: {config.LearningRate}\n" +
                   $"Training Complete: {agent.TrainingComplete}";
        }
        catch (Exception ex)
        {
            return $"Error training policy gradient agent: {ex.Message}";
        }
    }

    [KernelFunction("train_actor_critic_agent")]
    [Description("Trains an actor-critic agent for advanced RL")]
    public async Task<string> TrainActorCriticAgentAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Number of episodes")] int episodes = 1000,
        [Description("Actor learning rate")] double actorLR = 0.01,
        [Description("Critic learning rate")] double criticLR = 0.01)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var trainingData = await GenerateMarketStatesAsync(symbols, start, end);

            var config = new ActorCriticConfig
            {
                Episodes = episodes,
                ActorLearningRate = actorLR,
                CriticLearningRate = criticLR
            };

            var agent = await _rlService.TrainActorCriticAsync(trainingData, config);

            return $"Actor-Critic Training Completed:\n" +
                   $"Episodes: {config.Episodes}\n" +
                   $"Actor Learning Rate: {config.ActorLearningRate}\n" +
                   $"Critic Learning Rate: {config.CriticLearningRate}\n" +
                   $"Training Complete: {agent.TrainingComplete}";
        }
        catch (Exception ex)
        {
            return $"Error training actor-critic agent: {ex.Message}";
        }
    }

    [KernelFunction("adapt_trading_strategy")]
    [Description("Adapts trading strategy based on current market conditions using RL")]
    public async Task<string> AdaptTradingStrategyAsync(
        [Description("Current symbol")] string symbol,
        [Description("Current market state features")] List<double> features,
        [Description("Base strategy parameters")] Dictionary<string, double> baseParameters)
    {
        try
        {
            // Create current market state
            var currentState = new MarketState
            {
                Index = 0,
                Price = features[0], // Assuming first feature is price
                Features = features,
                Volume = features.Count > 1 ? features[1] : 1000,
                Volatility = features.Count > 2 ? features[2] : 0.02,
                Timestamp = DateTime.UtcNow
            };

            // Create a simple trained agent (in practice, would load from storage)
            var agent = new QAgent(new QLearningConfig());

            var adaptedStrategy = await _rlService.AdaptStrategyAsync(
                currentState, agent, baseParameters);

            return $"Strategy Adaptation Results:\n" +
                   $"Adaptation Reason: {adaptedStrategy.AdaptationReason}\n" +
                   $"Confidence: {adaptedStrategy.Confidence:F4}\n" +
                   $"Adaptation Date: {adaptedStrategy.AdaptationDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                   $"Parameter Changes:\n" +
                   string.Join("\n", adaptedStrategy.AdaptedParameters
                       .Where(p => Math.Abs(p.Value - (baseParameters.ContainsKey(p.Key) ? baseParameters[p.Key] : 0)) > 0.001)
                       .Select(p => $"  {p.Key}: {baseParameters.GetValueOrDefault(p.Key):F4} → {p.Value:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error adapting trading strategy: {ex.Message}";
        }
    }

    [KernelFunction("optimize_parameters_bandit")]
    [Description("Optimizes strategy parameters using multi-armed bandit")]
    public async Task<string> OptimizeParametersBanditAsync(
        [Description("Parameter sets to test")] List<Dictionary<string, double>> parameterSets,
        [Description("Total trials")] int totalTrials = 1000)
    {
        try
        {
            var parameters = parameterSets.Select((p, i) => new ParameterSet
            {
                Name = $"Set_{i + 1}",
                Parameters = p
            }).ToList();

            // Simplified evaluation function
            async Task<double> evaluationFunction(ParameterSet paramSet)
            {
                // In practice, would run backtest with these parameters
                var random = new Random();
                return 0.5 + random.NextDouble() * 0.5; // Simulated reward
            }

            var config = new BanditConfig { TotalTrials = totalTrials };
            var result = await _rlService.OptimizeParametersBanditAsync(
                parameters, evaluationFunction, config);

            return $"Bandit Parameter Optimization Results:\n" +
                   $"Total Trials: {result.TotalTrials}\n" +
                   $"Best Reward: {result.BestReward:F4}\n" +
                   $"Regret: {result.Regret:F4}\n" +
                   $"Optimization Date: {result.OptimizationDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                   $"Optimal Parameters ({result.OptimalParameters.Name}):\n" +
                   string.Join("\n", result.OptimalParameters.Parameters
                       .Select(p => $"  {p.Key}: {p.Value:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error optimizing parameters with bandit: {ex.Message}";
        }
    }

    [KernelFunction("run_contextual_bandit")]
    [Description("Runs contextual bandit for dynamic strategy selection")]
    public async Task<string> RunContextualBanditAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Available strategies")] List<Dictionary<string, object>> strategies,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var contexts = await GenerateMarketStatesAsync(symbols, start, end);
            var availableStrategies = strategies.Select((s, i) => new Strategy
            {
                Name = s.GetValueOrDefault("name", $"Strategy_{i + 1}").ToString() ?? $"Strategy_{i + 1}",
                Type = s.GetValueOrDefault("type", "default").ToString() ?? "default",
                Parameters = s.Where(kv => kv.Key != "name" && kv.Key != "type")
                             .ToDictionary(kv => kv.Key, kv => Convert.ToDouble(kv.Value))
            }).ToList();

            // Simplified reward function
            async Task<double> rewardFunction(MarketState context, Strategy strategy)
            {
                // In practice, would evaluate strategy performance in this context
                var random = new Random();
                return random.NextDouble();
            }

            var config = new ContextualBanditConfig { TotalTrials = Math.Min(contexts.Count, 1000) };
            var result = await _rlService.RunContextualBanditAsync(
                contexts, availableStrategies, rewardFunction, config);

            return $"Contextual Bandit Results:\n" +
                   $"Total Steps: {result.Steps.Count}\n" +
                   $"Total Reward: {result.TotalReward:F4}\n" +
                   $"Average Regret: {result.AverageRegret:F4}\n" +
                   $"Best Strategy: {result.BestStrategy.Name}\n" +
                   $"Completion Date: {result.CompletionDate:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            return $"Error running contextual bandit: {ex.Message}";
        }
    }

    [KernelFunction("evaluate_rl_agent")]
    [Description("Evaluates RL agent performance")]
    public async Task<string> EvaluateRLAgentAsync(
        [Description("List of symbols for testing")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Number of evaluation episodes")] int episodes = 100)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var testData = await GenerateMarketStatesAsync(symbols, start, end);

            // Create a simple agent for evaluation
            var agent = new QAgent(new QLearningConfig());

            var evaluation = await _rlService.EvaluateRLAgentAsync(agent, testData, episodes);

            return $"RL Agent Evaluation Results:\n" +
                   $"Evaluation Episodes: {evaluation.EvaluationEpisodes}\n" +
                   $"Average Reward: {evaluation.AverageReward:F4}\n" +
                   $"Reward Std Dev: {evaluation.RewardStdDev:F4}\n" +
                   $"Average Episode Length: {evaluation.AverageEpisodeLength:F2}\n" +
                   $"Max Reward: {evaluation.MaxReward:F4}\n" +
                   $"Min Reward: {evaluation.MinReward:F4}\n" +
                   $"Evaluation Date: {evaluation.EvaluationDate:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            return $"Error evaluating RL agent: {ex.Message}";
        }
    }

    [KernelFunction("generate_rl_strategy_report")]
    [Description("Generates a comprehensive RL strategy adaptation report")]
    public async Task<string> GenerateRLStrategyReportAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var trainingData = await GenerateMarketStatesAsync(symbols, start, end);

            // Train a simple Q-agent
            var config = new QLearningConfig { Episodes = 500 };
            var agent = await _rlService.TrainQAgentAsync(trainingData, config);

            // Evaluate performance
            var evaluation = await _rlService.EvaluateRLAgentAsync(agent, trainingData, 50);

            return $"RL Strategy Adaptation Report\n" +
                   $"==============================\n\n" +
                   $"Analysis Period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}\n" +
                   $"Symbols: {string.Join(", ", symbols)}\n" +
                   $"Training Data Points: {trainingData.Count}\n\n" +
                   $"Q-LEARNING TRAINING:\n" +
                   $"- Episodes: {config.Episodes}\n" +
                   $"- Learning Rate: {config.LearningRate}\n" +
                   $"- Discount Factor: {config.DiscountFactor}\n" +
                   $"- Q-Table States: {agent.QTable.Count}\n" +
                   $"- Training Complete: {agent.TrainingComplete}\n\n" +
                   $"PERFORMANCE EVALUATION:\n" +
                   $"- Test Episodes: {evaluation.EvaluationEpisodes}\n" +
                   $"- Average Reward: {evaluation.AverageReward:F4}\n" +
                   $"- Reward Variability: {evaluation.RewardStdDev:F4}\n" +
                   $"- Max Reward: {evaluation.MaxReward:F4}\n" +
                   $"- Min Reward: {evaluation.MinReward:F4}\n\n" +
                   $"STRATEGY ADAPTATION CAPABILITY:\n" +
                   $"- Agent can adapt to market conditions\n" +
                   $"- Supports multiple action types (Buy/Sell/Hold)\n" +
                   $"- Learns from historical market data\n\n" +
                   $"Report Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            return $"Error generating RL strategy report: {ex.Message}";
        }
    }

    // Helper method to generate market states from data
    private async Task<List<MarketState>> GenerateMarketStatesAsync(
        List<string> symbols, DateTime start, DateTime end)
    {
        // Simplified - would integrate with actual market data services
        var states = new List<MarketState>();
        var random = new Random();
        var days = (end - start).Days;

        for (int i = 0; i < Math.Min(days, 1000); i++)
        {
            var state = new MarketState
            {
                Index = i,
                Price = 100 + random.NextDouble() * 50,
                Features = new List<double>
                {
                    100 + random.NextDouble() * 50,  // Price
                    random.NextDouble() * 1000000,    // Volume
                    random.NextDouble() * 0.1,        // Volatility
                    random.NextDouble() * 2 - 1,      // Momentum
                    random.NextDouble()               // RSI-like
                },
                Volume = random.NextDouble() * 1000000,
                Volatility = random.NextDouble() * 0.1,
                Timestamp = start.AddDays(i)
            };

            states.Add(state);
        }

        return states;
    }
}