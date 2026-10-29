using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Components.DatabaseMaintenance
{
    /// <summary>
    /// Brings the database up to date at start-up, coping with servers whose default text collation differs from
    /// the one the existing tables were created with (e.g. data created on MySQL 8, now on MariaDB 11):
    ///  1. undoes a half-applied BeneficesAndServiceSchedules migration (MySQL/MariaDB can't roll back DDL, so a
    ///     failure part-way leaves some of its tables/columns behind and every retry then fails on its first step);
    ///  2. makes this connection create new text columns with the existing tables' collation, then migrates;
    ///  3. converts any of the site's tables that still have text columns on another collation.
    /// </summary>
    public static class DatabaseStartup
    {
        public const string BeneficesMigration = "20261028211943_BeneficesAndServiceSchedules";
        private static readonly Regex SafeName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        public static void Run(DbContext db, ILogger logger)
        {
            if (!db.Database.IsMySql())
            {
                if (db.Database.GetPendingMigrations().Any()) db.Database.Migrate();
                return;
            }

            // Keep one connection open so the session settings below apply to the migration itself.
            db.Database.OpenConnection();
            try
            {
                var connection = db.Database.GetDbConnection();
                if (db.Database.GetPendingMigrations().Any())
                {
                    if (db.Database.GetPendingMigrations().Contains(BeneficesMigration))
                        UndoPartialBeneficesMigration(connection, logger);

                    UseExistingCollationForNewColumns(connection, logger);
                    db.Database.Migrate();
                }

                CollationAligner.Align(connection, TablesOf(db), logger);
            }
            finally
            {
                db.Database.CloseConnection();
            }
        }

        private static List<string> TablesOf(DbContext db) => db.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        /// <summary>
        /// MySQL 8 and MariaDB 11.2+ let a session choose which collation "CHARACTER SET utf8mb4" means. Older MariaDB
        /// doesn't; there the aligner fixes things up afterwards.
        /// </summary>
        private static void UseExistingCollationForNewColumns(DbConnection connection, ILogger logger)
        {
            var reference = Scalar(connection,
                "SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id'");
            var charset = Scalar(connection,
                "SELECT CHARACTER_SET_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id'");
            if (reference == null || charset == null || !SafeName.IsMatch(reference) || !SafeName.IsMatch(charset)) return;

            foreach (var sql in new[]
            {
                $"SET SESSION character_set_collations = '{charset}={reference}'",   // MariaDB 11.2+
                $"SET SESSION default_collation_for_utf8mb4 = {reference}",           // MySQL 8.0
            })
            {
                try { Execute(connection, sql); logger.LogInformation("Migration session: {Sql}", sql); }
                catch (DbException) { /* not supported by this server */ }
            }
        }

        /// <summary>
        /// If BeneficesAndServiceSchedules isn't recorded as applied but some of its changes are present, removes them
        /// and restores what it removed, so the migration can run again from the start. Only the columns and tables
        /// that migration adds are dropped; nothing that existed before it is touched.
        /// </summary>
        public static void UndoPartialBeneficesMigration(DbConnection connection, ILogger logger)
        {
            // A brand-new database has nothing to repair.
            if (!TableExists(connection, "ChurchInformation") || !TableExists(connection, "ServiceTimes")) return;

            var addedServiceColumns = new[] { "DayOfMonth", "DayOfWeek", "EndDate", "EndTime", "Interval", "LocationAddress",
                                              "LocationChurchId", "RepeatType", "StartDate", "UseCustomLocation", "WeeksOfMonth" };
            var addedChurchColumns = new[] { "BeneficeId", "ImageBlobName" };

            var partial =
                TableExists(connection, "Benefices") || TableExists(connection, "ServiceDates") ||
                addedServiceColumns.Any(c => ColumnExists(connection, "ServiceTimes", c)) ||
                addedChurchColumns.Any(c => ColumnExists(connection, "ChurchInformation", c)) ||
                !IndexExists(connection, "ChurchInformation", "IX_ChurchInformation_ChurchAccountId") ||
                !ForeignKeyExists(connection, "ChurchInformation", "FK_ChurchInformation_AspNetUsers_ChurchAccountId");
            if (!partial) return;

            logger.LogWarning("Migration {Migration} was partly applied by an earlier start-up; undoing its partial changes so it can run cleanly.",
                BeneficesMigration);

            void Do(string sql) { logger.LogWarning("Repair: {Sql}", sql); Execute(connection, sql); }

            Execute(connection, "SET FOREIGN_KEY_CHECKS = 0");
            try
            {
                if (TableExists(connection, "ServiceDates")) Do("DROP TABLE `ServiceDates`");
                if (ForeignKeyExists(connection, "ServiceTimes", "FK_ServiceTimes_ChurchInformation_LocationChurchId"))
                    Do("ALTER TABLE `ServiceTimes` DROP FOREIGN KEY `FK_ServiceTimes_ChurchInformation_LocationChurchId`");
                if (ForeignKeyExists(connection, "ChurchInformation", "FK_ChurchInformation_Benefices_BeneficeId"))
                    Do("ALTER TABLE `ChurchInformation` DROP FOREIGN KEY `FK_ChurchInformation_Benefices_BeneficeId`");
                if (TableExists(connection, "Benefices")) Do("DROP TABLE `Benefices`");
                if (IndexExists(connection, "ServiceTimes", "IX_ServiceTimes_LocationChurchId"))
                    Do("DROP INDEX `IX_ServiceTimes_LocationChurchId` ON `ServiceTimes`");
                if (IndexExists(connection, "ChurchInformation", "IX_ChurchInformation_BeneficeId"))
                    Do("DROP INDEX `IX_ChurchInformation_BeneficeId` ON `ChurchInformation`");

                foreach (var c in addedServiceColumns.Where(c => ColumnExists(connection, "ServiceTimes", c)))
                    Do($"ALTER TABLE `ServiceTimes` DROP COLUMN `{c}`");
                foreach (var c in addedChurchColumns.Where(c => ColumnExists(connection, "ChurchInformation", c)))
                    Do($"ALTER TABLE `ChurchInformation` DROP COLUMN `{c}`");

                // The migration starts by dropping these two; put them back so its first steps work.
                if (!IndexExists(connection, "ChurchInformation", "IX_ChurchInformation_ChurchAccountId"))
                    Do("CREATE INDEX `IX_ChurchInformation_ChurchAccountId` ON `ChurchInformation` (`ChurchAccountId`)");
                if (!ForeignKeyExists(connection, "ChurchInformation", "FK_ChurchInformation_AspNetUsers_ChurchAccountId"))
                    Do("ALTER TABLE `ChurchInformation` ADD CONSTRAINT `FK_ChurchInformation_AspNetUsers_ChurchAccountId` " +
                       "FOREIGN KEY (`ChurchAccountId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE");
            }
            finally
            {
                Execute(connection, "SET FOREIGN_KEY_CHECKS = 1");
            }
        }

        private static bool TableExists(DbConnection c, string table) => Count(c,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @a", table) > 0;

        private static bool ColumnExists(DbConnection c, string table, string column) => Count(c,
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @a AND COLUMN_NAME = @b", table, column) > 0;

        private static bool IndexExists(DbConnection c, string table, string index) => Count(c,
            "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @a AND INDEX_NAME = @b", table, index) > 0;

        private static bool ForeignKeyExists(DbConnection c, string table, string name) => Count(c,
            "SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = @a AND CONSTRAINT_NAME = @b", table, name) > 0;

        private static long Count(DbConnection connection, string sql, string a, string? b = null)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            AddParam(cmd, "@a", a);
            if (b != null) AddParam(cmd, "@b", b);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        private static string? Scalar(DbConnection connection, string sql)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
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
