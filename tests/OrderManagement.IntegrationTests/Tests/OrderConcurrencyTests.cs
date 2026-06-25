using System.Net;
using System.Text.Json;
using FluentAssertions;
using OrderManagement.IntegrationTests.Helpers;
using OrderManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderManagement.IntegrationTests.Tests;

[Collection("Integration")]
public class OrderConcurrencyTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public OrderConcurrencyTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        using var db = _fixture.CreateDbContext();
        await DatabaseCleaner.CleanAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Sends 2 concurrent orders for the same product with stock = 1, qty = 1 each.
    /// Because GetByIdsForUpdateAsync uses UPDLOCK inside a transaction, only one
    /// request can hold the lock at a time — the second reads stock = 0 and fails.
    /// Expected: exactly 1 success (201) and 1 failure (422 INSUFFICIENT_STOCK).
    /// </summary>
    [Fact]
    public async Task SimultaneousOrders_SameProduct_OnlyOneSucceeds()
    {
        // Arrange: product with stock = 1
        var setupClient = _fixture.CreateClient();

        var customerResp = await setupClient.PostJsonAsync("/api/clientes", new
        {
            name = "Concorrência Cliente",
            email = $"conc_{Guid.NewGuid():N}@test.com",
            document = "529.982.247-25"
        });
        var customer = await customerResp.ReadAsAsync<JsonElement>();
        var customerId = customer.GetProperty("id").GetString();

        var productResp = await setupClient.PostJsonAsync("/api/produtos", new
        {
            name = "Produto Disputado",
            description = (string?)null,
            price = 100m,
            stockQuantity = 1
        });
        var product = await productResp.ReadAsAsync<JsonElement>();
        var productId = product.GetProperty("id").GetString();

        var orderPayload = new
        {
            customerId = Guid.Parse(customerId!),
            items = new[] { new { productId = Guid.Parse(productId!), quantity = 1 } }
        };

        // Act: fire 2 requests simultaneously using separate HttpClients
        var client1 = _fixture.CreateClient();
        var client2 = _fixture.CreateClient();

        var responses = await Task.WhenAll(
            client1.PostJsonAsync("/api/pedidos", orderPayload),
            client2.PostJsonAsync("/api/pedidos", orderPayload));

        // Assert: exactly 1 created and 1 rejected
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        statusCodes.Should().Contain(HttpStatusCode.Created);
        statusCodes.Should().Contain(HttpStatusCode.UnprocessableEntity);

        // Assert stock is 0 in the real DB (not negative)
        using var db = _fixture.CreateDbContext();
        var dbProduct = await db.Products.FindAsync(Guid.Parse(productId!));
        dbProduct!.StockQuantity.Should().Be(0);
    }

    /// <summary>
    /// 5 concurrent orders for the same product with stock = 3, qty = 1 each.
    /// Expected: exactly 3 successes and 2 failures. Stock ends at 0.
    /// </summary>
    [Fact]
    public async Task FiveConcurrentOrders_Stock3_ExactlyThreeSucceed()
    {
        var setupClient = _fixture.CreateClient();

        var customerResp = await setupClient.PostJsonAsync("/api/clientes", new
        {
            name = "Multi Concorrência",
            email = $"multi_{Guid.NewGuid():N}@test.com",
            document = "529.982.247-25"
        });
        var customer = await customerResp.ReadAsAsync<JsonElement>();
        var customerId = Guid.Parse(customer.GetProperty("id").GetString()!);

        var productResp = await setupClient.PostJsonAsync("/api/produtos", new
        {
            name = "Produto Limitado",
            description = (string?)null,
            price = 50m,
            stockQuantity = 3
        });
        var product = await productResp.ReadAsAsync<JsonElement>();
        var productId = Guid.Parse(product.GetProperty("id").GetString()!);

        var payload = new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } }
        };

        // Fire 5 concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _fixture.CreateClient().PostJsonAsync("/api/pedidos", payload));

        var responses = await Task.WhenAll(tasks);

        var successes = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var failures = responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);

        successes.Should().Be(3);
        failures.Should().Be(2);

        using var db = _fixture.CreateDbContext();
        var dbProduct = await db.Products.FindAsync(productId);
        dbProduct!.StockQuantity.Should().Be(0);
    }
}
