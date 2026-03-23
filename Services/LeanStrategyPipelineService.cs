using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services;

public class LeanStrategyPipelineService
{
    private readonly ILogger<LeanStrategyPipelineService> _logger;
    private readonly DeepSeekService _deepSeekService;

    public LeanStrategyPipelineService(ILogger<LeanStrategyPipelineService> logger, DeepSeekService deepSeekService)
    {
        _logger = logger;
        _deepSeekService = deepSeekService;
    }

    public StrategyPlan ParseStrategyText(string strategyText)
    {
        var plan = new StrategyPlan
        {
            StrategyName = "Generated Strategy",
            RawText = strategyText
        };

        if (string.IsNullOrWhiteSpace(strategyText))
        {
            plan.ParseWarnings.Add("Strategy text is empty.");
            return plan;
        }

        // Extract symbol
        var symbolMatch = Regex.Match(strategyText, @"Symbol:\s*([A-Z0-9\.\-]+)", RegexOptions.IgnoreCase);
        if (symbolMatch.Success)
        {
            plan.Symbol = symbolMatch.Groups[1].Value.Trim().ToUpperInvariant();
        }
        else
        {
            // Fallback to first uppercase ticker-like token
            var tickerMatch = Regex.Match(strategyText, @"\b([A-Z]{2,6}(?:USDT)?)\b");
            if (tickerMatch.Success)
            {
                plan.Symbol = tickerMatch.Groups[1].Value.Trim().ToUpperInvariant();
            }
        }

        // Dates
        var startDate = ExtractDate(strategyText, "Start Date");
        var endDate = ExtractDate(strategyText, "End Date");
        if (startDate.HasValue) plan.StartDate = startDate.Value;
        if (endDate.HasValue) plan.EndDate = endDate.Value;

        // Initial capital
        var capitalMatch = Regex.Match(strategyText, @"Initial\s+Capital:\s*\$?([0-9,]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
        if (capitalMatch.Success && decimal.TryParse(capitalMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var capital))
        {
            plan.InitialCapital = capital;
        }

        // Resolution
        if (Regex.IsMatch(strategyText, "daily", RegexOptions.IgnoreCase))
        {
            plan.Resolution = "daily";
        }
        else if (Regex.IsMatch(strategyText, "hour", RegexOptions.IgnoreCase))
        {
            plan.Resolution = "hour";
        }
        else if (Regex.IsMatch(strategyText, "minute", RegexOptions.IgnoreCase))
        {
            plan.Resolution = "minute";
        }

        // Position size
        var sizeMatch = Regex.Match(strategyText, @"Use\s*(\d{1,3})\s*%\s*of\s*available\s*cash", RegexOptions.IgnoreCase);
        if (sizeMatch.Success && decimal.TryParse(sizeMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var sizePercent))
        {
            plan.PositionSizePercent = sizePercent;
        }

        // Name
        if (!string.IsNullOrWhiteSpace(plan.Symbol))
        {
            plan.StrategyName = $"{plan.Symbol} Strategy";
        }

        if (string.IsNullOrWhiteSpace(plan.Symbol)) plan.ParseWarnings.Add("Could not detect a symbol. Please provide one explicitly.");
        if (!plan.StartDate.HasValue) plan.ParseWarnings.Add("Could not detect start date. Defaulting to last 2 years.");
        if (!plan.EndDate.HasValue) plan.ParseWarnings.Add("Could not detect end date. Defaulting to today.");

        if (!plan.StartDate.HasValue) plan.StartDate = DateTime.UtcNow.AddYears(-2).Date;
        if (!plan.EndDate.HasValue) plan.EndDate = DateTime.UtcNow.Date;

        return plan;
    }

    public DataPlan BuildDataPlan(StrategyPlan strategy)
    {
        var isCrypto = strategy.Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
            || strategy.Symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase) && strategy.Symbol.Length > 6;

        var source = isCrypto ? "binance" : "alpaca";
        var assetClass = isCrypto ? "crypto" : "equity";

        return new DataPlan
        {
            Symbol = strategy.Symbol,
            AssetClass = assetClass,
            PreferredSource = source,
            Resolution = strategy.Resolution,
            StartDate = strategy.StartDate ?? DateTime.UtcNow.AddYears(-2).Date,
            EndDate = strategy.EndDate ?? DateTime.UtcNow.Date,
            SuggestedCommand = BuildPipelineCommand(strategy, source),
            Reasoning = isCrypto
                ? "Crypto symbol detected; Binance pipeline is the preferred downloader."
                : "US equity symbol detected; Alpaca pipeline is the preferred downloader."
        };
    }

