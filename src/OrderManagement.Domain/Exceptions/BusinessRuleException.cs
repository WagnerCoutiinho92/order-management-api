namespace OrderManagement.Domain.Exceptions;

/// <summary>
/// Thrown when a business rule is violated (maps to 422 Unprocessable Entity).
/// </summary>
public class BusinessRuleException : DomainException
{
    public string Code { get; }

    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }
}
