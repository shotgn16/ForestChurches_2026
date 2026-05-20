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
            var AppDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var certificatePath = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? Environment.GetEnvironmentVariable("CERTIFICATE_PATH_PRO") : _configuration["Certificates:DevelopmentPath"];

            if (certificatePath == null)
            {
                Console.WriteLine("Certificate path cannot be null. Environment: {0}, CERTIFICATE_PATH_PRO: {1}, CERTIFICATE_PATH_DEV: {2}",
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    Environment.GetEnvironmentVariable("CERTIFICATE_PATH_PRO"),
                    _configuration["Certificates:DevelopmentPath"]);
                throw new InvalidOperationException("Certificate path cannot be null.");
            }

            var fullPath = Path.Combine(AppDirectory, certificatePath);
            var certificatePassword = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? Environment.GetEnvironmentVariable("CERTIFICATE_PWD") : _configuration["Certificates:Password"];
            var certificate = new X509Certificate2(fullPath, certificatePassword);

            // Authenticate with Azure using Certificate
            var keyVaultURL = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? Environment.GetEnvironmentVariable("KEY_VAULT_URL") : _configuration["Keyvault:Url"];
            var clientId = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? Environment.GetEnvironmentVariable("CLIENT_ID") : _configuration["Keyvault:ClientID"];
            var tenantId = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? Environment.GetEnvironmentVariable("DIRECTORY_ID") : _configuration["Keyvault:DirectoryID"];

            var clientCertificateCredential = new ClientCertificateCredential(tenantId, clientId, certificate);
            var client = new SecretClient(new Uri(keyVaultURL), clientCertificateCredential);

            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? client.GetSecret("db-connection-live").Value.Value : _configuration["ConnectionStrings:Development"];
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