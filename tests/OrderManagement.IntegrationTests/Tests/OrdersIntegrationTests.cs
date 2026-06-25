using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderManagement.IntegrationTests.Helpers;
using OrderManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderManagement.IntegrationTests.Tests;

[Collection("Integration")]
public class OrdersIntegrationTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFixture _fixture;

    public OrdersIntegrationTests(IntegrationTestFixture fixture)
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateActiveCustomerAsync()
    {
        var resp = await _client.PostJsonAsync("/api/clientes", new
        {
            name = "Cliente Teste",
            email = $"cliente_{Guid.NewGuid():N}@test.com",
            document = "529.982.247-25"
        });
        var body = await resp.ReadAsAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<(Guid Id, decimal Price)> CreateActiveProductAsync(int stock = 10, decimal price = 50m)
    {
        var resp = await _client.PostJsonAsync("/api/produtos", new
        {
            name = $"Produto {Guid.NewGuid():N}",
            description = (string?)null,
            price,
            stockQuantity = stock
        });
        var body = await resp.ReadAsAsync<JsonElement>();
        return (Guid.Parse(body.GetProperty("id").GetString()!), price);
    }

    // ── Criação ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_Valid_Returns201AndDebitsStock()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, price) = await CreateActiveProductAsync(stock: 5, price: 99.90m);

        var response = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 2 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsAsync<JsonElement>();
        body.GetProperty("totalValue").GetDecimal().Should().Be(199.80m);
        body.GetProperty("status").GetString().Should().Be("Created");

        // Assert stock was debited in the real DB
        using var db = _fixture.CreateDbContext();
        var product = await db.Products.FindAsync(productId);
        product!.StockQuantity.Should().Be(3);
    }

    [Fact]
    public async Task CreateOrder_PriceSnapshotKeptAfterPriceChange()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, originalPrice) = await CreateActiveProductAsync(stock: 5, price: 100m);

        var orderResp = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        });

        // Change product price after order creation
        await _client.PatchJsonAsync($"/api/produtos/{productId}/preco", new { price = 999m });

        var order = await orderResp.ReadAsAsync<JsonElement>();
        var itemPrice = order.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal();

        itemPrice.Should().Be(100m); // original price preserved
    }

    [Fact]
    public async Task CreateOrder_InactiveCustomer_Returns422()
    {
        var customerId = await CreateActiveCustomerAsync();
        await _client.PatchJsonAsync($"/api/clientes/{customerId}/status", new { isActive = false });

        var (productId, _) = await CreateActiveProductAsync();

        var response = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_Returns422()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, _) = await CreateActiveProductAsync(stock: 2);

        var response = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 5 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateOrder_InactiveProduct_Returns422()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, _) = await CreateActiveProductAsync();
        await _client.PatchJsonAsync($"/api/produtos/{productId}/status", new { isActive = false });

        var response = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Status ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_CreatedToPaid_Returns200()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, _) = await CreateActiveProductAsync();

        var orderResp = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        });
        var order = await orderResp.ReadAsAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetString();

        var response = await _client.PatchJsonAsync($"/api/pedidos/{orderId}/status",
            new { status = "Paid", reason = "Pagamento confirmado" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadAsAsync<JsonElement>();
        updated.GetProperty("status").GetString().Should().Be("Paid");
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Returns422()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, _) = await CreateActiveProductAsync();

        var orderResp = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        });
        var order = await orderResp.ReadAsAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetString();

        // Created → Shipped is invalid
        var response = await _client.PatchJsonAsync($"/api/pedidos/{orderId}/status",
            new { status = "Shipped" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateStatus_SameStatus_IsIdempotentReturns200()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, _) = await CreateActiveProductAsync();

        var orderResp = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        });
        var order = await orderResp.ReadAsAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetString();

        // Send same status (Created → Created)
        var response = await _client.PatchJsonAsync($"/api/pedidos/{orderId}/status",
            new { status = "Created" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelOrder_BeforeShipment_ReturnsStockToDatabase()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, _) = await CreateActiveProductAsync(stock: 10);

        var orderResp = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 3 } }
        });
        var order = await orderResp.ReadAsAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetString();

        await _client.PatchJsonAsync($"/api/pedidos/{orderId}/status",
            new { status = "Cancelled", reason = "Cliente desistiu" });

        // Assert stock returned in real DB
        using var db = _fixture.CreateDbContext();
        var product = await db.Products.FindAsync(productId);
        product!.StockQuantity.Should().Be(10); // fully restored
    }

    [Fact]
    public async Task GetOrderById_IncludesItemsAndStatusHistory()
    {
        var customerId = await CreateActiveCustomerAsync();
        var (productId, _) = await CreateActiveProductAsync();

        var orderResp = await _client.PostJsonAsync("/api/pedidos", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        });
        var order = await orderResp.ReadAsAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/pedidos/{orderId}");
        var body = await response.ReadAsAsync<JsonElement>();

        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("statusHistory").GetArrayLength().Should().BeGreaterThan(0);
    }
}
