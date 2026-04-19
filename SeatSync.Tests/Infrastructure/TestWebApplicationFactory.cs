extern alias SeatSyncApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SeatSync.Infrastructure.Data;

namespace SeatSync.Tests.Infrastructure;

public class TestWebApplicationFactory
    : WebApplicationFactory<SeatSyncApi::Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // 1. Remove BOTH the Options and the Configuration for your DbContext
            // In .NET 9+, removing IDbContextOptionsConfiguration is often necessary
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<SeatSyncDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            var configDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDbContextOptionsConfiguration<SeatSyncDbContext>));
            if (configDescriptor != null) services.Remove(configDescriptor);

            // 2. Setup SQLite connection (keep alive for in-memory)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<SeatSyncDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // 3. Initialize the schema
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SeatSyncDbContext>();
        
            // This ensures the in-memory SQLite tables are created
            db.Database.EnsureCreated();
            DemoDataSeeder.SeedAsync(db, TimeProvider.System).GetAwaiter().GetResult();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}
