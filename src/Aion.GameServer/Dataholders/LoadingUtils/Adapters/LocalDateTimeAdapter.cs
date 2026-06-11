using System;
using System.Globalization;

namespace Aion.GameServer.Dataholders.LoadingUtils.Adapters;

/// <summary>
/// Java parity: dataholders/loadingutils/adapters/LocalDateTimeAdapter (Neon). JAXB XmlAdapter&lt;String, LocalDateTime&gt;
/// → plain Marshal/Unmarshal class (no C# XmlAdapter base). LocalDateTime.parse / toString → ISO-8601 round-trip.
/// </summary>
public class LocalDateTimeAdapter
{
    public string Marshal(DateTime v)
    {
        // ISO-8601 (matches java.time.LocalDateTime.toString()).
        return v.ToString("s", CultureInfo.InvariantCulture);
    }

    public DateTime Unmarshal(string v)
    {
        return DateTime.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
