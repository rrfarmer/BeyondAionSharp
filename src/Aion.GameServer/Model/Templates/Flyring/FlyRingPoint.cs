using System.Xml.Serialization;
using Aion.GameServer.Model.Geometry;

namespace Aion.GameServer.Model.Templates.Flyring;

/// <summary>Java parity: model/templates/flyring/FlyRingPoint (M@xx).</summary>
[XmlType("FlyRingPoint")]
public class FlyRingPoint
{
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

    public FlyRingPoint()
    {
    }

    public FlyRingPoint(Point3D p)
    {
        x = p.GetX();
        y = p.GetY();
        z = p.GetZ();
    }
}
