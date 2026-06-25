using OrderManagement.Application.DTOs.Auth;
using OrderManagement.Application.Helpers;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Domain.Interfaces.Repositories;

namespace OrderManagement.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _uow;

    public AuthService(IUserRepository userRepository, ITokenService tokenService, IUnitOfWork uow)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _uow = uow;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, ct))
            throw new BusinessRuleException("EMAIL_ALREADY_REGISTERED", "E-mail já cadastrado.");

        var user = new User(request.Name, request.Email, PasswordHelper.Hash(request.Password));

        await _userRepository.AddAsync(user, ct);
        await _uow.CommitAsync(ct);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, ct);

        if (user is null || !PasswordHelper.Verify(request.Password, user.PasswordHash))
            throw new BusinessRuleException("INVALID_CREDENTIALS", "E-mail ou senha inválidos.");

        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var token = _tokenService.GenerateToken(user);
        return new AuthResponse(token, user.Name, user.Email, user.Role.ToString(), _tokenService.GetExpiration());
    }
}
