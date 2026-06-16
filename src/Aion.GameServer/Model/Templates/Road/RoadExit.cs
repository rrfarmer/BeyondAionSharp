using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Road;

/// <summary>Java parity: model/templates/road/RoadExit (SheppeR).</summary>
[XmlType("RoadExit")]
public class RoadExit
{
    // Public so XmlSerializer can bind these attributes (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("mapid")] public int mapId;

    [XmlAttribute("x")] public float x;

    [XmlAttribute("y")] public float y;

    [XmlAttribute("z")] public float z;

    public int GetMap()
    {
        return mapId;
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }
}
