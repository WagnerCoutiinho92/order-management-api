namespace OrderManagement.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    /// <summary>Price snapshot at the time of order creation.</summary>
    public decimal UnitPrice { get; private set; }

    public decimal TotalValue => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);

    // Navigation properties
    public Order Order { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    // EF Core constructor
    private OrderItem() { }

    public OrderItem(Guid orderId, Guid productId, int quantity, decimal unitPrice)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
