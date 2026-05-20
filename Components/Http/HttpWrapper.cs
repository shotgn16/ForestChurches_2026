using System.Net;
using System.Runtime.CompilerServices;

namespace ForestChurches.Components.Http
{
    public class HttpWrapper : IHttpWrapper
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClient _httpClient;

        private readonly ILogger<HttpWrapper> _logger;

        public HttpWrapper(IHttpClientFactory httpClientFactory, ILogger<HttpWrapper> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // Per-Instance HttpClient...
            _httpClient = _httpClientFactory.CreateClient();
        }

        public async Task SetAuthorizationHeader(string header, string value)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(header, value);
        }

        public async Task<(string, HttpStatusCode)> GetAsync(string url)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            (string, HttpStatusCode) Return;

            if (!response.IsSuccessStatusCode)
            {
                Return.Item1 = $"Error {response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
                Return.Item2 = response.StatusCode;
            }

            return (await response.Content.ReadAsStringAsync(), response.StatusCode);
        }

        public async Task<(string, HttpStatusCode)> PostAsync(string url, HttpContent content)
        {
            HttpResponseMessage response = await _httpClient.PostAsync(url, content);
            (string, HttpStatusCode) Return;

            if (!response.IsSuccessStatusCode)
            {
                Return.Item1 = $"Error {response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
                Return.Item2 = response.StatusCode;
            }

            return (await response.Content.ReadAsStringAsync(), response.StatusCode);
        }

        public async Task<(string, HttpStatusCode)> DeleteAsync(string url)
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync(url);
            (string, HttpStatusCode) Return;

            if (!response.IsSuccessStatusCode)
            {
                Return.Item1 = $"Error {response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
                Return.Item2 = response.StatusCode;
            }

            return (await response.Content.ReadAsStringAsync(), response.StatusCode);
        }
    }
}
