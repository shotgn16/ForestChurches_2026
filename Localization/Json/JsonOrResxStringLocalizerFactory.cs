using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ForestChurches.Localization.Json
{
    /// <summary>
    /// Factory that prefers JSON resources (if they exist) but falls back to ResourceManager (.resx) by default.
    /// </summary>
    internal class JsonOrResxStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly string _contentRoot;
        private readonly ResourceManagerStringLocalizerFactory _resxFactory;

        public JsonOrResxStringLocalizerFactory(
            Microsoft.Extensions.Hosting.IHostEnvironment env,
            IOptions<LocalizationOptions> localizationOptions,
            Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            _contentRoot = env.ContentRootPath;
            // This factory will be used as the default for resx files
            _resxFactory = new ResourceManagerStringLocalizerFactory(localizationOptions, loggerFactory);
        }

        /// <summary>
        /// Extract resource name from full type name (e.g., "ForestChurches.Resources.SharedResources" -> "SharedResources")
        /// </summary>
        private static string GetResourceName(string? baseName, string? location)
        {
            if (!string.IsNullOrEmpty(baseName))
            {
                var parts = baseName.Split('.');
                return parts[^1]; // Last segment
            }

            if (!string.IsNullOrEmpty(location))
            {
                var parts = location.Split('.');
                return parts[^1];
            }

            return "SharedResources";
        }

        /// <summary>
        /// Check if JSON resource files exist for the given resource name.
        /// </summary>
        private bool JsonExists(string resourceName)
        {
            var resourcesFolder = Path.Combine(_contentRoot, "Resources");
            if (!Directory.Exists(resourcesFolder)) return false;

            // Check for any JSON file matching the resource name pattern
            try
            {
                var jsonFiles = Directory.EnumerateFiles(resourcesFolder, $"{resourceName}*.json");
                return jsonFiles.Any();
            }
            catch
            {
                return false;
            }
        }

        public IStringLocalizer Create(Type resourceSource)
        {
            if (resourceSource == null)
                throw new ArgumentNullException(nameof(resourceSource));

            var resourceName = GetResourceName(resourceSource.FullName, resourceSource.Namespace);

            // Prefer JSON if it exists, otherwise use resx via ResourceManager
            if (JsonExists(resourceName))
            {
                return new JsonStringLocalizer(resourceName, _contentRoot);
            }

            // Fall back to ResourceManager factory (for .resx files)
            return _resxFactory.Create(resourceSource);
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            if (string.IsNullOrEmpty(baseName))
                baseName = location;

            var resourceName = GetResourceName(baseName, location);

            // Prefer JSON if it exists, otherwise use resx via ResourceManager
            if (JsonExists(resourceName))
            {
                return new JsonStringLocalizer(resourceName, _contentRoot);
            }

            // Fall back to ResourceManager factory (for .resx files)
            return _resxFactory.Create(baseName, location);
        }
    }
}
