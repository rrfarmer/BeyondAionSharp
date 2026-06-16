using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Road;

/// <summary>Java parity: model/templates/road/RoadPoint (SheppeR).</summary>
[XmlType("RoadPoint")]
public class RoadPoint
{
    // Public so XmlSerializer can bind these attributes (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("x")] public float x;

    [XmlAttribute("y")] public float y;

    [XmlAttribute("z")] public float z;

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
