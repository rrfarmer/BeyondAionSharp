using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Windstreams;

/// <summary>Java parity: model/templates/windstreams/WindstreamTemplate (LokiReborn).</summary>
[XmlType("WindFlight")]
public class WindstreamTemplate
{
    [XmlElement("locations")] public StreamLocations locations;
    [XmlAttribute("mapid")] public int mapid;

    /// <summary>Gets the value of the locations property.</summary>
    public StreamLocations GetLocations()
    {
        return locations;
    }

    /// <summary>Gets the value of the mapid property.</summary>
    public int GetMapId()
    {
        return mapid;
    }
}
