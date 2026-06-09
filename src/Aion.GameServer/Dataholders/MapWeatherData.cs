using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.World;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/MapWeatherData. @XmlRootElement(weather); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("weather")]
public class MapWeatherData
{
    [XmlElement("map")] private List<WeatherTable> weatherData;

    [XmlIgnore] private readonly Dictionary<int, WeatherTable> mapWeather = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (WeatherTable table in weatherData)
        {
            mapWeather[table.GetMapId()] = table;
        }
        weatherData = null;
    }

    public WeatherTable GetWeather(int mapId)
    {
        return mapWeather.TryGetValue(mapId, out var v) ? v : null;
    }

    public int Size()
    {
        return mapWeather.Count;
    }
}