    public async Task<string> GenerateLeanPythonAlgorithmAsync(StrategyPlan strategy)
    {
        var className = Regex.Replace(strategy.StrategyName, "[^a-zA-Z0-9]", string.Empty);
        if (string.IsNullOrWhiteSpace(className)) className = "GeneratedStrategy";

        var startDate = strategy.StartDate ?? DateTime.UtcNow.AddYears(-2).Date;
        var endDate = strategy.EndDate ?? DateTime.UtcNow.Date;

        var resolution = strategy.Resolution.ToLowerInvariant() switch
        {
            "minute" => "Resolution.Minute",
            "hour" => "Resolution.Hour",
            _ => "Resolution.Daily"
        };

        var symbol = strategy.Symbol.ToUpperInvariant();

        // Use DeepSeek to generate the algorithm logic based on the strategy description
        var prompt = $@"
Generate a complete QuantConnect Lean Python algorithm based on the following strategy description.

Strategy Description:
{strategy.RawText}

Key Parameters to Use:
- Symbol: {symbol}
- Start Date: {startDate:yyyy-MM-dd}
- End Date: {endDate:yyyy-MM-dd}
- Resolution: {resolution}
- Initial Cash: {strategy.InitialCapital}
- Position Size Percent: {strategy.PositionSizePercent}

Requirements:
- The algorithm must be a valid Python class inheriting from QCAlgorithm.
- Include proper initialization with set_start_date, set_end_date, set_cash, add_equity, set_benchmark.
- Implement on_data method with the trading logic from the strategy.
- Use appropriate indicators and logic as described. Do not use RSI unless explicitly mentioned in the strategy description.
- For EMA crossover strategies, use ExponentialMovingAverage indicators with the specified periods.
- Include logging for buy/sell signals.
- In OnEndOfAlgorithm, log the final portfolio value and total number of filled orders.
- Use proper Lean API calls (e.g., self.set_holdings, self.liquidate).
- If the strategy is too complex for Lean (e.g., requires external APIs or ML models), provide a simplified version with comments explaining limitations.
- For counting orders, use: len(list(self.Transactions.GetOrders(lambda x: x.Status == OrderStatus.Filled)))

Output only the Python code, no explanations.
";

        try
        {
            var generatedCode = await _deepSeekService.GetChatCompletionAsync(prompt);
            // Clean up the response if needed
            generatedCode = generatedCode.Trim();
            if (generatedCode.StartsWith("```python")) generatedCode = generatedCode.Substring(10);
            if (generatedCode.EndsWith("```")) generatedCode = generatedCode.Substring(0, generatedCode.Length - 3);
            generatedCode = generatedCode.Trim();

            return generatedCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate algorithm with DeepSeek");
            // Fallback to a basic template
            return GenerateFallbackAlgorithm(strategy, className, startDate, endDate, resolution, symbol);
        }
    }

    private string GenerateFallbackAlgorithm(StrategyPlan strategy, string className, DateTime startDate, DateTime endDate, string resolution, string symbol)
    {
        var sb = new StringBuilder();
        sb.AppendLine("from AlgorithmImports import *");
        sb.AppendLine();
        sb.AppendLine($"class {className}(QCAlgorithm):");
        sb.AppendLine("    def initialize(self):");
        sb.AppendLine($"        self.set_start_date({startDate.Year}, {startDate.Month}, {startDate.Day})");
        sb.AppendLine($"        self.set_end_date({endDate.Year}, {endDate.Month}, {endDate.Day})");
        sb.AppendLine($"        self.set_cash({strategy.InitialCapital.ToString(CultureInfo.InvariantCulture)})");
        sb.AppendLine();
        sb.AppendLine($"        self.symbol = self.add_equity(\"{symbol}\", {resolution}).symbol");
        sb.AppendLine("        self.set_benchmark(lambda time: self.securities[self.symbol].price)");
        sb.AppendLine();
        sb.AppendLine("    def on_data(self, data: Slice):");
        sb.AppendLine("        # Placeholder logic - strategy too complex for automatic generation");
        sb.AppendLine("        pass");
        sb.AppendLine();
        sb.AppendLine("    def on_end_of_algorithm(self):");
        sb.AppendLine("        self.log(f\"Final Portfolio Value: {self.portfolio.total_portfolio_value:.2f}\")");

        return sb.ToString();
    }

