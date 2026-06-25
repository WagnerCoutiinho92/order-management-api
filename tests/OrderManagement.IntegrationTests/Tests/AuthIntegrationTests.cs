using System.Net;
using System.Text.Json;
using FluentAssertions;
using OrderManagement.IntegrationTests.Helpers;
using OrderManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderManagement.IntegrationTests.Tests;

[Collection("Integration")]
public class AuthIntegrationTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFixture _fixture;

    public AuthIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient(); // sem autenticação — é o ponto destes testes
    }

    public async Task InitializeAsync()
    {
        using var db = _fixture.CreateDbContext();
        await DatabaseCleaner.CleanAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Registro ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidData_Returns201WithToken()
    {
        var response = await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Wagner",
            email = "wagner@example.com",
            password = "senha123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("email").GetString().Should().Be("wagner@example.com");
        body.GetProperty("role").GetString().Should().Be("Customer");
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var response = await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Wagner",
            email = "nao-e-email",
            password = "senha123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var response = await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Wagner",
            email = "wagner@example.com",
            password = "123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_EmptyName_Returns400()
    {
        var response = await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "",
            email = "wagner@example.com",
            password = "senha123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns422()
    {
        await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Wagner",
            email = "duplicado@example.com",
            password = "senha123"
        });

        var response = await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Outro",
            email = "duplicado@example.com",
            password = "outrasenha"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Wagner",
            email = "wagner@example.com",
            password = "senha123"
        });

        var response = await _client.PostJsonAsync("/api/auth/login", new
        {
            email = "wagner@example.com",
            password = "senha123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("expiresAt").GetDateTime().Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns422()
    {
        await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Wagner",
            email = "wagner@example.com",
            password = "senha123"
        });

        var response = await _client.PostJsonAsync("/api/auth/login", new
        {
            email = "wagner@example.com",
            password = "senha-errada"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns422()
    {
        var response = await _client.PostJsonAsync("/api/auth/login", new
        {
            email = "naoexiste@example.com",
            password = "qualquercoisa"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Autorização ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/clientes");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_WithCustomerToken_Returns403()
    {
        // Registra como Customer e faz login
        await _client.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Cliente",
            email = "cliente@example.com",
            password = "senha123"
        });

        var loginResp = await _client.PostJsonAsync("/api/auth/login", new
        {
            email = "cliente@example.com",
            password = "senha123"
        });

        var token = (await loginResp.ReadAsAsync<JsonElement>())
            .GetProperty("token").GetString()!;

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Tenta criar cliente (Admin only)
        var response = await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Teste",
            email = "teste@example.com",
            document = "529.982.247-25"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
