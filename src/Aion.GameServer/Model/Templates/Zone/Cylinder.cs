using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/Cylinder — cylinder zone geometry descriptor.</summary>
[XmlType("Cylinder")]
public class Cylinder
{
    [XmlAttribute("top")]    public float? Top    { get; set; }
    [XmlAttribute("bottom")] public float? Bottom { get; set; }
    [XmlAttribute("x")]      public float? X      { get; set; }
    [XmlAttribute("y")]      public float? Y      { get; set; }
    [XmlAttribute("r")]      public float? R      { get; set; }

    public Cylinder() { }

    public Cylinder(float x, float y, float radius, float top, float bottom)
    {
        X = x; Y = y; R = radius; Top = top; Bottom = bottom;
    }

    public float? GetTop()    => Top;
    public float? GetBottom() => Bottom;
    public float? GetX()      => X;
    public float? GetY()      => Y;
    public float? GetR()      => R;
}
