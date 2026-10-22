using ForestChurches.Components.Roles;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Components.Configuration
{
    public class ContextConfiguration
    {
        internal static void ConfigureDatabaseContext(IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ForestChurchesContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    mysqlOptions => mysqlOptions
                        .EnableStringComparisonTranslations()
                        // Increase command timeout (seconds) for long-running SQL operations
                        .CommandTimeout(360))   
                .EnableDetailedErrors()
                // Parameter values (tokens, password hashes) only appear in EF logs on a developer's machine.
                .EnableSensitiveDataLogging(string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)));

            services.AddDefaultIdentity<ChurchAccount>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<ChurchRoles>()
                .AddEntityFrameworkStores<ForestChurchesContext>();
        }
    }
}