using ForestChurches.Components.Configuration;
using ForestChurches.Components.Pageination;
using ForestChurches.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design.Internal;
using MySqlConnector;
using Microsoft.AspNetCore.SignalR;
using ForestChurches.Hubs;

namespace ForestChurches.Components.Logging
{
    public class LogRetriver
    {
        private readonly ILogger<LogRetriver> _logger;
        private readonly IHostEnvironment _enviroment;
        private readonly ForestChurches.Components.Configuration.Configuration _configuration;
        private readonly string _connectionString;
        private readonly IConfiguration _devConfiguration;
        private readonly IHubContext<LogsHub> _hubContext;
        private readonly ForestChurches.Components.Logging.SseManager _sseManager;

        public List<Models.LogEntry> Logs { get; set; }
        private bool _isRunning;

        public LogRetriver(ILogger<LogRetriver> logger, ForestChurches.Components.Configuration.Configuration configuration, IHostEnvironment enviroment, IConfiguration devConfiguration, IHubContext<LogsHub> hubContext, ForestChurches.Components.Logging.SseManager sseManager)
        {
            _logger = logger;
            _enviroment = enviroment;
            _configuration = configuration;
            _devConfiguration = devConfiguration;
            _hubContext = hubContext;
            _sseManager = sseManager;

            _connectionString = _enviroment.EnvironmentName == "Development" ? _devConfiguration["ConnectionStrings:Development"] : _configuration.Client.GetSecret("db-connection-live").Value.Value;
            Logs = new List<Models.LogEntry>();
        }

        public async Task AddLogToDatabase(Models.LogEntry logEntry)
        {
            var query = "INSERT INTO system_logs (Timestamp, Level, Exception, Message, Url, Action, Template, Properties) VALUES (@Timestamp, @Level, @Exception, @Message, @Url, @Action, @Template, @Properties)";

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Timestamp", logEntry.Timestamp);
                    command.Parameters.AddWithValue("@Level", logEntry.Level);
                    command.Parameters.AddWithValue("@Exception", logEntry.Exception);
                    command.Parameters.AddWithValue("@Message", logEntry.Message);
                    command.Parameters.AddWithValue("@Template", logEntry.Template ?? string.Empty);
                    command.Parameters.AddWithValue("@Properties", logEntry.Properties ?? string.Empty);

                    await command.ExecuteNonQueryAsync();
                }

                // retrieve the inserted id
                using (var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID();", connection))
                {
                    var idObj = await idCmd.ExecuteScalarAsync();
                    if (idObj != null)
                    {
                        logEntry.Id = Convert.ToString(idObj);
                    }
                }
            }

                // Notify connected SignalR clients that a new log was added and send the full object
            try
            {
                var payload = new
                {
                    Id = logEntry.Id,
                    Timestamp = logEntry.Timestamp,
                    Level = logEntry.Level,
                    Exception = logEntry.Exception,
                    Message = logEntry.Message,
                    Template = logEntry.Template,
                    Properties = logEntry.Properties
                };

                await _hubContext.Clients.All.SendAsync("NewLog", payload);

                // Also publish to SSE manager in case some clients use EventSource fallback
                await _sseManager.PublishAsync(payload);
            }
            catch
            {
                // Swallow SignalR/SSE errors to avoid breaking logging
            }
        }

        private static readonly string[] LogTableNames = { "applicationlogs", "system_logs", "systemlogs", "system-logs", "system.logs" };

        // Columns worth searching, in the spellings the different log sinks use. Properties holds the request ID.
        private static readonly string[] LogSearchColumns = { "Message", "RenderedMessage", "Exception", "Level", "Url", "Action", "Properties", "Template", "MessageTemplate" };

