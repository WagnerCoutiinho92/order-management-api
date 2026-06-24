using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Infrastructure.Data;

namespace OrderManagement.Infrastructure;

/// <summary>
/// Wraps SaveChangesAsync and optionally manages an explicit transaction.
/// For order creation, the service starts a transaction via IDbContextTransaction
/// before calling GetByIdsForUpdateAsync (which issues UPDLOCK hints), then
/// commits via CommitAsync — ensuring the lock spans the whole operation.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> CommitAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        _context.Database.BeginTransactionAsync(ct);
}
