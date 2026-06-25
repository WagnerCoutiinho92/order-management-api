using System.Net;
using System.Text.Json;
using FluentAssertions;
using OrderManagement.IntegrationTests.Helpers;
using OrderManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderManagement.IntegrationTests.Tests;

[Collection("Integration")]
public class CustomersIntegrationTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFixture _fixture;

    public CustomersIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        using var db = _fixture.CreateDbContext();
        await DatabaseCleaner.CleanAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Criação ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCustomer_ValidData_Returns201WithBody()
    {
        var response = await _client.PostJsonAsync("/api/clientes", new
        {
            name = "João Silva",
            email = "joao@example.com",
            document = "529.982.247-25"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("João Silva");
        body.GetProperty("email").GetString().Should().Be("joao@example.com");
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CreateCustomer_InvalidEmail_Returns400()
    {
        var response = await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Teste",
            email = "not-an-email",
            document = "529.982.247-25"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCustomer_InvalidCpf_Returns400()
    {
        var response = await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Teste",
            email = "teste@example.com",
            document = "111.111.111-11"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCustomer_DuplicateEmail_Returns422()
    {
        await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Primeiro",
            email = "dup@example.com",
            document = "529.982.247-25"
        });

        var response = await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Segundo",
            email = "dup@example.com",
            document = "111.444.777-35"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateCustomer_DuplicateDocument_Returns422()
    {
        await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Primeiro",
            email = "first@example.com",
            document = "529.982.247-25"
        });

        var response = await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Segundo",
            email = "second@example.com",
            document = "529.982.247-25"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Consulta ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomerById_Existing_Returns200()
    {
        var created = await (await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Maria",
            email = "maria@example.com",
            document = "529.982.247-25"
        })).ReadAsAsync<JsonElement>();

        var id = created.GetProperty("id").GetString();
        var response = await _client.GetAsync($"/api/clientes/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCustomerById_NonExisting_Returns404()
    {
        var response = await _client.GetAsync($"/api/clientes/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Status ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateCustomer_Returns200WithIsActiveFalse()
    {
        var created = await (await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Ana",
            email = "ana@example.com",
            document = "529.982.247-25"
        })).ReadAsAsync<JsonElement>();

        var id = created.GetProperty("id").GetString();
        var response = await _client.PatchJsonAsync($"/api/clientes/{id}/status", new { isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadAsAsync<JsonElement>();
        body.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }
}