    public async Task<LeanExecutionResult> RunLeanBacktestAsync(string algorithmFilePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        // Prefer dockerized LEAN engine to avoid local runtime drift.
        if (!await CommandExistsAsync("docker", cancellationToken))
        {
            return new LeanExecutionResult
            {
                Success = false,
                Message = "Docker is not installed or not available in PATH. Cannot run Lean backtest engine.",
                OutputDirectory = outputDirectory
            };
        }

        var repoRoot = Directory.GetCurrentDirectory();
        // Find project root: current dir is /WebApp/Server when running ASP.NET, need to go up 2 levels
        var currentDir = Directory.GetCurrentDirectory();
        repoRoot = currentDir.Contains("WebApp/Server") || currentDir.Contains("WebApp\\Server")
            ? Path.Combine(currentDir, "..", "..")
            : currentDir;
        repoRoot = Path.GetFullPath(repoRoot); // Normalize the path

        // Check if the Lean Docker image exists
        var imageCheck = await RunProcessAsync("docker", "images quantconnect/lean --format \"{{.Repository}}:{{.Tag}}\"", repoRoot, cancellationToken);
        if (imageCheck.ExitCode != 0 || string.IsNullOrWhiteSpace(imageCheck.StdOut))
        {
            _logger.LogWarning("Lean Docker image not found. Attempting to pull...");
            var pullResult = await RunProcessAsync("docker", "pull quantconnect/lean:latest", repoRoot, cancellationToken);
            if (pullResult.ExitCode != 0)
            {
                return new LeanExecutionResult
                {
                    Success = false,
                    Message = $"Failed to pull Lean Docker image: {pullResult.StdErr}",
                    OutputDirectory = outputDirectory
                };
            }
        }

        var dataFolder = Path.Combine(repoRoot, "data");
        if (!Directory.Exists(dataFolder))
        {
            _logger.LogWarning("Data folder not found at {Path}. Trying alternative location...", dataFolder);
            // Fallback: try current directory
            dataFolder = Path.Combine(currentDir, "data");
        }

        _logger.LogInformation("Using data folder: {DataFolder}", dataFolder);
        _logger.LogInformation("Data folder exists: {Exists}", Directory.Exists(dataFolder));

        // LEAN expects auxiliary folders for equities even when downloading only specific symbols.
        Directory.CreateDirectory(Path.Combine(dataFolder, "equity", "usa", "map_files"));
        Directory.CreateDirectory(Path.Combine(dataFolder, "equity", "usa", "factor_files"));
        
        var algorithmInContainer = algorithmFilePath.Replace(repoRoot, "/LeanCLI").Replace("\\", "/");
        var outputInContainer = outputDirectory.Replace(repoRoot, "/LeanCLI").Replace("\\", "/");

        var args = string.Join(" ",
            "run --rm",
            "-v", $"\"{repoRoot}\":/LeanCLI",
            "-v", $"\"{dataFolder}\":/Data",
            "quantconnect/lean:latest",
            "--config", "/LeanCLI/lean.json",
            "--algorithm-language", "Python",
            "--algorithm-location", algorithmInContainer,
            "--data-folder", "/Data",
            "--results-destination-folder", outputInContainer
        );

        var execution = await RunProcessAsync("docker", args, repoRoot, cancellationToken);

        _logger.LogInformation("Docker command executed with exit code {ExitCode}", execution.ExitCode);
        _logger.LogInformation("Docker stdout: {StdOut}", execution.StdOut);
        _logger.LogInformation("Docker stderr: {StdErr}", execution.StdErr);

        // Check the log file for runtime errors
        var logFilePath = Path.Combine(outputDirectory, "log.txt");
        var hasRuntimeErrors = false;
        var runtimeErrorMessage = "";
        if (File.Exists(logFilePath))
        {
            var logContent = await File.ReadAllTextAsync(logFilePath);
            var errorLines = logContent.Split('\n')
                .Where(line => line.Contains("ERROR::"))
                .ToList();
            if (errorLines.Any())
            {
                hasRuntimeErrors = true;
                runtimeErrorMessage = string.Join("\n", errorLines.Take(5)); // First 5 errors
            }
        }

        var result = new LeanExecutionResult
        {
            Success = execution.ExitCode == 0 && !hasRuntimeErrors,
            ExitCode = execution.ExitCode,
            Message = execution.ExitCode == 0
                ? (hasRuntimeErrors ? $"Lean backtest completed but had runtime errors: {runtimeErrorMessage}" : "Lean backtest completed successfully.")
                : BuildDetailedErrorMessage(execution, outputDirectory),
            StdOut = execution.StdOut,
            StdErr = execution.StdErr,
            OutputDirectory = outputDirectory,
            Statistics = TryExtractStatistics(outputDirectory)
        };

        return result;
    }

