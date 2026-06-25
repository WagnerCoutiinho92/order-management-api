using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
    DateTime GetExpiration();
}
