using OrderManagement.Domain.Entities;

namespace OrderManagement.Domain.Interfaces.Repositories;

public interface IOrderRepository
{
    /// <summary>Full read — includes Customer, Items, Products, StatusHistory. AsNoTracking.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Write path — includes only Items and StatusHistory (tracked). No Customer/Product navigation.</summary>
    Task<Order?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<Order>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
}
