using OrderManagement.Domain.Enums;

namespace OrderManagement.Domain.Entities;

public class OrderStatusHistory
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public OrderStatus? PreviousStatus { get; private set; }
    public OrderStatus NewStatus { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string? Reason { get; private set; }

    // Navigation
    public Order Order { get; private set; } = null!;

    // EF Core constructor
    private OrderStatusHistory() { }

    public OrderStatusHistory(Guid orderId, OrderStatus? previousStatus, OrderStatus newStatus, string? reason = null)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedAt = DateTime.UtcNow;
        Reason = reason;
    }
}
