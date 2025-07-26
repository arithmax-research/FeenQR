using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace QuantResearchAgent.Services;

public class LeanDataService
{
    private readonly ILogger<LeanDataService> _logger;
    private readonly string _dataPath;

    public LeanDataService(ILogger<LeanDataService> logger)
    {
        _logger = logger;
        _dataPath = Path.Combine(Directory.GetCurrentDirectory(), "data");
        
        if (!Directory.Exists(_dataPath))
        {
            _logger.LogWarning("Data directory not found at {DataPath}. Consider running the Python data pipeline first.", _dataPath);
        }
    }

    public async Task<List<LeanBar>> GetEquityBarsAsync(string symbol, string resolution = "daily", int limit = 10000)
    {
        try
        {
            var equityPath = Path.Combine(_dataPath, "equity", "usa", resolution);
            var symbolPath = Path.Combine(equityPath, symbol.ToLower());

            if (!Directory.Exists(symbolPath))
            {
                _logger.LogWarning("No data directory found for symbol {Symbol} at {Path}", symbol, symbolPath);
                return new List<LeanBar>();
            }

            var bars = new List<LeanBar>();
            var files = Directory.GetFiles(symbolPath, "*.zip")
                .OrderBy(f => f)
                .ToList();

            _logger.LogInformation("Found {FileCount} data files for {Symbol}", files.Count, symbol);

            foreach (var file in files)
            {
                var fileBars = await ReadLeanZipFileAsync(file, symbol);
                bars.AddRange(fileBars);
            }

            // Sort by date and take the most recent data
            var result = bars.OrderByDescending(b => b.Time)
                .Take(limit)
                .OrderBy(b => b.Time)
                .ToList();

            _logger.LogInformation("Loaded {BarCount} bars for {Symbol} from Lean data", result.Count, symbol);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Lean data for {Symbol}", symbol);
            return new List<LeanBar>();
        }
    }

    public async Task<List<LeanBar>> GetCryptoBarsAsync(string symbol, string resolution = "daily", int days = 30)
    {
        try
        {
            var cryptoPath = Path.Combine(_dataPath, "crypto", "binance", resolution);
            var symbolPath = Path.Combine(cryptoPath, symbol.ToLower());

            if (!Directory.Exists(symbolPath))
            {
                _logger.LogWarning("No crypto data directory found for symbol {Symbol} at {Path}", symbol, symbolPath);
                return new List<LeanBar>();
            }

            var bars = new List<LeanBar>();
            var files = Directory.GetFiles(symbolPath, "*.zip")
                .OrderByDescending(f => f)
                .Take(days)
                .ToList();

            _logger.LogInformation("Found {FileCount} crypto data files for {Symbol}", files.Count, symbol);

            foreach (var file in files)
            {
                var fileBars = await ReadLeanZipFileAsync(file, symbol);
                bars.AddRange(fileBars);
            }

            var result = bars.OrderByDescending(b => b.Time)
                .Take(days)
                .OrderBy(b => b.Time)
                .ToList();

            _logger.LogInformation("Loaded {BarCount} crypto bars for {Symbol} from Lean data", result.Count, symbol);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Lean crypto data for {Symbol}", symbol);
            return new List<LeanBar>();
        }
    }

    private async Task<List<LeanBar>> ReadLeanZipFileAsync(string zipFilePath, string symbol)
    {
        var bars = new List<LeanBar>();

        try
        {
            using var archive = ZipFile.OpenRead(zipFilePath);
            
            foreach (var entry in archive.Entries)
            {
                if (!entry.Name.EndsWith(".csv"))
                    continue;

                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var bar = ParseLeanCsvLine(line, symbol);
                    if (bar != null)
                    {
                        bars.Add(bar);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Lean zip file {FilePath}", zipFilePath);
        }

        return bars;
    }

    private LeanBar? ParseLeanCsvLine(string csvLine, string symbol)
    {
        try
        {
            var parts = csvLine.Split(',');
            if (parts.Length < 6)
                return null;

            // Lean CSV format: Time,Open,High,Low,Close,Volume
            var timeMs = long.Parse(parts[0]);
            var time = DateTimeOffset.FromUnixTimeMilliseconds(timeMs).DateTime;

            return new LeanBar
            {
                Symbol = symbol,
                Time = time,
                Open = decimal.Parse(parts[1], CultureInfo.InvariantCulture),
                High = decimal.Parse(parts[2], CultureInfo.InvariantCulture),
                Low = decimal.Parse(parts[3], CultureInfo.InvariantCulture),
                Close = decimal.Parse(parts[4], CultureInfo.InvariantCulture),
                Volume = long.Parse(parts[5])
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error parsing Lean CSV line: {Line} - {Error}", csvLine, ex.Message);
            return null;
        }
    }

    public async Task<bool> HasDataForSymbolAsync(string symbol, bool isCrypto = false)
    {
        return await Task.Run(() => {
            var basePath = isCrypto 
                ? Path.Combine(_dataPath, "crypto", "binance", "daily")
                : Path.Combine(_dataPath, "equity", "usa", "daily");
            var symbolPath = Path.Combine(basePath, symbol.ToLower());
            return Directory.Exists(symbolPath) && Directory.GetFiles(symbolPath, "*.zip").Length > 0;
        });
    }

    public async Task<List<string>> GetAvailableSymbolsAsync(bool isCrypto = false)
    {
        try
        {
            var basePath = isCrypto 
                ? Path.Combine(_dataPath, "crypto", "binance", "daily")
                : Path.Combine(_dataPath, "equity", "usa", "daily");

            if (!Directory.Exists(basePath))
                return new List<string>();

            var symbols = await Task.Run(() =>
                Directory.GetDirectories(basePath)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Select(name => name!.ToUpper())
                    .ToList()
            );
            _logger.LogInformation("Found {SymbolCount} available {AssetType} symbols", symbols.Count, isCrypto ? "crypto" : "equity");
            return symbols;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available symbols");
            return new List<string>();
        }
    }

    public async Task<bool> TriggerDataDownloadAsync(string symbol, bool isCrypto = false, int days = 30)
    {
        try
        {
            _logger.LogInformation("Triggering Python data download for {Symbol} ({AssetType})", symbol, isCrypto ? "crypto" : "equity");
            
            var pipelinePath = Path.Combine(Directory.GetCurrentDirectory(), "data_pipeline");
            var pythonScript = Path.Combine(pipelinePath, "main.py");
            
            if (!File.Exists(pythonScript))
            {
                _logger.LogError("Python data pipeline not found at {Path}", pythonScript);
                return false;
            }

            // Prepare the command arguments
            var endDate = DateTime.Now.ToString("yyyy-MM-dd");
            var startDate = DateTime.Now.AddDays(-days).ToString("yyyy-MM-dd");
            
            var args = new List<string>
            {
                pythonScript,
                "--start-date", startDate,
                "--end-date", endDate
            };

            if (isCrypto)
            {
                args.AddRange(new[] { "--source", "binance", "--crypto-symbols", symbol });
            }
            else
            {
                args.AddRange(new[] { "--source", "alpaca", "--equity-symbols", symbol });
            }

            // Execute Python script
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python3",
                Arguments = string.Join(" ", args),
                WorkingDirectory = pipelinePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            _logger.LogInformation("Executing: python3 {Args}", string.Join(" ", args));

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start Python data download process");
                return false;
            }

            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Successfully downloaded data for {Symbol}", symbol);
                return true;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("Python data download failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering data download for {Symbol}", symbol);
            return false;
        }
    }
}

public class LeanBar
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}
