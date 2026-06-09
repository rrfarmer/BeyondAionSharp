using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Road;

/// <summary>Java parity: model/templates/road/RoadPoint (SheppeR).</summary>
[XmlType("RoadPoint")]
public class RoadPoint
{
    [XmlAttribute("x")] private float x;

    [XmlAttribute("y")] private float y;

    [XmlAttribute("z")] private float z;

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
