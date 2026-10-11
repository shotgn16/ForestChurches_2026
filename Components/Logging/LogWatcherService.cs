using ForestChurches.Components.Configuration;
using ForestChurches.Components.Pageination;
using Microsoft.AspNetCore.SignalR;
using MySqlConnector;

namespace ForestChurches.Components.Logging
{
    public class LogWatcherService : BackgroundService
    {
        private readonly ILogger<LogWatcherService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostEnvironment _environment;
        private readonly IConfiguration _devConfiguration;
        private readonly IHubContext<ForestChurches.Hubs.LogsHub> _hubContext;
        private readonly ForestChurches.Components.Logging.SseManager _sseManager;
        private long _lastMaxId = 0;

        public LogWatcherService(ILogger<LogWatcherService> logger,
            IServiceProvider serviceProvider,
            IHostEnvironment environment,
            IConfiguration devConfiguration,
            IHubContext<ForestChurches.Hubs.LogsHub> hubContext,
            ForestChurches.Components.Logging.SseManager sseManager)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _environment = environment;
            _devConfiguration = devConfiguration;
            _hubContext = hubContext;
            _sseManager = sseManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var secretRefreshInterval = TimeSpan.FromMinutes(5);
            var lastSecretRefresh = DateTimeOffset.MinValue;
            string connectionString = string.Empty;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Refresh connection string only once every refresh interval (or on first run)
                    if (string.IsNullOrEmpty(connectionString) || DateTimeOffset.UtcNow - lastSecretRefresh >= secretRefreshInterval)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var configuration = scope.ServiceProvider.GetRequiredService<ForestChurches.Components.Configuration.Configuration>();
                        connectionString = _environment.EnvironmentName == "Development"
                            ? _devConfiguration["ConnectionStrings:Development"]
                            : configuration.Client.GetSecret("db-connection-live").Value.Value;
                        lastSecretRefresh = DateTimeOffset.UtcNow;
                    }

                    using var connection = new MySqlConnection(connectionString);
                    await connection.OpenAsync(stoppingToken);

                    // Only fetch logs newer than last seen id
                    var query = @"SELECT Id, Timestamp, `LogLevel` AS Level, Exception, Message, MessageTemplate AS Template, Properties
                          FROM applicationlogs
                          WHERE Id > @lastId
                          ORDER BY Id ASC
                          LIMIT 100";
                    using var command = new MySqlCommand(query, connection);
                    command.Parameters.AddWithValue("@lastId", _lastMaxId);

                    using var reader = await command.ExecuteReaderAsync(stoppingToken);
                    var rows = new List<Models.LogEntry>();
                    while (await reader.ReadAsync(stoppingToken))
                    {
                        var idStr = GetSafe(reader, "Id");
                        rows.Add(new Models.LogEntry
                        {
                            Id = idStr,
                            Timestamp = GetSafe(reader, "Timestamp"),
                            Level = GetSafe(reader, "Level"),
                            Exception = GetSafe(reader, "Exception") ?? string.Empty,
                            Message = GetSafe(reader, "Message"),
                            Template = GetSafe(reader, "Template"),
                            Properties = GetSafe(reader, "Properties")
                        });
                    }

                    // rows are newest-first; process oldest-first
                    rows.Reverse();

                    foreach (var r in rows)
                    {
                        if (long.TryParse(r.Id, out var lid))
                        {
                            if (lid > _lastMaxId)
                            {
                                _lastMaxId = lid;
                                // broadcast
                                try
                                {
                                    var payload = new
                                    {
                                        Id = r.Id,
                                        Timestamp = r.Timestamp,
                                        Level = r.Level,
                                        Exception = r.Exception,
                                        Message = r.Message,
                                        Template = r.Template,
                                        Properties = r.Properties
                                    };

                                    await _hubContext.Clients.All.SendAsync("NewLog", payload, cancellationToken: stoppingToken);
                                    // also publish to SSE manager for clients that might use EventSource fallback
                                    await _sseManager.PublishAsync(payload);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to broadcast log id {Id}", r.Id);
                                }
                            }
                        }
                        else
                        {
                            // non-numeric id, broadcast if not seen
                            if (string.IsNullOrEmpty(r.Id)) continue;
                            // use hash
                            var h = r.Id.GetHashCode();
                            if (h > _lastMaxId)
                            {
                                _lastMaxId = h;
                                await _hubContext.Clients.All.SendAsync("NewLog", r, cancellationToken: stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in LogWatcherService loop");
                }

                // Slightly longer delay to reduce load; make configurable if needed
                await Task.Delay(2000, stoppingToken);
            }
        }

        private static string GetSafe(MySqlDataReader reader, string name)
        {
            try
            {
                var idx = reader.GetOrdinal(name);
                if (reader.IsDBNull(idx)) return string.Empty;
                return reader.GetValue(idx)?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
