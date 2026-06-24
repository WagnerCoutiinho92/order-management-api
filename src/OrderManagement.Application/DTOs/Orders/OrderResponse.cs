using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.DTOs.Orders;

public record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    OrderStatus Status,
    string StatusName,
    decimal TotalValue,
    DateTimeOffset CreatedAt,
    List<OrderItemResponse> Items,
    List<OrderStatusHistoryResponse> StatusHistory
);

public record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalValue
);

public record OrderStatusHistoryResponse(
    Guid Id,
    string? PreviousStatus,
    string NewStatus,
    DateTimeOffset ChangedAt,
    string? Reason
);
