using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Interfaces.Repositories;
using OrderManagement.Infrastructure.Data;

namespace OrderManagement.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context) => _context = context;

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IEnumerable<Order>> GetAllAsync(int page, int pageSize, CancellationToken ct = default) =>
        await _context.Orders
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
