using FluentAssertions;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using Xunit;

namespace OrderManagement.Tests.Domain;

public class ProductStockTests
{
    private static Product CreateProduct(int stock = 10) =>
        new("Produto Teste", null, 9.99m, stock);

    [Fact]
    public void DebitStock_WithSufficientStock_ShouldDecrement()
    {
        var product = CreateProduct(10);
        product.DebitStock(3);
        product.StockQuantity.Should().Be(7);
    }

    [Fact]
    public void DebitStock_ExactQuantity_ShouldReachZero()
    {
        var product = CreateProduct(5);
        product.DebitStock(5);
        product.StockQuantity.Should().Be(0);
    }

    [Fact]
    public void DebitStock_InsufficientStock_ShouldThrow()
    {
        var product = CreateProduct(3);
        var act = () => product.DebitStock(5);
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Estoque insuficiente*");
    }

    [Fact]
    public void DebitStock_ZeroQuantity_ShouldThrow()
    {
        var product = CreateProduct(10);
        var act = () => product.DebitStock(0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReturnStock_ShouldIncrement()
    {
        var product = CreateProduct(5);
        product.ReturnStock(3);
        product.StockQuantity.Should().Be(8);
    }

    [Fact]
    public void SetStock_ShouldReplaceValue()
    {
        var product = CreateProduct(10);
        product.SetStock(0);
        product.StockQuantity.Should().Be(0);
    }

    [Fact]
    public void UpdatePrice_ShouldChangePrice()
    {
        var product = CreateProduct();
        product.UpdatePrice(19.99m);
        product.Price.Should().Be(19.99m);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var product = CreateProduct();
        product.Deactivate();
        product.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveTrue()
    {
        var product = CreateProduct();
        product.Deactivate();
        product.Activate();
        product.IsActive.Should().BeTrue();
    }
}
