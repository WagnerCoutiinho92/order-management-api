using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs.Customers;

namespace OrderManagement.Application.Interfaces;

public interface ICustomerService
{
    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CustomerResponse>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CustomerResponse> UpdateStatusAsync(Guid id, UpdateCustomerStatusRequest request, CancellationToken ct = default);
}
