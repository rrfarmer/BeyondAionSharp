using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.World;

/// <summary>Java parity: model/templates/world/WeatherTable (Rolandas).</summary>
[XmlType("WeatherTable")]
public class WeatherTable
{
    [XmlElement("table")] protected List<WeatherEntry> zoneData;
    [XmlAttribute("weather_count")] protected int weatherCount;
    [XmlAttribute("zone_count")] protected int zoneCount;
    [XmlAttribute("id")] protected int mapId;

    public List<WeatherEntry> GetZoneData()
    {
        return zoneData;
    }

    public int GetMapId()
    {
        return mapId;
    }

    public int GetZoneCount()
    {
        return zoneCount;
    }

    public int GetWeatherCount()
    {
        return weatherCount;
    }

    public WeatherEntry GetWeatherAfter(WeatherEntry entry)
    {
        if (entry == null || entry.GetWeatherName() == null || entry.IsAfter())
            return null;
        foreach (WeatherEntry we in GetZoneData())
        {
            if (we.GetZoneId() != entry.GetZoneId())
                continue;
            if (entry.GetWeatherName().Equals(we.GetWeatherName()))
            {
                if (entry.IsBefore() && !we.IsBefore() && !we.IsAfter())
                    return we;
                else if (!entry.IsBefore() && !entry.IsAfter() && we.IsAfter())
                    return we;
            }
        }
        return null;
    }

    public List<WeatherEntry> GetWeathersForZone(int zoneId)
    {
        List<WeatherEntry> result = new List<WeatherEntry>();
        foreach (WeatherEntry entry in GetZoneData())
        {
            if (entry.GetZoneId() == zoneId)
                result.Add(entry);
        }
        return result;
    }
}
