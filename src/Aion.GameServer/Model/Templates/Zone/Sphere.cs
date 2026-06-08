using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/Sphere — sphere zone geometry descriptor.</summary>
[XmlType("Sphere")]
public class Sphere
{
    [XmlAttribute("x")] public float? X { get; set; }
    [XmlAttribute("y")] public float? Y { get; set; }
    [XmlAttribute("z")] public float? Z { get; set; }
    [XmlAttribute("r")] public float? R { get; set; }

    public Sphere() { }

    public Sphere(float x, float y, float z, float radius)
    {
        X = x; Y = y; Z = z; R = radius;
    }

    public float? GetX() => X;
    public float? GetY() => Y;
    public float? GetZ() => Z;
    public float? GetR() => R;
}
