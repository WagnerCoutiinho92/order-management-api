using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs.Products;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Domain.Interfaces.Repositories;

namespace OrderManagement.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITimezoneConverter _tz;

    public ProductService(IProductRepository productRepository, IUnitOfWork unitOfWork, ITimezoneConverter tz)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _tz = tz;
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = new Product(request.Name, request.Description, request.Price, request.StockQuantity);
        await _productRepository.AddAsync(product, ct);
        await _unitOfWork.CommitAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdAsync(id, ct);
        return product is null ? null : ToResponse(product);
    }

    public async Task<PagedResult<ProductResponse>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var products = await _productRepository.GetAllAsync(page, pageSize, ct);
        var total = await _productRepository.CountAsync(ct);
        return PagedResult<ProductResponse>.Create(products.Select(ToResponse), total, page, pageSize);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Produto", id);
        product.Update(request.Name, request.Description);
        await _unitOfWork.CommitAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdatePriceAsync(Guid id, UpdateProductPriceRequest request, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Produto", id);
        product.UpdatePrice(request.Price);
        await _unitOfWork.CommitAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateStockAsync(Guid id, UpdateProductStockRequest request, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Produto", id);
        product.SetStock(request.StockQuantity);
        await _unitOfWork.CommitAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Produto", id);

        if (request.IsActive) product.Activate();
        else product.Deactivate();

        await _unitOfWork.CommitAsync(ct);
        return ToResponse(product);
    }

    private ProductResponse ToResponse(Product p) => new(
        p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.IsActive,
        _tz.ToSaoPaulo(p.CreatedAt),
        _tz.ToSaoPaulo(p.UpdatedAt));
}
