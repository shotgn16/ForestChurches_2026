using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ForestChurches.Tools
{
    public class ImageMigrationHostedService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ImageMigrationHostedService> _logger;
        private readonly IConfiguration _config;

        public ImageMigrationHostedService(IServiceProvider services, ILogger<ImageMigrationHostedService> logger, IConfiguration config)
        {
            _services = services;
            _logger = logger;
            _config = config;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Only run when explicitly enabled in configuration to avoid long startup time
            var enabled = _config.GetValue<bool>("ImageMigration:Enabled", false);
            if (!enabled)
            {
                _logger.LogInformation("Image migration service is disabled in configuration.");
                return;
            }

            try
            {
                var (events, users) = await ImageMigrationUtility.MigrateAsync(_services, _logger, cancellationToken);
                _logger.LogInformation("Image migration completed: events={Events} users={Users}", events, users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image migration failed.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
