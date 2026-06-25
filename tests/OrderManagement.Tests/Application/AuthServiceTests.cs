using FluentAssertions;
using Moq;
using OrderManagement.Application.DTOs.Auth;
using OrderManagement.Application.Helpers;
using OrderManagement.Application.Interfaces;
using OrderManagement.Application.Services;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Domain.Interfaces.Repositories;
using Xunit;

namespace OrderManagement.Tests.Application;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokenServiceMock.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("fake-jwt-token");
        _tokenServiceMock.Setup(t => t.GetExpiration()).Returns(DateTime.UtcNow.AddHours(1));

        _sut = new AuthService(_userRepoMock.Object, _tokenServiceMock.Object, _uowMock.Object);
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_ReturnsAuthResponse()
    {
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("novo@email.com", default)).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);

        var result = await _sut.RegisterAsync(new RegisterRequest("Wagner", "novo@email.com", "senha123"));

        result.Should().NotBeNull();
        result.Token.Should().Be("fake-jwt-token");
        result.Email.Should().Be("novo@email.com");
        result.Name.Should().Be("Wagner");
        result.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsBusinessRuleException()
    {
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("existente@email.com", default)).ReturnsAsync(true);

        var act = async () => await _sut.RegisterAsync(
            new RegisterRequest("Wagner", "existente@email.com", "senha123"));

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "EMAIL_ALREADY_REGISTERED");
    }

    [Fact]
    public async Task Register_ValidRequest_CommitsUnitOfWork()
    {
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);

        await _sut.RegisterAsync(new RegisterRequest("Wagner", "novo@email.com", "senha123"));

        _uowMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        var user = new User("Wagner", "wagner@email.com", PasswordHelper.Hash("senha123"));
        _userRepoMock.Setup(r => r.GetByEmailAsync("wagner@email.com", default)).ReturnsAsync(user);

        var result = await _sut.LoginAsync(new LoginRequest("wagner@email.com", "senha123"));

        result.Should().NotBeNull();
        result.Token.Should().Be("fake-jwt-token");
        result.Email.Should().Be("wagner@email.com");
    }

    [Fact]
    public async Task Login_UserNotFound_ThrowsBusinessRuleException()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        var act = async () => await _sut.LoginAsync(
            new LoginRequest("inexistente@email.com", "senha123"));

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsBusinessRuleException()
    {
        var user = new User("Wagner", "wagner@email.com", PasswordHelper.Hash("senha-correta"));
        _userRepoMock.Setup(r => r.GetByEmailAsync("wagner@email.com", default)).ReturnsAsync(user);

        var act = async () => await _sut.LoginAsync(
            new LoginRequest("wagner@email.com", "senha-errada"));

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_ValidCredentials_DoesNotCommitUnitOfWork()
    {
        var user = new User("Wagner", "wagner@email.com", PasswordHelper.Hash("senha123"));
        _userRepoMock.Setup(r => r.GetByEmailAsync("wagner@email.com", default)).ReturnsAsync(user);

        await _sut.LoginAsync(new LoginRequest("wagner@email.com", "senha123"));

        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
