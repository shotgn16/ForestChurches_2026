using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace ForestChurches.Components.Configuration
{
    public class Configuration
    {
        private readonly IHostEnvironment _environment;
        private readonly ILogger<Configuration> _logger;
        private readonly IConfiguration _configReader;
        private SecretClient _client;

        public Configuration(ILogger<Configuration> logger, IHostEnvironment environment, IConfiguration configReader)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configReader = configReader ?? throw new ArgumentNullException(nameof(configReader));
        }

        public SecretClient Client
        {
            get
            {
                if (_client == null)
                {
                    AuthenticateKeyVault();
                }

                return _client;
            }
        }
        /// <summary>
        /// Get a secret value from Key Vault when running in Production, or from the
        /// application's configuration when running in Development.
        /// </summary>
        public string? GetSecretValue(string secretName)
        {
            try
            {
                if (_environment.IsDevelopment())
                {
                    // Try common appsettings location used by Program.cs
                    var fromConfig = _configReader[$"ConnectionStrings:Development"];
                    if (!string.IsNullOrWhiteSpace(fromConfig))
                        return fromConfig;

                    // Fallback to top-level key if present
                    var fallback = _configReader[secretName];
                    if (!string.IsNullOrWhiteSpace(fallback))
                        return fallback;
                }

                // In non-development environments, read from Key Vault
                return Client.GetSecret(secretName).Value?.Value;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to read secret/value '{SecretName}': {Message}", secretName, ex.Message);
                return null;
            }
        }
        private void AuthenticateKeyVault()
        {
            var keyVaultURL = Environment.GetEnvironmentVariable("KEY_VAULT_URL");
            if (string.IsNullOrEmpty(keyVaultURL))
                throw new InvalidOperationException("Key Vault URL cannot be null.");

            // Use EnvironmentCredential which reads AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET
            var credential = new EnvironmentCredential();

            _client = new SecretClient(new Uri(keyVaultURL), credential);
        }
    }
}