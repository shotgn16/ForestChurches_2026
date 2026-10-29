using ForestChurches.Components.DatabaseMaintenance;
using ForestChurches.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Xunit;

namespace ForestChurches.Tests;

/// <summary>
/// Runs against a real database only when FC_DB_TEST_CONNECTION is set: the CI job sets it after leaving the
/// database the way a MySQL 8 database moved to MariaDB 11 ends up (old tables on utf8mb4_0900_ai_ci and the
/// benefices migration half-applied). Otherwise it does nothing.
/// </summary>
public class DatabaseStartupIntegrationTests
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("FC_DB_TEST_CONNECTION");
    private static string OtherCollation => Environment.GetEnvironmentVariable("FC_DB_OTHER_COLLATION") ?? "utf8mb4_uca1400_ai_ci";

    private static ForestChurchesContext NewContext() => new(new DbContextOptionsBuilder<ForestChurchesContext>()
        .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString))
        .Options);

    private static object? Scalar(MySqlConnection c, string sql) { using var cmd = new MySqlCommand(sql, c); return cmd.ExecuteScalar(); }
    private static void Exec(MySqlConnection c, string sql) { using var cmd = new MySqlCommand(sql, c); cmd.ExecuteNonQuery(); }
    private static long ForeignKeyCount(MySqlConnection c) => Convert.ToInt64(Scalar(c,
        "SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()"));

    [Fact]
    public void Repairs_half_applied_migration_and_mixed_collations()
    {
        if (string.IsNullOrEmpty(ConnectionString)) return;

        using (var db = NewContext()) DatabaseStartup.Run(db, NullLogger.Instance);
        using (var db = NewContext())
        {
            Assert.Empty(db.Database.GetPendingMigrations());
            DatabaseStartup.Run(db, NullLogger.Instance);   // a second start-up changes nothing
        }

        using var connection = new MySqlConnection(ConnectionString);
        connection.Open();

        Assert.Equal(1L, Convert.ToInt64(Scalar(connection,
            $"SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '{DatabaseStartup.BeneficesMigration}'")));

        // The joins the church pages, My Church and search rely on.
        foreach (var sql in new[]
        {
            "SELECT COUNT(*) FROM ServiceTimes s LEFT JOIN ChurchInformation c ON s.LocationChurchId = c.ID",
            "SELECT COUNT(*) FROM ChurchInformation c LEFT JOIN Benefices b ON c.BeneficeId = b.Id",
            "SELECT COUNT(*) FROM ServiceDates d JOIN ServiceTimes s ON d.ServiceTimesId = s.ServiceID",
            "SELECT COUNT(*) FROM ChurchInformation c JOIN AspNetUsers u ON c.ChurchAccountId = u.Id",
        }) Scalar(connection, sql);

        // Foreign keys are in place and enforced.
        Assert.Throws<MySqlException>(() => Exec(connection,
            "INSERT INTO ServiceDates (Id, ServiceTimesId, Date) VALUES ('fk-check', 'no-such-service', '2026-01-01')"));

        // The aligner converts a table that takes part in foreign keys, keeping every key.
        var keysBefore = ForeignKeyCount(connection);
        Exec(connection, $"ALTER TABLE ChurchInformation ADD COLUMN ZzCollationTest varchar(10) CHARACTER SET utf8mb4 COLLATE {OtherCollation} NULL");
        try
        {
            using var db = NewContext();
            var converted = CollationAligner.Align(db, NullLogger.Instance);
            var reference = (string?)Scalar(connection,
                "SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id'");
            if (reference != OtherCollation) Assert.Contains("ChurchInformation", converted);
            Assert.Equal(keysBefore, ForeignKeyCount(connection));
            Assert.Equal(0L, Convert.ToInt64(Scalar(connection,
                $"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ChurchInformation' AND COLLATION_NAME <> '{reference}'")));
        }
        finally
        {
            Exec(connection, "ALTER TABLE ChurchInformation DROP COLUMN ZzCollationTest");
        }
    }
}
