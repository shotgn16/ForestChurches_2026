using Microsoft.Extensions.Configuration;
using Serilog;

namespace ForestChurches.Components.Configuration
{
    /// <summary>
    /// Helper class for retrieving ChurchSuite OAuth configuration from various sources.
    /// Supports Key Vault secrets, configuration files, and environment variables.
    /// </summary>
    public static class ChurchSuiteOAuthConfiguration
    {
        /// <summary>
        /// Gets ChurchSuite OAuth configuration from various sources with fallback support.
        /// Priority: Environment Variables > appsettings
        /// ChurchSuite configuration is optional - if not provided, the application will operate without ChurchSuite OAuth.
        /// </summary>
        public static ChurchSuiteOAuthSettings GetOAuthSettings(IConfiguration configuration)
        {
            var settings = new ChurchSuiteOAuthSettings();

            try
            {
                // Try to get from environment variables first (highest priority)
                var clientIdEnv = Environment.GetEnvironmentVariable("CHURCHSUITE_OAUTH_CLIENT_ID");
                var clientSecretEnv = Environment.GetEnvironmentVariable("CHURCHSUITE_OAUTH_CLIENT_SECRET");
                var callbackUrlEnv = Environment.GetEnvironmentVariable("CHURCHSUITE_OAUTH_CALLBACK_URL");

                if (!string.IsNullOrEmpty(clientIdEnv))
                {
                    settings.ClientId = clientIdEnv;
                    Log.Information("ChurchSuite OAuth ClientId loaded from environment variable");
                }

                if (!string.IsNullOrEmpty(clientSecretEnv))
                {
                    settings.ClientSecret = clientSecretEnv;
                    Log.Information("ChurchSuite OAuth ClientSecret loaded from environment variable");
                }

                if (!string.IsNullOrEmpty(callbackUrlEnv))
                {
                    settings.CallbackUrl = callbackUrlEnv;
                    Log.Information("ChurchSuite OAuth CallbackUrl loaded from environment variable");
                }

                // Try to get from appsettings (lower priority)
                if (string.IsNullOrEmpty(settings.ClientId))
                {
                    var configValue = configuration["ChurchSuite:OAuth:ClientId"];
                    if (!string.IsNullOrEmpty(configValue))
                    {
                        settings.ClientId = configValue;
                        Log.Information("ChurchSuite OAuth ClientId loaded from appsettings");
                    }
                }

                if (string.IsNullOrEmpty(settings.ClientSecret))
                {
                    var configValue = configuration["ChurchSuite:OAuth:ClientSecret"];
                    if (!string.IsNullOrEmpty(configValue))
                    {
                        settings.ClientSecret = configValue;
                        Log.Warning("ChurchSuite OAuth ClientSecret loaded from appsettings - NOT recommended for production!");
                    }
                }

                if (string.IsNullOrEmpty(settings.CallbackUrl))
                {
                    var configValue = configuration["ChurchSuite:OAuth:CallbackUrl"];
                    if (!string.IsNullOrEmpty(configValue))
                    {
                        settings.CallbackUrl = configValue;
                        Log.Information("ChurchSuite OAuth CallbackUrl loaded from appsettings");
                    }
                }

                // If no configuration found, return null settings (optional)
                if (string.IsNullOrEmpty(settings.ClientId) && string.IsNullOrEmpty(settings.CallbackUrl))
                {
                    Log.Information("ChurchSuite OAuth configuration not provided. ChurchSuite integration will be disabled.");
                    return null;
                }

                // Validate that required settings are present if any ChurchSuite config is provided
                ValidateSettings(settings);

                return settings;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading ChurchSuite OAuth configuration");
                throw;
            }
        }

        /// <summary>
        /// Validates that required OAuth settings are configured when ChurchSuite is enabled.
        /// Since ChurchSuite integration is optional, we only validate if ClientId is provided.
        /// Allows operating as a public client (PKCE) when ClientSecret is not provided.
        /// </summary>
        private static void ValidateSettings(ChurchSuiteOAuthSettings settings)
        {
            if (settings == null)
                return;

            var missingSettings = new List<string>();

            // ClientId is required if any ChurchSuite configuration is provided
            if (string.IsNullOrEmpty(settings.ClientId))
                missingSettings.Add("ClientId");

            // ClientSecret is optional to support PKCE/public-client flows. If missing, log a warning rather than failing.
            if (string.IsNullOrEmpty(settings.ClientSecret))
            {
                Log.Warning("ChurchSuite OAuth ClientSecret not provided. Operating in public-client (PKCE) mode.");
            }

            if (string.IsNullOrEmpty(settings.CallbackUrl))
                missingSettings.Add("CallbackUrl");

            if (missingSettings.Any())
            {
                var message = $"Missing ChurchSuite OAuth configuration: {string.Join(", ", missingSettings)}. " +
                    "Please ensure required settings are configured in environment variables or appsettings.";
                Log.Error(message);
                throw new InvalidOperationException(message);
            }

            // Validate callback URL format
            if (!Uri.TryCreate(settings.CallbackUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"ChurchSuite OAuth CallbackUrl is not a valid absolute URI: {settings.CallbackUrl}");
            }

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Host != "localhost" && uri.Host != "127.0.0.1")
            {
                Log.Warning("ChurchSuite OAuth CallbackUrl should use HTTPS in production");
            }

            Log.Information("ChurchSuite OAuth configuration validated successfully. CallbackUrl: {CallbackUrl}", 
                settings.CallbackUrl);
        }

        /// <summary>
        /// Gets the callback path from the full callback URL.
        /// </summary>
        public static string GetCallbackPath(string callbackUrl)
        {
            if (Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri))
            {
                return uri.PathAndQuery;
            }
            return "/signin-churchsuite";
        }
    }

    /// <summary>
    /// ChurchSuite OAuth configuration settings.
    /// </summary>
    public class ChurchSuiteOAuthSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string CallbackUrl { get; set; }
        public string AuthorizationEndpoint { get; set; } = "https://oauth.churchsuite.com/oauth/authorize";
        public string TokenEndpoint { get; set; } = "https://oauth.churchsuite.com/oauth/token";
        public string UserInformationEndpoint { get; set; } = "https://oauth.churchsuite.com/oauth/me";
    }
}
