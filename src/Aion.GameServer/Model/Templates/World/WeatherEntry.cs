using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.World;

/// <summary>Java parity: model/templates/world/WeatherEntry (Rolandas).</summary>
[XmlType("WeatherEntry")]
public class WeatherEntry
{
    public static readonly WeatherEntry NONE = new WeatherEntry();

    // Public so XmlSerializer can populate them (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("zone_id")] public int zoneId;
    [XmlAttribute("code")] public int weatherCode;
    [XmlAttribute("rank")] public int rank;
    [XmlAttribute("name")] public string weatherName;
    [XmlAttribute("before")] public bool isBefore;
    [XmlAttribute("after")] public bool isAfter;

    // Public parameterless ctor required by XmlSerializer (JAXB used the implicit no-arg ctor).
    public WeatherEntry()
    {
    }

    public WeatherEntry(int zoneId, int weatherCode)
    {
        this.zoneId = zoneId;
        this.weatherCode = weatherCode;
    }

    public int GetZoneId()
    {
        return zoneId;
    }

    public int GetCode()
    {
        return weatherCode;
    }

    public int GetRank()
    {
        return rank;
    }

    public bool IsBefore()
    {
        return isBefore;
    }

    public bool IsAfter()
    {
        return isAfter;
    }

    public string GetWeatherName()
    {
        return weatherName;
    }
}
