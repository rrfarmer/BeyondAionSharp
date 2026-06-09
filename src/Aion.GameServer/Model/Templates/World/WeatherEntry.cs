using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.World;

/// <summary>Java parity: model/templates/world/WeatherEntry (Rolandas).</summary>
[XmlType("WeatherEntry")]
public class WeatherEntry
{
    public static readonly WeatherEntry NONE = new WeatherEntry();

    [XmlAttribute("zone_id")] private int zoneId;
    [XmlAttribute("code")] private int weatherCode;
    [XmlAttribute("rank")] private int rank;
    [XmlAttribute("name")] private string weatherName;
    [XmlAttribute("before")] private bool isBefore;
    [XmlAttribute("after")] private bool isAfter;

    private WeatherEntry()
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
