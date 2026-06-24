namespace OrderManagement.Domain.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist (maps to 404 Not Found).
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string resource, object id)
        : base($"{resource} com id '{id}' não foi encontrado.") { }

    public NotFoundException(string message) : base(message) { }
}
