namespace Aion.GameServer.Utils.Time;

/// <summary>
/// Static server-time utilities — actual wall-clock time in the configured server
/// time zone, NOT in-game time.  All methods return <see cref="DateTimeOffset"/>
/// with the correct zone offset (equivalent to Java's ZonedDateTime).
///
/// Java parity: utils/time/ServerTime.
/// GSConfig.TIME_ZONE_ID is a ZoneId set at server boot; here the equivalent is
/// a TimeZoneInfo stored in _timeZone and set via Initialize() during server startup.
/// </summary>
public static class ServerTime
{
    // Java parity: GSConfig.TIME_ZONE_ID — set once at server bootstrap.
    // Default: TimeZoneInfo.Local (matches ParseTimeZoneId fallback in GameServerOptions).
    private static TimeZoneInfo _timeZone = TimeZoneInfo.Local;

    /// <summary>
    /// Called during server startup with the configured time-zone.
    /// Java parity: GSConfig.TIME_ZONE_ID is loaded from config before any service uses it.
    /// </summary>
    public static void Initialize(TimeZoneInfo timeZone) =>
        _timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));

    /// <summary>
    /// Java parity: ServerTime.now() → ZonedDateTime.now(GSConfig.TIME_ZONE_ID)
    /// </summary>
    public static DateTimeOffset Now() =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone);

    /// <summary>
    /// Java parity: ServerTime.of(LocalDateTime) → ZonedDateTime.of(localDateTime, GSConfig.TIME_ZONE_ID)
    /// </summary>
    public static DateTimeOffset Of(DateTime localDateTime) =>
        new(localDateTime, _timeZone.GetUtcOffset(localDateTime));

    /// <summary>
    /// Java parity: ServerTime.atDate(Date) → ZonedDateTime.ofInstant(date.toInstant(), GSConfig.TIME_ZONE_ID)
    /// </summary>
    public static DateTimeOffset AtDate(DateTimeOffset date) =>
        TimeZoneInfo.ConvertTime(date, _timeZone);

    /// <summary>
    /// Java parity: ServerTime.ofEpochMilli(long) → ZonedDateTime.ofInstant(Instant.ofEpochMilli(...), tz)
    /// </summary>
    public static DateTimeOffset OfEpochMilli(long epochMilli) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(epochMilli), _timeZone);

    /// <summary>
    /// Java parity: ServerTime.ofEpochSecond(long) → ZonedDateTime.ofInstant(Instant.ofEpochSecond(...), tz)
    /// </summary>
    public static DateTimeOffset OfEpochSecond(long epochSecond) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(epochSecond), _timeZone);

    /// <summary>
    /// Java parity: ServerTime.parse(CharSequence) → ZonedDateTime.parse(text).withZoneSameInstant(tz)
    /// </summary>
    public static DateTimeOffset Parse(string text) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.Parse(text), _timeZone);

    /// <summary>
    /// Java parity: ServerTime.parseLocal(CharSequence) → of(LocalDateTime.parse(text))
    /// </summary>
    public static DateTimeOffset ParseLocal(string text) =>
        Of(DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Java parity: ServerTime.getDaylightSavings() — DST offset in seconds at the current instant.
    /// Java: GSConfig.TIME_ZONE_ID.getRules().getDaylightSavings(Instant.now()).getSeconds()
    /// C# equivalent: (total offset) − (standard offset)
    /// </summary>
    public static int GetDaylightSavings()
    {
        var now = DateTime.UtcNow;
        var totalOffset = _timeZone.GetUtcOffset(now);
        var standardOffset = _timeZone.BaseUtcOffset;
        return (int)(totalOffset - standardOffset).TotalSeconds;
    }

    /// <summary>
    /// Java parity: ServerTime.getOffset() — total UTC offset in seconds at current instant (incl. DST).
    /// Java: GSConfig.TIME_ZONE_ID.getRules().getOffset(Instant.now()).getTotalSeconds()
    /// </summary>
    public static int GetOffset() =>
        (int)_timeZone.GetUtcOffset(DateTime.UtcNow).TotalSeconds;

    /// <summary>
    /// Java parity: ServerTime.getStandardOffset() — standard UTC offset in seconds (excl. DST).
    /// Java: GSConfig.TIME_ZONE_ID.getRules().getStandardOffset(Instant.now()).getTotalSeconds()
    /// </summary>
    public static int GetStandardOffset() =>
        (int)_timeZone.BaseUtcOffset.TotalSeconds;
}
