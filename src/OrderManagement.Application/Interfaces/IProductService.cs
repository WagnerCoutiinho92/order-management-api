using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs.Products;

namespace OrderManagement.Application.Interfaces;

public interface IProductService
{
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ProductResponse>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task<ProductResponse> UpdatePriceAsync(Guid id, UpdateProductPriceRequest request, CancellationToken ct = default);
    Task<ProductResponse> UpdateStockAsync(Guid id, UpdateProductStockRequest request, CancellationToken ct = default);
    Task<ProductResponse> UpdateStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken ct = default);
}
