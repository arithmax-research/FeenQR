using HtmlAgilityPack;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;

namespace QuantResearchAgent.Services;

public class LinkedInScrapingService
{
    private readonly HttpClient _httpClient;

    public LinkedInScrapingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    public async Task<List<LinkedInPost>> ScrapePostsAsync(string url)
    {
        var posts = new List<LinkedInPost>();

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return posts;
            }

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // LinkedIn post containers - this might need adjustment based on actual HTML structure
            var postNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'feed-shared-update-v2') or contains(@class, 'feed-shared-post')]");

            if (postNodes != null)
            {
                foreach (var postNode in postNodes.Take(10)) // Limit to 10 posts
                {
                    var post = new LinkedInPost();

                    // Extract author
                    var authorNode = postNode.SelectSingleNode(".//span[contains(@class, 'feed-shared-actor__name') or contains(@class, 'update-components-actor__name')]");
                    post.Author = authorNode?.InnerText.Trim() ?? "Unknown";

                    // Extract content
                    var contentNode = postNode.SelectSingleNode(".//div[contains(@class, 'feed-shared-text') or contains(@class, 'update-components-text')]");
                    post.Content = contentNode?.InnerText.Trim() ?? "";

                    // Extract timestamp
                    var timeNode = postNode.SelectSingleNode(".//time");
                    post.Timestamp = timeNode?.GetAttributeValue("datetime", "") ?? timeNode?.InnerText.Trim() ?? "";

                    // Extract papers/attachments
                    post.Papers = ExtractPapers(postNode);

                    posts.Add(post);
                }
            }
        }
        catch (Exception)
        {
            // Return empty list on error
        }

        return posts;
    }

    private List<Paper> ExtractPapers(HtmlNode postNode)
    {
        var papers = new List<Paper>();

        // Look for document attachments
        var attachmentNodes = postNode.SelectNodes(".//div[contains(@class, 'feed-shared-document') or contains(@class, 'update-components-document')]");

        if (attachmentNodes != null)
        {
            foreach (var attachment in attachmentNodes)
            {
                var paper = new Paper();

                // Extract title
                var titleNode = attachment.SelectSingleNode(".//span[contains(@class, 'document-title') or contains(@class, 'update-components-document__title')]");
                paper.Title = titleNode?.InnerText.Trim() ?? "Untitled Document";

                // Extract link
                var linkNode = attachment.SelectSingleNode(".//a");
                paper.Link = linkNode?.GetAttributeValue("href", "");

                // For download link, might need to construct or find download URL
                if (!string.IsNullOrEmpty(paper.Link))
                {
                    paper.DownloadLink = paper.Link; // Assuming same link for download
                }

                papers.Add(paper);
            }
        }

        // Also look for links in content that might be papers
        var contentNode = postNode.SelectSingleNode(".//div[contains(@class, 'feed-shared-text') or contains(@class, 'update-components-text')]");
        if (contentNode != null)
        {
            var links = contentNode.SelectNodes(".//a");
            if (links != null)
            {
                foreach (var link in links)
                {
                    var href = link.GetAttributeValue("href", "");
                    var text = link.InnerText.Trim();

                    // Check if it looks like a paper link (contains keywords or PDF)
                    if (Regex.IsMatch(href, @"\.pdf$", RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(text, @"paper|research|study|article", RegexOptions.IgnoreCase))
                    {
                        papers.Add(new Paper
                        {
                            Title = text,
                            Link = href,
                            DownloadLink = href
                        });
                    }
                }
            }
        }

        return papers;
    }
}

public class LinkedInPost
{
    public string? Author { get; set; }
    public string? Content { get; set; }
    public string? Timestamp { get; set; }
    public List<Paper>? Papers { get; set; }
}

public class Paper
{
    public string? Title { get; set; }
    public string? Link { get; set; }
    public string? DownloadLink { get; set; }
}