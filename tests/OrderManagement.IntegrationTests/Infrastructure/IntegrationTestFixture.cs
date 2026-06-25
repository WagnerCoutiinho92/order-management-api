using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderManagement.Application.Helpers;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Domain.Interfaces.Repositories;
using OrderManagement.Infrastructure.Data;
using OrderManagement.IntegrationTests.Helpers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace OrderManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Shared fixture: creates OrderManagementDb_Test once per test run,
/// applies migrations, seeds an admin user, and drops the database when all tests finish.
/// </summary>
public class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string? _adminToken;

    // Credentials for the seeded admin user used across all integration tests
    private const string AdminEmail = "admin@test.com";
    private const string AdminPassword = "Admin@123";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.IntegrationTests.json"),
                optional: false);

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Auth:PermitLimit"] = "3",
                ["RateLimiting:Auth:WindowSeconds"] = "60",
                ["RateLimiting:Create:PermitLimit"] = "3",
                ["RateLimiting:Create:WindowSeconds"] = "60",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();

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
                       .EnableSensitiveDataLogging()
                       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Always recreate the test DB to guarantee a clean, up-to-date schema.
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        // Seed admin user for integration tests
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var admin = new User("Admin Teste", AdminEmail, PasswordHelper.Hash(AdminPassword), UserRole.Admin);
        await userRepo.AddAsync(admin);
        await uow.CommitAsync();

        // Obtain and cache admin JWT token
        var loginResp = await CreateClient().PostJsonAsync("/api/auth/login",
            new { email = AdminEmail, password = AdminPassword });
        var body = await loginResp.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        _adminToken = json.RootElement.GetProperty("token").GetString()!;
    }

    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    /// <summary>Creates an HttpClient with the admin JWT token pre-configured.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _adminToken);
        return client;
    }

    /// <summary>
    /// Creates an isolated DbContext for direct DB assertions in tests.
    /// Caller is responsible for disposing the returned context.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
