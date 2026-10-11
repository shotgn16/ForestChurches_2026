using MySqlConnector;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Configuration = ForestChurches.Components.Configuration.Configuration;

namespace ForestChurches.Areas.Identity.Data
{
    public class DatabaseService : IDatabaseService
    {
        private readonly Configuration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(Configuration configuration, IHostEnvironment enviroment, ILogger<DatabaseService> logger)
        {
            _configuration = configuration;
            _environment = enviroment;
            _logger = logger;
        }

        public async Task<float> GetLogDatabaseSize()
        {
            var environment = _environment.EnvironmentName;
            float databaseSize = 0f;

            string connectionString = null;
            try
            {
                // Use the same location Program.cs uses for Development connection string
                var secretName = environment == "Production" ? "db-connection-live" : "ConnectionStrings:Development";
                connectionString = _configuration.GetSecretValue(secretName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to read database connection string from configuration: {Message}", ex.Message);
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger?.LogWarning("Database connection string is empty. Returning size 0.");
                return databaseSize;
            }

            try
            {
                // Parse and log non-sensitive parts of the connection string to help with debugging
                try
                {
                    var builder = new MySqlConnectionStringBuilder(connectionString);
                    _logger?.LogInformation("Attempting to open MySQL connection to {Server}:{Port}, Database={Database}", builder.Server, builder.Port, builder.Database);
                }
                catch (Exception parseEx)
                {
                    _logger?.LogInformation("Could not parse connection string for diagnostic logging: {Message}", parseEx.Message);
                }

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);

                    using (var command = new MySqlCommand())
                    {
                        command.Connection = connection;
                        // Build the query with proper spacing to avoid accidental token concatenation
                        command.CommandText =
                            "SELECT ROUND((data_length + index_length) / 1024 / 1024, 2)" + 
                            " AS size_mb FROM information_schema.TABLES WHERE LOWER(table_schema) = LOWER('forestchurches')" +
                            " AND LOWER(table_name) = LOWER('applicationlogs');";

                        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
                        if (result != null)
                        {
                            try
                            {
                                // Handle numeric types (decimal/double/int) as well as strings
                                databaseSize = Convert.ToSingle(result);
                            }
                            catch
                            {
                                if (float.TryParse(result.ToString(), out float size))
                                {
                                    databaseSize = size;
                                }
                            }
                        }
                    }
                }
            }
            catch (MySqlException mex)
            {
                _logger?.LogWarning("MySQL error while getting log database size: {Message}. Returning 0.", mex.Message);
                return 0f;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Unexpected error while getting log database size: {Message}. Returning 0.", ex.Message);
                return 0f;
            }

            return databaseSize;
        }

        public async Task<float> GetDatabaseSize()
        {
            var environment = _environment.EnvironmentName;
            float databaseSize = 0f;

            string connectionString = null;
            try
            {
                // Use the same location Program.cs uses for Development connection string
                var secretName = environment == "Production" ? "db-connection-live" : "ConnectionStrings:Development";
                connectionString = _configuration.GetSecretValue(secretName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to read database connection string from configuration: {Message}", ex.Message);
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger?.LogWarning("Database connection string is empty. Returning size 0.");
                return databaseSize;
            }

            try
            {
                try
                {
                    var builder = new MySqlConnectionStringBuilder(connectionString);
                    _logger?.LogInformation("Attempting to open MySQL connection to {Server}:{Port}, Database={Database}", builder.Server, builder.Port, builder.Database);
                }
                catch (Exception parseEx)
                {
                    _logger?.LogInformation("Could not parse connection string for diagnostic logging: {Message}", parseEx.Message);
                }

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);

                    using (var command = new MySqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText =
                            "SELECT table_schema, ROUND(SUM(data_length + index_length)" + 
                            " / 1024 / 1024, 2) AS size_mb FROM information_schema.tables" + 
                            " WHERE LOWER(table_schema) = LOWER('forestchurches') GROUP BY table_schema; ";

                        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
                        if (result != null)
                        {
                            try
                            {
                                databaseSize = Convert.ToSingle(result);
                            }
                            catch
                            {
                                if (float.TryParse(result.ToString(), out float size))
                                {
                                    databaseSize = size;
                                }
                            }
                        }
                    }
                }
            }
            catch (MySqlException mex)
            {
                _logger?.LogWarning("MySQL error while getting database size: {Message}. Returning 0.", mex.Message);
                return 0f;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Unexpected error while getting database size: {Message}. Returning 0.", ex.Message);
                return 0f;
            }

            return databaseSize;
        }

        // ChurchSuite Integration

    }
}
