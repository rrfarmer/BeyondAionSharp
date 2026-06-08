using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/Points — polygon zone boundary points with top/bottom Z.</summary>
[XmlType("Points")]
public class Points
{
    [XmlElement("point")]    public List<Point2D>? Point  { get; set; }
    [XmlAttribute("top")]    public float           Top    { get; set; }
    [XmlAttribute("bottom")] public float           Bottom { get; set; }

    public Points() { }

    public Points(float bottom, float top)
    {
        Bottom = bottom;
        Top = top;
    }

    /// <summary>Java parity: getPoint() — returns list, initialising if null.</summary>
    public List<Point2D> GetPoint()
    {
        Point ??= [];
        return Point;
    }

    public float GetTop()    => Top;
    public float GetBottom() => Bottom;
}
