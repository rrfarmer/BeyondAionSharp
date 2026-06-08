using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/Point2D — 2D float coordinate.</summary>
[XmlType("Point2D")]
public class Point2D
{
    [XmlAttribute("x")] public float X { get; set; }
    [XmlAttribute("y")] public float Y { get; set; }

    public Point2D() { }

    public Point2D(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float GetX() => X;
    public float GetY() => Y;
}
