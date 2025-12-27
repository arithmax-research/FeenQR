using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

public class IEXCloudPlugin
{
    private readonly IEXCloudService _service;

    public IEXCloudPlugin(IEXCloudService service)
    {
        _service = service;
    }

    [KernelFunction, Description("Get real-time quote from IEX Cloud")]
    public async Task<string> GetQuote([Description("Stock symbol")] string symbol)
    {
        try
        {
            var quote = await _service.GetQuoteAsync(symbol);

            if (quote != null)
            {
                return $"IEX Cloud Quote for {symbol}:\n" +
                       $"Price: ${quote.LatestPrice:F2}\n" +
                       $"Change: ${quote.Change:F2} ({quote.ChangePercent:F2}%)\n" +
                       $"Volume: {quote.Volume:N0}\n" +
                       $"Market Cap: ${quote.MarketCap:N0}\n" +
                       $"52 Week High: ${quote.Week52High:F2}\n" +
                       $"52 Week Low: ${quote.Week52Low:F2}\n" +
                       $"PE Ratio: {quote.PeRatio:F2}\n" +
                       $"Last Updated: {quote.LastTradeTime}";
            }

            return $"No quote data available for {symbol}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving quote: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get company information from IEX Cloud")]
    public async Task<string> GetCompany([Description("Stock symbol")] string symbol)
    {
        try
        {
            var company = await _service.GetCompanyAsync(symbol);

            if (company != null)
            {
                return $"Company Information for {symbol}:\n" +
                       $"Name: {company.CompanyName}\n" +
                       $"Industry: {company.Industry}\n" +
                       $"Sector: {company.Sector}\n" +
                       $"CEO: {company.CEO}\n" +
                       $"Employees: {company.Employees:N0}\n" +
                       $"Website: {company.Website}\n" +
                       $"Description: {company.Description}\n" +
                       $"Exchange: {company.Exchange}\n" +
                       $"Country: {company.Country}";
            }

            return $"No company data available for {symbol}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving company data: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get latest news from IEX Cloud")]
    public async Task<string> GetNews(
        [Description("Stock symbol")] string symbol,
        [Description("Number of articles to retrieve")] int count = 5)
    {
        try
        {
            var news = await _service.GetNewsAsync(symbol, count);

            if (news?.Any() == true)
            {
                var result = $"Latest News for {symbol}:\n\n";

                foreach (var article in news.Take(count))
                {
                    result += $"Title: {article.Headline}\n";
                    result += $"Source: {article.Source}\n";
                    result += $"Date: {article.Datetime:yyyy-MM-dd HH:mm}\n";
                    result += $"Summary: {article.Summary}\n";
                    if (!string.IsNullOrEmpty(article.Url))
                        result += $"URL: {article.Url}\n";
                    result += "---\n";
                }

                return result;
            }

            return $"No news available for {symbol}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving news: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get earnings data from IEX Cloud")]
    public async Task<string> GetEarnings([Description("Stock symbol")] string symbol)
    {
        try
        {
            var earnings = await _service.GetEarningsAsync(symbol);

            if (earnings?.Any() == true)
            {
                var result = $"Earnings Data for {symbol}:\\n\\n";

                foreach (var earning in earnings.OrderByDescending(e => e.FiscalEndDate))
                {
                    result += $"Fiscal Period: {earning.FiscalPeriod}\n";
                    result += $"End Date: {earning.FiscalEndDate:yyyy-MM-dd}\n";
                    result += $"EPS: ${earning.ActualEPS:F2}\n";
                    result += $"Estimated EPS: ${earning.EstimatedEPS:F2}\n";
                    result += $"Revenue: ${earning.Revenue:N0}\n";
                    result += $"Estimated Revenue: ${earning.EstimatedRevenue:N0}\n";
                    result += $"EPS Surprise: {earning.EPSSurpriseDollar:F2}\n";
                    result += "---\n";
                }

                return result;
            }

            return $"No earnings data available for {symbol}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving earnings data: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get dividend data from IEX Cloud")]
    public async Task<string> GetDividends([Description("Stock symbol")] string symbol)
    {
        try
        {
            var dividends = await _service.GetDividendsAsync(symbol);

            if (dividends?.Any() == true)
            {
                var result = $"Dividend History for {symbol}:\n\n";

                foreach (var dividend in dividends.OrderByDescending(d => d.ExDate))
                {
                    result += $"Ex-Date: {dividend.ExDate:yyyy-MM-dd}\n";
                    result += $"Payment Date: {dividend.PaymentDate:yyyy-MM-dd}\n";
                    result += $"Amount: ${dividend.Amount:F2}\n";
                    result += $"Type: {dividend.Flag}\n";
                    result += "---\n";
                }

                return result;
            }

            return $"No dividend data available for {symbol}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving dividend data: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get sector performance from IEX Cloud")]
    public async Task<string> GetSectorPerformance()
    {
        try
        {
            var sectors = await _service.GetSectorPerformanceAsync();

            if (sectors?.Any() == true)
            {
                var result = "Sector Performance:\n\n";

                foreach (var sector in sectors.OrderByDescending(s => s.Performance))
                {
                    result += $"{sector.Name}: {sector.Performance:F2}%\n";
                }

                return result;
            }

            return "No sector performance data available";
        }
        catch (Exception ex)
        {
            return $"Error retrieving sector performance: {ex.Message}";
        }
    }
}