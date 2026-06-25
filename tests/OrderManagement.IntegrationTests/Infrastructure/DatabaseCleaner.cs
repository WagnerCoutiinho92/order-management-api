using Microsoft.EntityFrameworkCore;
using OrderManagement.Infrastructure.Data;

namespace OrderManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Deletes all rows between tests respecting FK order.
/// Using DELETE instead of TRUNCATE to avoid reseed issues with FK constraints.
/// </summary>
public static class DatabaseCleaner
{
    public static async Task CleanAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [OrderStatusHistories]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [OrderItems]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Orders]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Products]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Customers]");
    }
}
