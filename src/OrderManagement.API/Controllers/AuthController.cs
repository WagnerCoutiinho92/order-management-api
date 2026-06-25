using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Application.DTOs.Auth;
using OrderManagement.Application.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace OrderManagement.API.Controllers;

/// <summary>Autenticação e registro de usuários.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[Tags("Autenticação")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;

    public AuthController(IAuthService authService, IValidator<RegisterRequest> registerValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
    }

    /// <summary>Registra um novo usuário com perfil Customer.</summary>
    /// <response code="201">Usuário criado e token JWT retornado.</response>
    /// <response code="400">Dados de entrada inválidos.</response>
    /// <response code="422">E-mail já cadastrado.</response>
    [HttpPost("registrar")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var result = await _authService.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Autentica um usuário e retorna um token JWT.</summary>
    /// <response code="200">Login realizado com sucesso.</response>
    /// <response code="422">E-mail ou senha inválidos.</response>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(result);
    }
}
