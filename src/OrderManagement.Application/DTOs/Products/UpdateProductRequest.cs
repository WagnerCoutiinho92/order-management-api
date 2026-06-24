namespace OrderManagement.Application.DTOs.Products;

public record UpdateProductRequest(string Name, string? Description);
public record UpdateProductPriceRequest(decimal Price);
public record UpdateProductStockRequest(int StockQuantity);
public record UpdateProductStatusRequest(bool IsActive);
