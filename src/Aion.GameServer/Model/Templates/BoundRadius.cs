using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates;

/// <summary>
/// Bounding radius data for a visible object.
/// Java parity: model/templates/BoundRadius.
/// </summary>
[XmlType("BoundRadius")]
public class BoundRadius
{
    // Java parity: BoundRadius.DEFAULT = new BoundRadius(0f, 0f, 0f)
    public static readonly BoundRadius Default = new(0f, 0f, 0f);

    // Java parity: @XmlAttribute private float front / side / upper (JAXB field access)
    [XmlAttribute("front")]
    public float Front { get; set; }

    [XmlAttribute("side")]
    public float Side { get; set; }

    [XmlAttribute("upper")]
    public float Upper { get; set; }

    // Java parity: BoundRadius() — required for XML deserialization
    public BoundRadius() { }

    // Java parity: BoundRadius(float front, float side, float upper)
    public BoundRadius(float front, float side, float upper)
    {
        Front = front;
        Side = side;
        Upper = upper;
    }

    // Java parity: getFront()
    public float GetFront() => Front;

    // Java parity: getSide()
    public float GetSide() => Side;

    // Java parity: getMaxOfFrontAndSide()
    public float GetMaxOfFrontAndSide() => Math.Max(Front, Side);

    // Java parity: getUpper()
    public float GetUpper() => Upper;
}
