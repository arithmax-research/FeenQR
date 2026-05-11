using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuantResearchAgent.Services;

namespace QuantResearchAgent
{
    public class ApiTester
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<ApiTester> _logger;

        public ApiTester(HttpClient httpClient, IConfiguration config, ILogger<ApiTester> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task TestNewsAPIs()
        {
            Console.WriteLine("🧪 Testing News APIs...\n");

            // Test Yahoo Finance
            await TestYahooFinance("AAPL");

            // Test Finviz
            await TestFinviz("AAPL");

            // Test NewsAPI
            await TestNewsApi("AAPL");

            // Test Alpha Vantage (if it has news)
            await TestAlphaVantageNews("AAPL");

            // Test FMP (if it has news)
            await TestFMPNews("AAPL");
        }

        private async Task TestYahooFinance(string symbol)
        {
            try
            {
                Console.WriteLine("📈 Testing Yahoo Finance News API...");
                var yfinanceService = new YFinanceNewsService(_httpClient, NullLogger<YFinanceNewsService>.Instance);
                var news = await yfinanceService.GetNewsAsync(symbol, 5);

                Console.WriteLine($"✅ Yahoo Finance: Retrieved {news.Count} news items");
                if (news.Count > 0)
                {
                    Console.WriteLine($"   Sample: {news[0].Title}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Yahoo Finance failed: {ex.Message}");
            }
            Console.WriteLine();
        }

        private async Task TestFinviz(string symbol)
        {
            try
            {
                Console.WriteLine("📊 Testing Finviz News API...");
                var finvizService = new FinvizNewsService(_httpClient, NullLogger<FinvizNewsService>.Instance);
                var news = await finvizService.GetNewsAsync(symbol, 5);

                Console.WriteLine($"✅ Finviz: Retrieved {news.Count} news items");
                if (news.Count > 0)
                {
                    Console.WriteLine($"   Sample: {news[0].Title}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Finviz failed: {ex.Message}");
            }
            Console.WriteLine();
        }

        private async Task TestNewsApi(string symbol)
        {
            try
            {
                Console.WriteLine("📰 Testing Python dual news pipeline...");
                var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Scripts", "news_pipeline.py"));
                if (!File.Exists(scriptPath))
                {
                    scriptPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "news_pipeline.py"));
                }

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "python3",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.StartInfo.ArgumentList.Add(scriptPath);
                process.StartInfo.ArgumentList.Add(symbol);
                process.StartInfo.ArgumentList.Add("--source");
                process.StartInfo.ArgumentList.Add("All");
                process.StartInfo.ArgumentList.Add("--max-articles");
                process.StartInfo.ArgumentList.Add("5");
                process.StartInfo.ArgumentList.Add("--pretty");

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var errors = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"❌ Pipeline failed: {errors}");
                    return;
                }

                var doc = JsonSerializer.Deserialize<JsonElement>(output);
                var count = doc.GetProperty("count").GetInt32();
                Console.WriteLine($"✅ Pipeline: Retrieved {count} articles");
                if (count > 0)
                {
                    var first = doc.GetProperty("articles")[0];
                    Console.WriteLine($"   Sample: {first.GetProperty("title").GetString()}");
                    Console.WriteLine($"   Provider: {first.GetProperty("provider").GetString()}");
                    Console.WriteLine($"   Scraped: {first.GetProperty("is_scraped").GetBoolean()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Pipeline failed: {ex.Message}");
            }
            Console.WriteLine();
        }

        private async Task TestAlphaVantageNews(string symbol)
        {
            try
            {
                Console.WriteLine("🔍 Testing Alpha Vantage News API...");
                var apiKey = _config["AlphaVantage:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("❌ Alpha Vantage: No API key configured");
                    return;
                }

                // Alpha Vantage News API
                var url = $"https://www.alphavantage.co/query?function=NEWS_SENTIMENT&tickers={symbol}&apikey={apiKey}&limit=5";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(json);

                    if (data.TryGetProperty("feed", out var feed))
                    {
                        var count = feed.GetArrayLength();
                        Console.WriteLine($"✅ Alpha Vantage: Retrieved {count} news items");
                        if (count > 0)
                        {
                            var firstItem = feed[0];
                            var title = firstItem.GetProperty("title").GetString();
                            Console.WriteLine($"   Sample: {title}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Alpha Vantage: No 'feed' property in response");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Alpha Vantage: HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Alpha Vantage failed: {ex.Message}");
            }
            Console.WriteLine();
        }

        private async Task TestFMPNews(string symbol)
        {
            try
            {
                Console.WriteLine("💰 Testing Financial Modeling Prep News API...");
                var apiKey = _config["FMP:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("❌ FMP: No API key configured");
                    return;
                }

                // FMP Stock News API
                var url = $"https://financialmodelingprep.com/api/v3/stock_news?tickers={symbol}&limit=5&apikey={apiKey}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<List<JsonElement>>(json);

                    Console.WriteLine($"✅ FMP: Retrieved {data.Count} news items");
                    if (data.Count > 0)
                    {
                        var title = data[0].GetProperty("title").GetString();
                        Console.WriteLine($"   Sample: {title}");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ FMP: HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FMP failed: {ex.Message}");
            }
            Console.WriteLine();
        }
    }
}