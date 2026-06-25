using OrderManagement.Domain.Entities;

namespace OrderManagement.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
}
