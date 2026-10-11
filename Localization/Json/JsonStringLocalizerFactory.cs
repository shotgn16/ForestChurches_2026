using Microsoft.Extensions.Localization;
using System.Globalization;

namespace ForestChurches.Localization.Json
{
    internal class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly string _contentRoot;

        public JsonStringLocalizerFactory(Microsoft.Extensions.Hosting.IHostEnvironment env)
        {
            _contentRoot = env.ContentRootPath;
        }

        private string GetResourceName(string baseName, string? location)
        {
            if (!string.IsNullOrEmpty(baseName))
            {
                // take last segment after '.'
                var parts = baseName.Split('.');
                return parts.Last();
            }

            if (!string.IsNullOrEmpty(location))
            {
                var parts = location.Split('.');
                return parts.Last();
            }

            return "SharedResources";
        }

        public IStringLocalizer Create(Type resourceSource)
        {
            var resourceName = GetResourceName(resourceSource.FullName, resourceSource.Namespace);
            return new JsonStringLocalizer(resourceName, _contentRoot);
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            var resourceName = GetResourceName(baseName, location);
            return new JsonStringLocalizer(resourceName, _contentRoot);
        }
    }
}
