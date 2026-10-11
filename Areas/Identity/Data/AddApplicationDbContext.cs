using ForestChurches.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ForestChurches.Areas.Identity.Data
{
    public class AddApplicationDbContext : IDesignTimeDbContextFactory<ForestChurchesContext>
    {
        private IConfigurationRoot _configuration;

        public ForestChurchesContext CreateDbContext(string[] args = null)
        {
            BuildConfig(); // This must build before anything else in this class...

            var connectionString = GetConnectionString();
            var optionsBuilder = new DbContextOptionsBuilder<ForestChurchesContext>();

            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)); // Use MySQL or SQL Server as needed

            var context = new ForestChurchesContext(optionsBuilder.Options);

            try
            {
                var databaseCreator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                if (databaseCreator != null && !databaseCreator.CanConnect())
                {
                    databaseCreator.Create();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while creating the database {message}", ex.Message);
            }

            return context;
        }

        public string GetConnectionString()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // 1. Get the Key Vault URL
            var keyVaultURL = environment == "Production"
                ? Environment.GetEnvironmentVariable("KEY_VAULT_URL")
                : _configuration["Keyvault:Url"];

            if (string.IsNullOrEmpty(keyVaultURL))
                throw new InvalidOperationException("Key Vault URL cannot be null.");

            // 2. Initialize EnvironmentCredential
            // This looks for the same AZURE_* environment variables as your ConfigureAzureServices class
            var credential = new EnvironmentCredential();

            // 3. Authenticate with Key Vault
            var client = new SecretClient(new Uri(keyVaultURL), credential);

            // 4. Retrieve Secret
            // If Production, fetch from Key Vault; otherwise, use local configuration
            if (environment == "Production")
            {
                return client.GetSecret("db-connection-live").Value.Value;
            }

            return _configuration["ConnectionStrings:Development"];
        }

        private void BuildConfig()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            if (environment == "Development") {
                configBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
            }

            _configuration = configBuilder.Build();
        }
    }
}