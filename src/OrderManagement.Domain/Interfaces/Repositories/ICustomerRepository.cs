using OrderManagement.Domain.Entities;

namespace OrderManagement.Domain.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Customer>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<bool> ExistsActiveWithEmailAsync(string email, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> ExistsActiveWithDocumentAsync(string document, Guid? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
}
