using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.DTOs.Orders;

public record UpdateOrderStatusRequest(OrderStatus Status, string? Reason = null);
