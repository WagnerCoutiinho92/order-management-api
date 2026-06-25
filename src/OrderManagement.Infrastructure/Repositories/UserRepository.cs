using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Interfaces.Repositories;
using OrderManagement.Infrastructure.Data;

namespace OrderManagement.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        _context.Users.AnyAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await _context.Users.AddAsync(user, ct);

    public Task<bool> AnyAsync(CancellationToken ct = default) =>
        _context.Users.AnyAsync(ct);
}
