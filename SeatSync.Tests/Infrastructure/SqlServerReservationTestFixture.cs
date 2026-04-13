using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Tests.Infrastructure;

public sealed class SqlServerReservationTestFixture : IAsyncLifetime
{
    private string? _databaseName;
    public string? ConnectionString { get; private set; }
    public string? SkipReason { get; private set; }
    public bool IsAvailable => ConnectionString is not null;

    public async Task InitializeAsync()
    {
        var enabled = Environment.GetEnvironmentVariable("SEATSYNC_RUN_SQLSERVER_TESTS");
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            SkipReason = "Set SEATSYNC_RUN_SQLSERVER_TESTS=true to run SQL Server integration tests.";
            return;
        }

        try
        {
            var baseConnectionString =
                Environment.GetEnvironmentVariable("SEATSYNC_SQLSERVER_TEST_CONNECTION")
                ?? "Server=localhost,1433;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;";

            var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = "master"
            };

            _databaseName = $"SeatSyncTest_{Guid.NewGuid():N}";
            await using (var masterConnection = new SqlConnection(masterBuilder.ConnectionString))
            {
                await masterConnection.OpenAsync();
                await using var createDatabase = masterConnection.CreateCommand();
                createDatabase.CommandText = $"CREATE DATABASE [{_databaseName}]";
                await createDatabase.ExecuteNonQueryAsync();
            }

            var testBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = _databaseName
            };
            ConnectionString = testBuilder.ConnectionString;

            await using var db = CreateDbContext();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            SkipReason = $"SQL Server test setup failed: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_databaseName is null)
        {
            return;
        }

        try
        {
            var baseConnectionString =
                Environment.GetEnvironmentVariable("SEATSYNC_SQLSERVER_TEST_CONNECTION")
                ?? "Server=localhost,1433;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;";

            var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = "master"
            };

            await using var masterConnection = new SqlConnection(masterBuilder.ConnectionString);
            await masterConnection.OpenAsync();

            await using var dropDatabase = masterConnection.CreateCommand();
            dropDatabase.CommandText =
                $"""
                 ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                 DROP DATABASE [{_databaseName}];
                 """;
            await dropDatabase.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup in tests.
        }
    }

    public SeatSyncDbContext CreateDbContext()
    {
        if (ConnectionString is null)
        {
            throw new InvalidOperationException(SkipReason ?? "SQL Server fixture not initialized.");
        }

        var options = new DbContextOptionsBuilder<SeatSyncDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new SeatSyncDbContext(options);
    }

    public bool EnsureAvailable() => ConnectionString is not null;
}
