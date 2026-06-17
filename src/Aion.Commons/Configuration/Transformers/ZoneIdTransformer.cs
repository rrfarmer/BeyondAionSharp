using System;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/ZoneIdTransformer (Neon). Java java.time.ZoneId → System.TimeZoneInfo. An empty value
/// resolves to the system default time zone (Java ZoneId.systemDefault() → TimeZoneInfo.Local); otherwise the IANA
/// zone id (e.g. "Europe/Paris") is resolved via TimeZoneInfo.FindSystemTimeZoneById (which on .NET 6+ accepts IANA
/// ids on every platform, including Windows, exactly mirroring Java ZoneId.of(value)).
/// </summary>
public sealed class ZoneIdTransformer : PropertyTransformer
{
    public override bool Matches(Type targetType) => targetType == typeof(TimeZoneInfo);

    protected override object? ParseObject(string value, Type type)
        => value.Length == 0 ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(value);
}
