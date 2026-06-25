using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Interfaces.Repositories;
using OrderManagement.Infrastructure.Data;

namespace OrderManagement.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Read-only — used by GET endpoints and to build responses after saves.
    /// AsNoTracking: nothing is tracked, no interference with SaveChanges.
    /// </summary>
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <summary>
    /// Write path — used by UpdateStatusAsync.
    /// Tracked (no AsNoTracking) so EF Core detects status/StatusHistory changes.
    /// Intentionally omits Customer and Product navigations to avoid
    /// tracking unrelated entities during SaveChanges.
    /// </summary>
    public Task<Order?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
        _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IEnumerable<Order>> GetAllAsync(int page, int pageSize, CancellationToken ct = default) =>
        await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.StatusHistory)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.Orders.CountAsync(ct);

    public async Task AddAsync(Order order, CancellationToken ct = default) =>
        await _context.Orders.AddAsync(order, ct);
}
