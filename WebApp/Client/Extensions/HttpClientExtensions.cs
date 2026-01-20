using System.Net.Http.Json;

namespace Client.Extensions;

public static class HttpClientExtensions
{
    public static async Task<T?> PostAsJsonAsync<T>(this HttpClient client, string url, object request)
    {
        var response = await client.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
