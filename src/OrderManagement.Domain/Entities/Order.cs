using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Exceptions;

namespace OrderManagement.Domain.Entities;

public class Order
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalValue { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public Customer Customer { get; private set; } = null!;
    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private readonly List<OrderStatusHistory> _statusHistory = [];
    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    // EF Core constructor
    private Order() { }

    public Order(Guid customerId, List<OrderItem> items)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = OrderStatus.Created;
        CreatedAt = DateTime.UtcNow;

        foreach (var item in items)
            _items.Add(item);

        CalculateTotal();

        // Record initial status
        _statusHistory.Add(new OrderStatusHistory(Id, null, OrderStatus.Created));
    }

    private static readonly Dictionary<OrderStatus, IReadOnlySet<OrderStatus>> AllowedTransitions = new()
    {
        [OrderStatus.Created]   = new HashSet<OrderStatus> { OrderStatus.Paid, OrderStatus.Cancelled },
        [OrderStatus.Paid]      = new HashSet<OrderStatus> { OrderStatus.Shipped },
        [OrderStatus.Shipped]   = new HashSet<OrderStatus>(),
        [OrderStatus.Cancelled] = new HashSet<OrderStatus>()
    };

    /// <summary>
    /// Attempts to transition the order to <paramref name="newStatus"/>.
    /// Returns true if a transition occurred, false if newStatus == current status (idempotent).
    /// Throws <see cref="BusinessRuleException"/> for invalid transitions.
    /// </summary>
    public bool TransitionTo(OrderStatus newStatus, string? reason = null)
    {
        if (newStatus == Status)
            return false; // Idempotent — no error, no history record

        if (!AllowedTransitions[Status].Contains(newStatus))
            throw new BusinessRuleException(
                "INVALID_STATUS_TRANSITION",
                $"Transição de status inválida: '{Status}' → '{newStatus}'.");

        var previousStatus = Status;
        Status = newStatus;

        _statusHistory.Add(new OrderStatusHistory(Id, previousStatus, newStatus, reason));
        return true;
    }

    public bool CanReturnStock() => Status != OrderStatus.Shipped;

    private void CalculateTotal()
    {
        TotalValue = Math.Round(_items.Sum(i => i.TotalValue), 2, MidpointRounding.AwayFromZero);
    }
}
