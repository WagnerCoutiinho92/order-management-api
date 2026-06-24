using OrderManagement.Application.Interfaces;

namespace OrderManagement.Infrastructure.Services;

public class TimezoneConverter : ITimezoneConverter
{
    private static readonly TimeZoneInfo SaoPauloTz = GetSaoPauloTimeZone();

    public DateTimeOffset ToSaoPaulo(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, SaoPauloTz);
    }

    public DateTime ToUtc(DateTimeOffset saoPauloOffset) =>
        saoPauloOffset.UtcDateTime;

    private static TimeZoneInfo GetSaoPauloTimeZone()
    {
        // Linux/macOS uses IANA IDs; Windows uses its own timezone IDs
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
