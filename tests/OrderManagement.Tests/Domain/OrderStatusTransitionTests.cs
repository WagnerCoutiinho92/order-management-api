using FluentAssertions;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Exceptions;
using Xunit;

namespace OrderManagement.Tests.Domain;

public class OrderStatusTransitionTests
{
    private static Order CreateOrder()
    {
        var item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), 2, 50m);
        return new Order(Guid.NewGuid(), [item]);
    }

    [Fact]
    public void NewOrder_ShouldHaveStatusCreated()
    {
        var order = CreateOrder();
        order.Status.Should().Be(OrderStatus.Created);
    }

    [Fact]
    public void NewOrder_ShouldHaveOneHistoryRecord()
    {
        var order = CreateOrder();
        order.StatusHistory.Should().HaveCount(1);
        order.StatusHistory.First().PreviousStatus.Should().BeNull();
        order.StatusHistory.First().NewStatus.Should().Be(OrderStatus.Created);
    }

    // ── Transições válidas ─────────────────────────────────────────────────
    [Fact]
    public void Transition_CreatedToPaid_ShouldSucceed()
    {
        var order = CreateOrder();
        var changed = order.TransitionTo(OrderStatus.Paid);
        changed.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
        order.StatusHistory.Should().HaveCount(2);
    }

    [Fact]
    public void Transition_PaidToShipped_ShouldSucceed()
    {
        var order = CreateOrder();
        order.TransitionTo(OrderStatus.Paid);
        var changed = order.TransitionTo(OrderStatus.Shipped);
        changed.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void Transition_CreatedToCancelled_ShouldSucceed()
    {
        var order = CreateOrder();
        var changed = order.TransitionTo(OrderStatus.Cancelled);
        changed.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    // ── Transições inválidas ──────────────────────────────────────────────
    // Valid from Paid: only Shipped. Created and Cancelled are invalid.
    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.Cancelled)]
    public void Transition_PaidToInvalidStatus_ShouldThrow(OrderStatus target)
    {
        var order = CreateOrder();
        order.TransitionTo(OrderStatus.Paid);
        var act = () => order.TransitionTo(target);
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Transição de status inválida*");
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Shipped)]
    public void Transition_FromCancelled_ShouldThrow(OrderStatus target)
    {
        var order = CreateOrder();
        order.TransitionTo(OrderStatus.Cancelled);
        var act = () => order.TransitionTo(target);
        act.Should().Throw<BusinessRuleException>();
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.Cancelled)]
    public void Transition_FromShipped_ShouldThrow(OrderStatus target)
    {
        var order = CreateOrder();
        order.TransitionTo(OrderStatus.Paid);
        order.TransitionTo(OrderStatus.Shipped);
        var act = () => order.TransitionTo(target);
        act.Should().Throw<BusinessRuleException>();
    }

    // ── Comportamento idempotente ─────────────────────────────────────────
    [Fact]
    public void Transition_SameStatus_ShouldReturnFalseAndNotAddHistory()
    {
        var order = CreateOrder();
        var changed = order.TransitionTo(OrderStatus.Created);
        changed.Should().BeFalse();
        order.StatusHistory.Should().HaveCount(1); // only initial
    }

    // ── Retorno de estoque ────────────────────────────────────────────────
    [Fact]
    public void CanReturnStock_WhenCreated_ShouldBeTrue()
        => CreateOrder().CanReturnStock().Should().BeTrue();

    [Fact]
    public void CanReturnStock_WhenShipped_ShouldBeFalse()
    {
        var order = CreateOrder();
        order.TransitionTo(OrderStatus.Paid);
        order.TransitionTo(OrderStatus.Shipped);
        order.CanReturnStock().Should().BeFalse();
    }

    // ── Cálculo de total ──────────────────────────────────────────────────
    [Fact]
    public void TotalValue_ShouldBeSumOfItems()
    {
        var item1 = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), 2, 10.00m);  // 20.00
        var item2 = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), 3, 5.50m);   // 16.50
        var order = new Order(Guid.NewGuid(), [item1, item2]);
        order.TotalValue.Should().Be(36.50m);
    }

    [Fact]
    public void TotalValue_ShouldRoundAwayFromHalf()
    {
        var item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), 3, 0.005m); // 0.015 → 0.02
        var order = new Order(Guid.NewGuid(), [item]);
        order.TotalValue.Should().Be(0.02m);
    }
}
