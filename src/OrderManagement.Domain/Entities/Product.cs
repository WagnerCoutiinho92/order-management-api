namespace OrderManagement.Domain.Entities;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core constructor
    private Product() { }

    public Product(string name, string? description, decimal price, int stockQuantity)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePrice(decimal price)
    {
        Price = price;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Debits stock. Throws if insufficient quantity.
    /// </summary>
    public void DebitStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("A quantidade deve ser maior que zero.", nameof(quantity));

        if (StockQuantity < quantity)
            throw new Exceptions.BusinessRuleException(
                "INSUFFICIENT_STOCK",
                $"Estoque insuficiente para o produto '{Name}'. Disponível: {StockQuantity}, Solicitado: {quantity}.");

        StockQuantity -= quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns stock (used on order cancellation).
    /// </summary>
    public void ReturnStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("A quantidade deve ser maior que zero.", nameof(quantity));

        StockQuantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStock(int quantity)
    {
        StockQuantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
