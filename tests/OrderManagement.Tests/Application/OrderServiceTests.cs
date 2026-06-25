using FluentAssertions;
using Moq;
using OrderManagement.Application.DTOs.Orders;
using OrderManagement.Application.Interfaces;
using OrderManagement.Application.Services;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Domain.Interfaces.Repositories;
using Xunit;

namespace OrderManagement.Tests.Application;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<ICustomerRepository> _customerRepoMock = new();
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITimezoneConverter> _tzMock = new();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _tzMock.Setup(t => t.ToSaoPaulo(It.IsAny<DateTime>()))
            .Returns((DateTime d) => new DateTimeOffset(d.Ticks, TimeSpan.FromHours(-3)));

        _uowMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // ExecuteInTransactionAsync must actually invoke the action so business rules inside run.
        _uowMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((action, _) => action());

        _sut = new OrderService(
            _orderRepoMock.Object,
            _customerRepoMock.Object,
            _productRepoMock.Object,
            _uowMock.Object,
            _tzMock.Object);
    }

    private static Customer CreateActiveCustomer() =>
        new("João", "joao@email.com", "52998224725");

    private static Product CreateActiveProduct(int stock = 10, decimal price = 100m) =>
        new("Produto", null, price, stock);

    [Fact]
    public async Task Create_ValidOrder_ReturnsOrderResponse()
    {
        var customer = CreateActiveCustomer();
        var product = CreateActiveProduct(stock: 5, price: 49.90m);

        _customerRepoMock.Setup(r => r.GetByIdAsync(customer.Id, default)).ReturnsAsync(customer);
        _productRepoMock.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync([product]);
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), default)).Returns(Task.CompletedTask);

        var request = new CreateOrderRequest(customer.Id, [new CreateOrderItemRequest(product.Id, 2)]);
        var result = await _sut.CreateAsync(request);

        result.Should().NotBeNull();
        result.TotalValue.Should().Be(99.80m);
        result.Items.Should().HaveCount(1);
        result.Items[0].UnitPrice.Should().Be(49.90m);
    }

    [Fact]
    public async Task Create_InactiveCustomer_ThrowsBusinessRuleException()
    {
        var customer = CreateActiveCustomer();
        customer.Deactivate();

        _customerRepoMock.Setup(r => r.GetByIdAsync(customer.Id, default)).ReturnsAsync(customer);

        var request = new CreateOrderRequest(customer.Id, [new CreateOrderItemRequest(Guid.NewGuid(), 1)]);
        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*inativos*");
    }

    [Fact]
    public async Task Create_CustomerNotFound_ThrowsNotFoundException()
    {
        _customerRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Customer?)null);

        var request = new CreateOrderRequest(Guid.NewGuid(), [new CreateOrderItemRequest(Guid.NewGuid(), 1)]);
        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_InactiveProduct_ThrowsBusinessRuleException()
    {
        var customer = CreateActiveCustomer();
        var product = CreateActiveProduct();
        product.Deactivate();

        _customerRepoMock.Setup(r => r.GetByIdAsync(customer.Id, default)).ReturnsAsync(customer);
        _productRepoMock.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync([product]);

        var request = new CreateOrderRequest(customer.Id, [new CreateOrderItemRequest(product.Id, 1)]);
        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*inativo*");
    }

    [Fact]
    public async Task Create_InsufficientStock_ThrowsBusinessRuleException()
    {
        var customer = CreateActiveCustomer();
        var product = CreateActiveProduct(stock: 2);

        _customerRepoMock.Setup(r => r.GetByIdAsync(customer.Id, default)).ReturnsAsync(customer);
        _productRepoMock.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync([product]);

        var request = new CreateOrderRequest(customer.Id, [new CreateOrderItemRequest(product.Id, 5)]);
        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Estoque insuficiente*");
    }

    [Fact]
    public async Task Create_ShouldDebitStock()
    {
        var customer = CreateActiveCustomer();
        var product = CreateActiveProduct(stock: 10);

        _customerRepoMock.Setup(r => r.GetByIdAsync(customer.Id, default)).ReturnsAsync(customer);
        _productRepoMock.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync([product]);
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), default)).Returns(Task.CompletedTask);

        await _sut.CreateAsync(new CreateOrderRequest(customer.Id, [new CreateOrderItemRequest(product.Id, 3)]));

        product.StockQuantity.Should().Be(7);
    }

    [Fact]
    public async Task Create_PriceSnapshot_ShouldCapturePriceAtCreationTime()
    {
        var customer = CreateActiveCustomer();
        var product = CreateActiveProduct(price: 99.99m);

        _customerRepoMock.Setup(r => r.GetByIdAsync(customer.Id, default)).ReturnsAsync(customer);
        _productRepoMock.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync([product]);
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(
            new CreateOrderRequest(customer.Id, [new CreateOrderItemRequest(product.Id, 1)]));

        // Simulate price change after creation
        product.UpdatePrice(199.99m);

        result.Items[0].UnitPrice.Should().Be(99.99m);
    }

    [Fact]
    public async Task UpdateStatus_ValidTransition_ShouldSucceed()
    {
        var order = new Order(Guid.NewGuid(), [new OrderItem(Guid.NewGuid(), Guid.NewGuid(), 1, 10m)]);
        // Write path uses GetByIdForUpdateAsync; response reload uses GetByIdAsync.
        _orderRepoMock.Setup(r => r.GetByIdForUpdateAsync(order.Id, default)).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatus.Paid));

        result.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task UpdateStatus_SameStatus_IsIdempotentAndReturns200()
    {
        var order = new Order(Guid.NewGuid(), [new OrderItem(Guid.NewGuid(), Guid.NewGuid(), 1, 10m)]);
        _orderRepoMock.Setup(r => r.GetByIdForUpdateAsync(order.Id, default)).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        // Request Created status when already Created — should not throw
        var result = await _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatus.Created));

        result.Should().NotBeNull();
        result.Status.Should().Be(OrderStatus.Created);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_ThrowsBusinessRuleException()
    {
        var order = new Order(Guid.NewGuid(), [new OrderItem(Guid.NewGuid(), Guid.NewGuid(), 1, 10m)]);
        _orderRepoMock.Setup(r => r.GetByIdForUpdateAsync(order.Id, default)).ReturnsAsync(order);

        var act = async () => await _sut.UpdateStatusAsync(
            order.Id, new UpdateOrderStatusRequest(OrderStatus.Shipped)); // Created → Shipped is invalid

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Cancel_BeforeShipment_ShouldReturnStock()
    {
        var product = CreateActiveProduct(stock: 5);
        var orderItem = new OrderItem(Guid.NewGuid(), product.Id, 3, 10m);
        var order = new Order(Guid.NewGuid(), [orderItem]);

        _orderRepoMock.Setup(r => r.GetByIdForUpdateAsync(order.Id, default)).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);
        _productRepoMock.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync([product]);

        await _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatus.Cancelled));

        product.StockQuantity.Should().Be(8); // 5 + 3 returned
    }
}
