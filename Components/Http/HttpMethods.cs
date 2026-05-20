using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace ForestChurches.Components.Http
{
    public class HttpMethods : IHttpMethods
    {
        private IHttpWrapper _httpWrapper;
        private ILogger<HttpMethods> _logger;
        public HttpMethods(IHttpWrapper httpWrapper, ILogger<HttpMethods> logger)
        {
            _httpWrapper = httpWrapper;
            _logger = logger;
        }
    }
}
