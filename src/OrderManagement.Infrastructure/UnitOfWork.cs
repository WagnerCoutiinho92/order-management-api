using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Infrastructure.Data;

namespace OrderManagement.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> CommitAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);

    /// <summary>
    /// Runs <paramref name="action"/> inside an explicit SQL Server transaction.
    /// The UPDLOCK hints issued by GetByIdsForUpdateAsync are held until
    /// SaveChanges + Commit — serializing concurrent order creation for the same products.
    /// Uses EF Core's execution strategy so retries work correctly with Resiliency enabled.
    /// </summary>
    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await action();
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
