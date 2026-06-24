namespace OrderManagement.Application.DTOs.Orders;

public record CreateOrderRequest(
    Guid CustomerId,
    List<CreateOrderItemRequest> Items
);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity
);
