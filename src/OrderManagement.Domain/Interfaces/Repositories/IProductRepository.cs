using OrderManagement.Domain.Entities;

namespace OrderManagement.Domain.Interfaces.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);

    /// <summary>
    /// Returns products locked for update (pessimistic locking) — used during order creation.
    /// </summary>
    Task<List<Product>> GetByIdsForUpdateAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
