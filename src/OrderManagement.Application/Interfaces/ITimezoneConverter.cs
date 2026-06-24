namespace OrderManagement.Application.Interfaces;

/// <summary>
/// Converts UTC <see cref="DateTime"/> values to/from America/Sao_Paulo.
/// </summary>
public interface ITimezoneConverter
{
    DateTimeOffset ToSaoPaulo(DateTime utcDateTime);
    DateTime ToUtc(DateTimeOffset saoPauloOffset);
}
