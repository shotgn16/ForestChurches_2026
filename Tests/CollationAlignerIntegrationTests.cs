using ForestChurches.Components.DatabaseMaintenance;
using ForestChurches.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Xunit;

namespace ForestChurches.Tests;

/// <summary>
/// Runs against a real database only when FC_DB_TEST_CONNECTION is set (the CI migration job sets it after
/// deliberately giving the old tables a different collation from the new ones). Otherwise it does nothing.
/// </summary>
public class CollationAlignerIntegrationTests
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("FC_DB_TEST_CONNECTION");

    [Fact]
    public void Aligns_mixed_collations_so_joins_work()
    {
        if (string.IsNullOrEmpty(ConnectionString)) return;

        var options = new DbContextOptionsBuilder<ForestChurchesContext>()
            .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString))
            .Options;

        using (var db = new ForestChurchesContext(options))
        {
            CollationAligner.Align(db, NullLogger.Instance);
            // A second run finds nothing left to do.
            Assert.Empty(CollationAligner.Align(db, NullLogger.Instance));
        }

        using var connection = new MySqlConnection(ConnectionString);
        connection.Open();

        // The joins the church pages, My Church and search rely on.
        foreach (var sql in new[]
        {
            "SELECT COUNT(*) FROM ServiceTimes s LEFT JOIN ChurchInformation c ON s.LocationChurchId = c.ID",
            "SELECT COUNT(*) FROM ChurchInformation c LEFT JOIN Benefices b ON c.BeneficeId = b.Id",
            "SELECT COUNT(*) FROM ServiceDates d JOIN ServiceTimes s ON d.ServiceTimesId = s.ServiceID",
            "SELECT COUNT(*) FROM ChurchInformation c JOIN AspNetUsers u ON c.ChurchAccountId = u.Id",
        })
        {
            using var cmd = new MySqlCommand(sql, connection);
            cmd.ExecuteScalar();
        }

        // Foreign keys are still enforced after the conversion.
        using var bad = new MySqlCommand(
            "INSERT INTO ServiceDates (Id, ServiceTimesId, Date) VALUES ('fk-check', 'no-such-service', '2026-01-01')", connection);
        Assert.Throws<MySqlException>(() => bad.ExecuteNonQuery());
    }
}
