namespace OrderManagement.Application.DTOs.Auth;

public record AuthResponse(string Token, string Name, string Email, string Role, DateTime ExpiresAt);
