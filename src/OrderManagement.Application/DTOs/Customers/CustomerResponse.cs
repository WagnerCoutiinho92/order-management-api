namespace OrderManagement.Application.DTOs.Customers;

public record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string Document,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
