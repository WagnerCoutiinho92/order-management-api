namespace OrderManagement.Application.DTOs.Products;

public record CreateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity
);
