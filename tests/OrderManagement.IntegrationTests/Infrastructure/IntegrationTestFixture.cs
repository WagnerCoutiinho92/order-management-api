using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderManagement.Infrastructure.Data;
using Xunit;

namespace OrderManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Shared fixture: creates OrderManagementDb_Test once per test run,
/// applies migrations and drops the database when all tests finish.
/// </summary>
public class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use "Testing" environment so Program.cs auto-migration block (IsDevelopment) is skipped
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove original DbContext registration
            services.RemoveAll<DbContextOptions<AppDbContext>>();

            // Load test connection string from appsettings.IntegrationTests.json
            var testConfig = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
                .Build();

            var connStr = testConfig.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found in appsettings.IntegrationTests.json.");

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connStr, sql => sql.EnableRetryOnFailure(3))
                       .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
                       .EnableSensitiveDataLogging());
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Always recreate the test DB to guarantee a clean, up-to-date schema.
        // This prevents stale schema issues when a previous session didn't dispose cleanly.
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Creates an isolated DbContext for direct DB assertions in tests.
    /// Caller is responsible for disposing the returned context.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        // Create a fresh scope that is tied to the returned DbContext lifetime.
        // The scope is disposed when the DbContext is disposed, preventing connection leaks.
        var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db;
    }
}


