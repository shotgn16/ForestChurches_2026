using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Components.DatabaseMaintenance
{
    /// <summary>
    /// Keeps every text column in the site's tables on one collation (the rule MySQL/MariaDB uses to compare text).
    ///
    /// Why: migrations declare columns as "CHARACTER SET utf8mb4" without naming a collation, so the server fills in
    /// its own default. A database first created on MySQL 8 (utf8mb4_0900_ai_ci) and later moved to MariaDB 11
    /// (default utf8mb4_uca1400_ai_ci) ends up with old columns on one collation and newly added ones on another,
    /// and any query joining them fails with "Illegal mix of collations".
    ///
    /// The fix takes the collation of AspNetUsers.Id (the oldest table, so the one the data was created with) and
    /// converts any of the site's tables that have text columns on a different collation. When everything already
    /// matches it does nothing but one information_schema query.
    /// </summary>
    public static class CollationAligner
    {
        private const string ReferenceTable = "AspNetUsers";
        private const string ReferenceColumn = "Id";
        private static readonly Regex SafeName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        /// <summary>Aligns the tables in the EF model. Does nothing on databases other than MySQL/MariaDB.</summary>
        public static IReadOnlyList<string> Align(DbContext db, ILogger logger)
        {
            if (!db.Database.IsMySql()) return Array.Empty<string>();

            var tables = db.Model.GetEntityTypes()
                .Select(t => t.GetTableName())
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var connection = db.Database.GetDbConnection();
            var opened = false;
            if (connection.State != ConnectionState.Open) { connection.Open(); opened = true; }
            try
            {
                return Align(connection, tables, logger);
            }
            finally
            {
                if (opened) connection.Close();
            }
        }

        /// <summary>Aligns the given tables using an open connection. Returns the tables that were converted.</summary>
        public static IReadOnlyList<string> Align(DbConnection connection, IEnumerable<string> tables, ILogger? logger = null)
        {
            var wanted = new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);

            var reference = Scalar(connection,
                "SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c",
                ("@t", ReferenceTable), ("@c", ReferenceColumn));
            if (string.IsNullOrEmpty(reference) || !SafeName.IsMatch(reference)) return Array.Empty<string>();

            var charset = Scalar(connection,
                "SELECT CHARACTER_SET_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c",
                ("@t", ReferenceTable), ("@c", ReferenceColumn));
            if (string.IsNullOrEmpty(charset) || !SafeName.IsMatch(charset)) return Array.Empty<string>();

            var mismatched = new List<string>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT DISTINCT TABLE_NAME FROM information_schema.COLUMNS " +
                    "WHERE TABLE_SCHEMA = DATABASE() AND COLLATION_NAME IS NOT NULL AND COLLATION_NAME <> @ref";
                AddParam(cmd, "@ref", reference);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var table = reader.GetString(0);
                    if (wanted.Contains(table) && SafeName.IsMatch(table)) mismatched.Add(table);
                }
            }

            if (mismatched.Count == 0) return mismatched;

            logger?.LogWarning("Text collation differs from {Reference} in tables {Tables}; converting them so text can be compared.",
                reference, string.Join(", ", mismatched));

            // Foreign key columns can only change collation while key checks are off; this is per-connection.
            Execute(connection, "SET FOREIGN_KEY_CHECKS = 0");
            try
            {
                foreach (var table in mismatched)
                {
                    Execute(connection, $"ALTER TABLE `{table}` CONVERT TO CHARACTER SET {charset} COLLATE {reference}");
                }
            }
            finally
            {
                Execute(connection, "SET FOREIGN_KEY_CHECKS = 1");
            }

            logger?.LogInformation("Converted {Count} table(s) to {Reference}.", mismatched.Count, reference);
            return mismatched;
        }

        private static string? Scalar(DbConnection connection, string sql, params (string name, string value)[] parameters)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters) AddParam(cmd, name, value);
            return cmd.ExecuteScalar() as string;
        }

        private static void Execute(DbConnection connection, string sql)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 600;
            cmd.ExecuteNonQuery();
        }

        private static void AddParam(DbCommand cmd, string name, string value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
    }
}