        /// <summary>
        /// Picks the first log table that exists and returns which of the searchable columns it actually has
        /// (the tables written by the different sinks don't all have Url/Action, and searching a missing column fails).
        /// </summary>
        private static async Task<(string table, List<string> searchColumns)> ResolveLogTableAsync(MySqlConnection connection)
        {
            var found = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new MySqlCommand(
                "SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN (" +
                string.Join(",", LogTableNames.Select((_, i) => "@t" + i)) + ")", connection))
            {
                for (var i = 0; i < LogTableNames.Length; i++) cmd.Parameters.AddWithValue("@t" + i, LogTableNames[i]);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var table = reader.GetString(0);
                    if (!found.TryGetValue(table, out var cols)) found[table] = cols = new List<string>();
                    cols.Add(reader.GetString(1));
                }
            }

            foreach (var name in LogTableNames)
            {
                var key = found.Keys.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
                if (key == null) continue;
                var columns = found[key];
                var search = LogSearchColumns
                    .Select(c => columns.FirstOrDefault(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase)))
                    .Where(c => c != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return (key, search);
            }

            return (LogTableNames[0], new List<string> { "Message" });
        }

        private static string SearchClause(List<string> columns) =>
            "(" + string.Join(" OR ", columns.Select(c => $"`{c.Replace("`", "``")}` LIKE @Search")) + ")";

        public async Task<PaginatedList<Models.LogEntry>> LogsOnCall(int pageNumber, int pageSize, string searchQuery = null)
        {
            var logs = new List<Models.LogEntry>();
            var totalLogs = 0;

            try
            {
                _logger.LogInformation("Retrieving logs from database. PageNumber: {PageNumber}, PageSize: {PageSize}, Search: {Search}", pageNumber, pageSize, searchQuery);

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // Find the log table and the text columns it really has, so the search only names columns that exist.
                    var (baseTable, searchColumns) = await ResolveLogTableAsync(connection);

                    var query = $"SELECT SQL_CALC_FOUND_ROWS * FROM `{baseTable}`";
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                    {
                        query += " WHERE " + SearchClause(searchColumns);
                    }

                    // Newest first
                    query += " ORDER BY Id DESC LIMIT @Offset, @PageSize";

                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                        command.Parameters.AddWithValue("@PageSize", pageSize);

                        if (!string.IsNullOrWhiteSpace(searchQuery))
                        {
                            command.Parameters.AddWithValue("@Search", "%" + searchQuery + "%");
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string GetSafeLocal(MySqlDataReader r, string[] names)
                                {
                                    foreach (var n in names)
                                    {
                                        try
                                        {
                                            var idx = r.GetOrdinal(n);
                                            if (!r.IsDBNull(idx)) return r.GetValue(idx)?.ToString() ?? string.Empty;
                                        }
                                        catch { }
                                    }
                                    return string.Empty;
                                }

                                var id = GetSafeLocal(reader, new[] { "Id", "id" });
                                var timestamp = GetSafeLocal(reader, new[] { "Timestamp", "timestamp", "TimeStamp", "Time", "TimestampUtc" });
                                var level = GetSafeLocal(reader, new[] { "Level", "level", "LogLevel" });
                                var exception = GetSafeLocal(reader, new[] { "Exception", "exception" });
                                var message = GetSafeLocal(reader, new[] { "Message", "message", "RenderedMessage", "renderedMessage", "MessageTemplate" });
                                var url = GetSafeLocal(reader, new[] { "Url", "url", "SourceContext" });
                                var action = GetSafeLocal(reader, new[] { "Action", "action" });
                                var template = GetSafeLocal(reader, new[] { "MessageTemplate", "Template", "MessageTemplate" });
                                var properties = GetSafeLocal(reader, new[] { "Properties", "properties" });

                                logs.Add(new Models.LogEntry
                                {
                                    Id = id,
                                    Timestamp = timestamp,
                                    Level = level,
                                    Exception = exception,
                                    Message = message,
                                    Template = template,
                                    Properties = properties
                                });
                            }
                        }
                    }

                    query = "SELECT FOUND_ROWS()";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        try
                        {
                            totalLogs = Convert.ToInt32(await command.ExecuteScalarAsync());
                        }
                        catch
                        {
                            // Some MariaDB setups may not return FOUND_ROWS(); fall back to counting using the detected base table
                            using (var countCmd = new MySqlCommand($"SELECT COUNT(1) FROM {baseTable}", connection))
                            {
                                totalLogs = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                            }
                        }
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

        public async Task<List<Models.LogEntry>> GetLogsWithDateRange(DateTime fromDate, DateTime toDate, string searchQuery = null)
        {
            var logs = new List<Models.LogEntry>();

            try
            {
                _logger.LogInformation("Retrieving logs from database with date range. From: {FromDate}, To: {ToDate}, Search: {Search}", fromDate, toDate, searchQuery);

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // Find the log table and the text columns it really has, so the search only names columns that exist.
                    var (baseTable, searchColumns) = await ResolveLogTableAsync(connection);

                    var query = $"SELECT * FROM `{baseTable}` WHERE Timestamp >= @FromDate AND Timestamp <= @ToDate";

                    if (!string.IsNullOrWhiteSpace(searchQuery))
                    {
                        query += " AND " + SearchClause(searchColumns);
                    }

                    // Newest first
                    query += " ORDER BY Id DESC";

                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@FromDate", fromDate);
                        command.Parameters.AddWithValue("@ToDate", toDate);

                        if (!string.IsNullOrWhiteSpace(searchQuery))
                        {
                            command.Parameters.AddWithValue("@Search", "%" + searchQuery + "%");
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string GetSafeLocal(MySqlDataReader r, string[] names)
                                {
                                    foreach (var n in names)
                                    {
                                        try
                                        {
                                            var idx = r.GetOrdinal(n);
                                            if (!r.IsDBNull(idx)) return r.GetValue(idx)?.ToString() ?? string.Empty;
                                        }
                                        catch { }
                                    }
                                    return string.Empty;
                                }

                                var id = GetSafeLocal(reader, new[] { "Id", "id" });
                                var timestamp = GetSafeLocal(reader, new[] { "Timestamp", "timestamp", "TimeStamp", "Time", "TimestampUtc" });
                                var level = GetSafeLocal(reader, new[] { "Level", "level", "LogLevel" });
                                var exception = GetSafeLocal(reader, new[] { "Exception", "exception" });
                                var message = GetSafeLocal(reader, new[] { "Message", "message", "RenderedMessage", "renderedMessage", "MessageTemplate" });
                                var url = GetSafeLocal(reader, new[] { "Url", "url", "SourceContext" });
                                var action = GetSafeLocal(reader, new[] { "Action", "action" });
                                var template = GetSafeLocal(reader, new[] { "MessageTemplate", "Template", "MessageTemplate" });
                                var properties = GetSafeLocal(reader, new[] { "Properties", "properties" });

                                logs.Add(new Models.LogEntry
                                {
                                    Id = id,
                                    Timestamp = timestamp,
                                    Level = level,
                                    Exception = exception,
                                    Message = message,
                                    Template = template,
                                    Properties = properties
                                });
                            }
                        }
                    }
                }

                _logger.LogInformation($"Retrieved {logs.Count} logs from database for date range.");

                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving logs from the database with date range.");
                throw;
            }
        }
    }
}
