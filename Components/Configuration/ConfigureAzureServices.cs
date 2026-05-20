// Redo using: https://learn.microsoft.com/en-us/dotnet/api/azure.identity.environmentcredential?view=azure-dotnet

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
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // 1. Get the Key Vault URL (still needed to tell the client WHERE to go)
            var keyVaultURL = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
                ? Environment.GetEnvironmentVariable("KEY_VAULT_URL")
                : configuration["Keyvault:Url"];

            if (string.IsNullOrEmpty(keyVaultURL))
                throw new InvalidOperationException("Key Vault URL cannot be null.");

            // 2. Initialize EnvironmentCredential
            // This will automatically pull ClientId, TenantId, and Cert Path from AZURE_* variables.
            var credential = new EnvironmentCredential();

            // 3. Register with the Configuration builder
            services.Configuration.AddAzureKeyVault(new Uri(keyVaultURL), credential);

            // 4. Return the SecretClient for direct use
            return new SecretClient(new Uri(keyVaultURL), credential);
        }
    }
}