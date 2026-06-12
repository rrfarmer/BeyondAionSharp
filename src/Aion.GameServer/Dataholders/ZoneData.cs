using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ZoneData (ATracer). @XmlRootElement(zones)/@XmlType("")→[XmlRoot]/[XmlType("")]; @XmlTransient maps/count→[XmlIgnore]; afterUnmarshal builds Poly/Cylinder/Sphere/Semisphere areas per AreaType + ZoneInfo per zone, weather-zone numbering; saveData JAXB-marshal literal (red-tolerated). All geometry areas/ZoneTemplate/ZoneInfo green.</summary>
[XmlType("")]
[XmlRoot("zones")]
public class ZoneData
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("zone")]
    public List<ZoneTemplate> zoneList;

    [XmlIgnore]
    private readonly Dictionary<int, List<ZoneInfo>> zoneNameMap = new();

    [XmlIgnore]
    private readonly Dictionary<ZoneTemplate, int> weatherZoneIds = new();

    [XmlIgnore]
    private int count;

    public void AfterUnmarshal(object parent)
    {
        int lastMapId = 0;
        int weatherZoneId = 1;
        foreach (ZoneTemplate zone in zoneList)
        {
            Area area = null;
            switch (zone.GetAreaType())
            {
                case AreaType.Polygon:
                    area = new PolyArea(zone.GetName(), zone.GetMapid(), zone.GetPoints().GetPoint(), zone.GetPoints().GetBottom(), zone.GetPoints().GetTop());
                    break;
                case AreaType.Cylinder:
                    area = new CylinderArea(zone.GetName(), zone.GetMapid(), zone.GetCylinder().GetX().Value, zone.GetCylinder().GetY().Value, zone.GetCylinder().GetR().Value,
                        zone.GetCylinder().GetBottom().Value, zone.GetCylinder().GetTop().Value);
                    break;
                case AreaType.Sphere:
                    if (zone.GetSphere().GetR() <= 0)
                        break;
                    area = new SphereArea(zone.GetName(), zone.GetMapid(), zone.GetSphere().GetX().Value, zone.GetSphere().GetY().Value, zone.GetSphere().GetZ().Value, zone
                        .GetSphere().GetR().Value);
                    break;
                case AreaType.Semisphere:
                    area = new SemisphereArea(zone.GetName(), zone.GetMapid(), zone.GetSemisphere().GetX().Value, zone.GetSemisphere().GetY().Value, zone.GetSemisphere()
                        .GetZ().Value, zone.GetSemisphere().GetR().Value);
                    break;
            }
            if (area != null)
            {
                if (!zoneNameMap.TryGetValue(zone.GetMapid(), out List<ZoneInfo> zones))
                {
                    zones = new List<ZoneInfo>();
                    zoneNameMap[zone.GetMapid()] = zones;
                }
                if (zone.GetZoneType() == ZoneClassName.WEATHER)
                {
                    if (lastMapId != zone.GetMapid())
                    {
                        lastMapId = zone.GetMapid();
                        weatherZoneId = 1;
                    }
                    weatherZoneIds[zone] = weatherZoneId++;
                }
                zones.Add(new ZoneInfo(area, zone));
                count++;
            }
        }
        zoneList = null;
    }

    public Dictionary<int, List<ZoneInfo>> GetZones()
    {
        return zoneNameMap;
    }

    public int Size()
    {
        return count;
    }

    /// <summary>Weather zone ID it's an order number (starts from 1)</summary>
    public int GetWeatherZoneId(ZoneTemplate template)
    {
        return weatherZoneIds.GetValueOrDefault(template, 0);
    }

    public void SaveData()
    {
        // Java parity: dataholders/ZoneData.saveData — JAXB marshal → idiomatic C# XmlSerializer (infra).
        const string xmlPath = "./data/static_data/zones/generated_zones.xml";
        try
        {
            var serializer = new XmlSerializer(typeof(ZoneData));
            using var writer = new StreamWriter(xmlPath);
            serializer.Serialize(writer, this);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while saving data: " + e.Message);
        }
    }
}
