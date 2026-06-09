using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Road;

/// <summary>Java parity: model/templates/road/RoadExit (SheppeR).</summary>
[XmlType("RoadExit")]
public class RoadExit
{
    [XmlAttribute("mapid")] private int mapId;

    [XmlAttribute("x")] private float x;

    [XmlAttribute("y")] private float y;

    [XmlAttribute("z")] private float z;

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
