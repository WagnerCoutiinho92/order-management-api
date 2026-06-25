using System.Net;
using System.Text.Json;
using FluentAssertions;
using OrderManagement.IntegrationTests.Helpers;
using OrderManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderManagement.IntegrationTests.Tests;

public class RateLimitingIntegrationTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly HttpClient _anonClient;
    private readonly HttpClient _authClient;
    private readonly IntegrationTestFixture _fixture;
    private readonly string _testIp = $"10.0.{Random.Shared.Next(0, 255)}.{Random.Shared.Next(1, 254)}";

    public RateLimitingIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;

        _anonClient = fixture.CreateClient();
        _anonClient.DefaultRequestHeaders.Add("X-Forwarded-For", _testIp);

        _authClient = fixture.CreateAuthenticatedClient();
        _authClient.DefaultRequestHeaders.Add("X-Forwarded-For", _testIp);
    }

    public async Task InitializeAsync()
    {
        using var db = _fixture.CreateDbContext();
        await DatabaseCleaner.CleanAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Policy "auth" (limite = 3 nos testes) ────────────────────────────────

    [Fact]
    public async Task Register_ExceedsLimit_Returns429()
    {
        for (int i = 0; i < 3; i++)
        {
            await _anonClient.PostJsonAsync("/api/auth/registrar", new
            {
                name = $"User {i}",
                email = $"ratelimit{i}@example.com",
                password = "senha123"
            });
        }

        var response = await _anonClient.PostJsonAsync("/api/auth/registrar", new
        {
            name = "Bloqueado",
            email = "bloqueado@example.com",
            password = "senha123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var body = await response.ReadAsAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("RATE_LIMIT_EXCEEDED");
        body.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_ExceedsLimit_Returns429()
    {
        for (int i = 0; i < 3; i++)
        {
            await _anonClient.PostJsonAsync("/api/auth/login", new
            {
                email = "naoexiste@example.com",
                password = "qualquer"
            });
        }

        var response = await _anonClient.PostJsonAsync("/api/auth/login", new
        {
            email = "naoexiste@example.com",
            password = "qualquer"
        });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ── Policy "create" (limite = 3 nos testes) ───────────────────────────────

    [Fact]
    public async Task CreateCustomer_ExceedsLimit_Returns429()
    {
        var documents = new[] { "529.982.247-25", "111.444.777-35", "803.604.688-04" };

        for (int i = 0; i < 3; i++)
        {
            await _authClient.PostJsonAsync("/api/clientes", new
            {
                name = $"Cliente {i}",
                email = $"cliente{i}@example.com",
                document = documents[i]
            });
        }

        var response = await _authClient.PostJsonAsync("/api/clientes", new
        {
            name = "Bloqueado",
            email = "bloqueado@example.com",
            document = "111.444.777-35"
        });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task CreateProduct_ExceedsLimit_Returns429()
    {
        for (int i = 0; i < 3; i++)
        {
            await _authClient.PostJsonAsync("/api/produtos", new
            {
                name = $"Produto {i}",
                price = 10.0,
                stockQuantity = 5
            });
        }

        var response = await _authClient.PostJsonAsync("/api/produtos", new
        {
            name = "Bloqueado",
            price = 10.0,
            stockQuantity = 5
        });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task CreateOrder_ExceedsLimit_Returns429()
    {
        // Cria produto e cliente para usar nos pedidos
        var product = await (await _authClient.PostJsonAsync("/api/produtos", new
        {
            name = "Produto Rate Limit",
            price = 10.0,
            stockQuantity = 100
        })).ReadAsAsync<JsonElement>();

        var customer = await (await _authClient.PostJsonAsync("/api/clientes", new
        {
            name = "Cliente Rate Limit",
            email = "rl-order@example.com",
            document = "529.982.247-25"
        })).ReadAsAsync<JsonElement>();

        var productId = product.GetProperty("id").GetString();
        var customerId = customer.GetProperty("id").GetString();

        // Consome o limite de create (já usamos 2 POSTs acima, restam 1)
        // Recria clientes com limite próprio — o mais seguro é usar um cliente anônimo
        // para criar pedidos com um terceiro HttpClient autenticado e limite zerado
        // Para isolar, usamos CreateAuthenticatedClient() para este teste
        var client = _fixture.CreateAuthenticatedClient();

        for (int i = 0; i < 3; i++)
        {
            await client.PostJsonAsync("/api/pedidos", new
            {
                customerId,
                items = new[] { new { productId, quantity = 1 } }
            });
        }

        var response = await client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ── GET não é limitado ────────────────────────────────────────────────────

    [Fact]
    public async Task GetEndpoints_NeverRateLimited()
    {
        for (int i = 0; i < 10; i++)
        {
            var response = await _authClient.GetAsync("/api/clientes");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }
}
