using System.Net;

namespace ForestChurches.Components.Http
{
    public interface IHttpWrapper
    {
        Task SetAuthorizationHeader(string header, string value);
        Task<(string, HttpStatusCode)> GetAsync(string url);
        Task<(string, HttpStatusCode)> PostAsync(string url, HttpContent content);
        Task<(string, HttpStatusCode)> DeleteAsync(string url);
    }
}
