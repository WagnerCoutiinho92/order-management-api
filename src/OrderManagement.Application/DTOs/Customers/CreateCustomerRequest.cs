namespace OrderManagement.Application.DTOs.Customers;

public record CreateCustomerRequest(
    string Name,
    string Email,
    string Document
);
