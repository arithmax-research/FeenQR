using System.Net;
using System.Text;

namespace RedditScraping.Tests;

/// <summary>One scripted HTTP response: status, body and (optionally) headers.</summary>
internal sealed record CannedResponse(
    HttpStatusCode StatusCode,
    string Body,
    string ContentType = "application/json",
    Action<HttpResponseMessage> Configure = null)
{
    public static CannedResponse Json(string body) => new(HttpStatusCode.OK, body);

    public static CannedResponse Xml(string body) => new(HttpStatusCode.OK, body, "application/xml");
}

/// <summary>
/// Serves a scripted sequence of responses and records every request URI, so a test can assert
/// both what came back and how many calls (and which endpoints) were made.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<CannedResponse> _scripted;

    public FakeHttpMessageHandler(params CannedResponse[] responses)
        => _scripted = new Queue<CannedResponse>(responses);

    /// <summary>Every request the service sent, in order, as absolute URI strings.</summary>
    public List<string> RequestUris { get; } = new();

    public int RequestCount => RequestUris.Count;

    public int CountRequestsContaining(string fragment)
        => RequestUris.Count(uri => uri.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);

        if (_scripted.Count == 0)
        {
            throw new InvalidOperationException(
                $"The service sent more requests than the test scripted (unexpected call to {request.RequestUri}).");
        }

        var canned = _scripted.Dequeue();
        var response = new HttpResponseMessage(canned.StatusCode)
        {
            Content = new StringContent(canned.Body, Encoding.UTF8, canned.ContentType),
            RequestMessage = request
        };
        canned.Configure?.Invoke(response);
        return Task.FromResult(response);
    }
}
