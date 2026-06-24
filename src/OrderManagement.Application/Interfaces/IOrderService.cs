using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs.Orders;

namespace OrderManagement.Application.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<OrderResponse>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<OrderResponse> UpdateStatusAsync(Guid id, UpdateOrderStatusRequest request, CancellationToken ct = default);
}
