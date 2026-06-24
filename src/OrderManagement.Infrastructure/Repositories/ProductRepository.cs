using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Interfaces.Repositories;
using OrderManagement.Infrastructure.Data;

namespace OrderManagement.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context) => _context = context;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Product>> GetAllAsync(int page, int pageSize, CancellationToken ct = default) =>
        await _context.Products
            .OrderBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.Products.CountAsync(ct);

    public async Task AddAsync(Product product, CancellationToken ct = default) =>
        await _context.Products.AddAsync(product, ct);

    /// <summary>
    /// Loads products with SQL Server UPDLOCK + ROWLOCK hints (pessimistic concurrency).
    /// Concurrent requests targeting the same products will queue, preventing double-debit.
    /// Must be called inside an explicit transaction (handled by the caller via IUnitOfWork).
    /// </summary>
    public async Task<List<Product>> GetByIdsForUpdateAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();

        // Build parameterized IN clause — safe from SQL injection
        var parameters = idList
            .Select((id, i) => new SqlParameter($"@p{i}", id))
            .ToArray<object>();

        var paramNames = string.Join(", ", parameters.Cast<SqlParameter>().Select(p => p.ParameterName));
        var sql = $"SELECT * FROM [Products] WITH (UPDLOCK, ROWLOCK) WHERE [Id] IN ({paramNames})";

        return await _context.Products
            .FromSqlRaw(sql, parameters)
            .ToListAsync(ct);
    }
}
