using ForestChurches.Components.Configuration;
using ForestChurches.Components.Pageination;
using ForestChurches.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design.Internal;
using MySqlConnector;

namespace ForestChurches.Components.Logging
{
    public class LogRetriver
    {
        private readonly ILogger<LogRetriver> _logger;
        private readonly IHostEnvironment _enviroment;
        private readonly ForestChurches.Components.Configuration.Configuration _configuration;
        private readonly string _connectionString;
        private readonly IConfiguration _devConfiguration;

        public List<Models.LogEntry> Logs { get; set; }
        private bool _isRunning;

        public LogRetriver(ILogger<LogRetriver> logger, ForestChurches.Components.Configuration.Configuration configuration, IHostEnvironment enviroment, IConfiguration devConfiguration)
        {
            _logger = logger;
            _enviroment = enviroment;
            _configuration = configuration;
            _devConfiguration = devConfiguration;

            _connectionString = _enviroment.EnvironmentName == "Development" ? _devConfiguration["ConnectionStrings:Development"] : _configuration.Client.GetSecret("db-connection-live").Value.Value;
            Logs = new List<Models.LogEntry>();
        }

        public async Task AddLogToDatabase(Models.LogEntry logEntry)
        {
            var query = "INSERT INTO system_logs (Timestamp, Level, Exception, Message, Url, Action) VALUES (@Timestamp, @Level, @Exception, @Message, @Url, @Action)";

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Timestamp", logEntry.Timestamp);
                    command.Parameters.AddWithValue("@Level", logEntry.Level);
                    command.Parameters.AddWithValue("@Exception", logEntry.Exception);
                    command.Parameters.AddWithValue("@Message", logEntry.Message);
                    command.Parameters.AddWithValue("@Url", logEntry.Url);
                    command.Parameters.AddWithValue("@Action", logEntry.Action);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<PaginatedList<Models.LogEntry>> LogsOnCall(int pageNumber, int pageSize)
        {
            var logs = new List<Models.LogEntry>();
            var totalLogs = 0;

            try
            {
                _logger.LogInformation("Retrieving logs from database. PageNumber: {PageNumber}, PageSize: {PageSize}", pageNumber, pageSize);

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = "SELECT SQL_CALC_FOUND_ROWS * FROM system_logs ORDER BY Id ASC LIMIT @Offset, @PageSize";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                        command.Parameters.AddWithValue("@PageSize", pageSize);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                logs.Add(new Models.LogEntry
                                {
                                    Timestamp = reader["Timestamp"].ToString(),
                                    Level = reader["Level"].ToString(),
                                    Exception = reader["Exception"].ToString(),
                                    Message = reader["Message"].ToString(),
                                    Url = reader["Url"].ToString(),
                                    Action = reader["Action"].ToString()
                                });
                            }
                        }
                    }

                    query = "SELECT FOUND_ROWS()";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        totalLogs = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }
                }

                _logger.LogInformation($"Retrieved {logs.Count} logs from database.");

                return new PaginatedList<Models.LogEntry>(logs, totalLogs, pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving logs from the database.");
                throw;
            }
        }
    }
}
