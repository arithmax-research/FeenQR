using System.Net.Http;

namespace QuantResearchAgent.Core
{
    /// <summary>
    /// Simple implementation of IHttpClientFactory for console app DI
    /// </summary>
    public class SimpleHttpClientFactory : System.Net.Http.IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public SimpleHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name)
        {
            return _httpClient;
        }
    }
}