    private static string BuildDetailedErrorMessage(ProcessResult execution, string outputDirectory)
    {
        var messages = new List<string> { "Lean backtest failed." };

        // Check for common docker issues
        if (execution.StdErr.Contains("No such image") || execution.StdErr.Contains("pull access denied"))
        {
            messages.Add("Docker image 'quantconnect/lean:latest' not found or not accessible. Try: docker pull quantconnect/lean:latest");
        }
        else if (execution.StdErr.Contains("bind mount") || execution.StdErr.Contains("volume"))
        {
            messages.Add("Docker volume mount failed. Check that data folder exists and paths are correct.");
        }
        else if (execution.StdErr.Contains("permission denied") || execution.StdErr.Contains("Permission denied"))
        {
            messages.Add("Permission denied. Check file permissions for data folder and output directory.");
        }
        else if (execution.StdErr.Contains("port already in use") || execution.StdErr.Contains("address already in use"))
        {
            messages.Add("Port conflict. Another process may be using required ports.");
        }

        // Include stdout/stderr if available
        if (!string.IsNullOrWhiteSpace(execution.StdOut))
        {
            messages.Add($"StdOut: {execution.StdOut.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(execution.StdErr))
        {
            messages.Add($"StdErr: {execution.StdErr.Trim()}");
        }

        // Check if log file exists and has content
        var logFilePath = Path.Combine(outputDirectory, "log.txt");
        if (File.Exists(logFilePath))
        {
            try
            {
                var logContent = File.ReadAllText(logFilePath);
                if (!string.IsNullOrWhiteSpace(logContent))
                {
                    var lastLines = logContent.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).TakeLast(10);
                    messages.Add($"Last 10 log lines: {string.Join(" | ", lastLines)}");
                }
            }
            catch
            {
                messages.Add("Could not read log file.");
            }
        }
        else
        {
            messages.Add("No log file generated - backtest may have failed before starting.");
        }

        return string.Join(" ", messages);
    }

    private static DateTime? ExtractDate(string content, string label)
    {
        var m = Regex.Match(content, $@"{Regex.Escape(label)}:\s*([^\n\r]+)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var raw = m.Groups[1].Value.Trim();
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
        {
            return date.Date;
        }

        return null;
    }

    private static string BuildPipelineCommand(StrategyPlan strategy, string source)
    {
        var symbolArg = source == "binance"
            ? $"--crypto-symbols {strategy.Symbol}"
            : $"--equity-symbols {strategy.Symbol}";

        var start = (strategy.StartDate ?? DateTime.UtcNow.AddYears(-2)).ToString("yyyy-MM-dd");
        var end = (strategy.EndDate ?? DateTime.UtcNow).ToString("yyyy-MM-dd");

        return $"python main.py --source {source} {symbolArg} --start-date {start} --end-date {end} --resolution {strategy.Resolution}";
    }

    private static Dictionary<string, object>? TryExtractStatistics(string outputDirectory)
    {
        try
        {
            var jsonFiles = Directory.GetFiles(outputDirectory, "*.json", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            foreach (var file in jsonFiles)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));

                if (doc.RootElement.TryGetProperty("Statistics", out var stats) && stats.ValueKind == JsonValueKind.Object)
                {
                    return stats.EnumerateObject().ToDictionary(
                        p => p.Name,
                        p => p.Value.ValueKind == JsonValueKind.String ? (object)(p.Value.GetString() ?? string.Empty) : p.Value.ToString());
                }

                if (doc.RootElement.TryGetProperty("statistics", out var statsLower) && statsLower.ValueKind == JsonValueKind.Object)
                {
                    return statsLower.EnumerateObject().ToDictionary(
                        p => p.Name,
                        p => p.Value.ValueKind == JsonValueKind.String ? (object)(p.Value.GetString() ?? string.Empty) : p.Value.ToString());
                }
            }
        }
        catch
        {
            // Ignore parse errors; statistics are optional.
        }

        return null;
    }

    private static async Task<bool> CommandExistsAsync(string command, CancellationToken cancellationToken)
    {
        var which = OperatingSystem.IsWindows() ? "where" : "which";
        var result = await RunProcessAsync(which, command, Directory.GetCurrentDirectory(), cancellationToken);
        return result.ExitCode == 0;
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, string workingDir, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var started = process.Start();
        if (!started)
        {
            return new ProcessResult(1, string.Empty, "Failed to start process.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}

public class StrategyPlan
{
    public string StrategyName { get; set; } = "Generated Strategy";
    public string Symbol { get; set; } = "SPY";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Resolution { get; set; } = "daily";
    public decimal InitialCapital { get; set; } = 100000m;
    public decimal PositionSizePercent { get; set; } = 95m;
    public string RawText { get; set; } = string.Empty;
    public List<string> ParseWarnings { get; set; } = new();
}

public class DataPlan
{
    public string Symbol { get; set; } = string.Empty;
    public string AssetClass { get; set; } = "equity";
    public string PreferredSource { get; set; } = "alpaca";
    public string Resolution { get; set; } = "daily";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string SuggestedCommand { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

public class LeanExecutionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public Dictionary<string, object>? Statistics { get; set; }
}
