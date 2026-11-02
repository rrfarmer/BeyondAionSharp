using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Windstreams;

/// <summary>Java parity: model/templates/windstreams/StreamLocations (LokiReborn).</summary>
[XmlType("StreamLocations")]
public class StreamLocations
{
    [XmlElement("location")] public List<Location2D> location;

    public List<Location2D> GetLocation()
    {
        return location == null ? new List<Location2D>() : location;
    }
}
