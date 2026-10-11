using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using System.Configuration;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace ForestChurches.Components.Configuration
{
    public class ConfigureAzureServices
    {
        private readonly ILogger<ConfigureAzureServices> _logger;
        public ConfigureAzureServices(ILogger<ConfigureAzureServices> logger)
        {
            _logger = logger;
        }

        public static SecretClient ConfigureAzureKeyVault(WebApplicationBuilder services, string environment, IConfigurationRoot configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Get the Key Vault URL from environment variables only (both development and production)
            var keyVaultURL = Environment.GetEnvironmentVariable("KEY_VAULT_URL");

            if (string.IsNullOrEmpty(keyVaultURL))
                throw new InvalidOperationException("Key Vault URL cannot be null. Please set the KEY_VAULT_URL environment variable.");

            // Initialize EnvironmentCredential
            // This will automatically pull ClientId, TenantId, and Cert Path from AZURE_* variables.
            var credential = new EnvironmentCredential();

            // Register with the Configuration builder
            services.Configuration.AddAzureKeyVault(new Uri(keyVaultURL), credential);

            // Return the SecretClient for direct use
            return new SecretClient(new Uri(keyVaultURL), credential);
        }
    }
}