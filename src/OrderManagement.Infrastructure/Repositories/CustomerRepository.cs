using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Interfaces.Repositories;
using OrderManagement.Infrastructure.Data;

namespace OrderManagement.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _context;

    public CustomerRepository(AppDbContext context) => _context = context;

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IEnumerable<Customer>> GetAllAsync(int page, int pageSize, CancellationToken ct = default) =>
        await _context.Customers
            .OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.Customers.CountAsync(ct);

    public Task<bool> ExistsActiveWithEmailAsync(string email, Guid? excludeId = null, CancellationToken ct = default) =>
        _context.Customers
            .Where(c => c.IsActive && c.Email == email && (excludeId == null || c.Id != excludeId))
            .AnyAsync(ct);

    public Task<bool> ExistsActiveWithDocumentAsync(string document, Guid? excludeId = null, CancellationToken ct = default) =>
        _context.Customers
            .Where(c => c.IsActive && c.Document == document && (excludeId == null || c.Id != excludeId))
            .AnyAsync(ct);

    public async Task AddAsync(Customer customer, CancellationToken ct = default) =>
        await _context.Customers.AddAsync(customer, ct);
}
