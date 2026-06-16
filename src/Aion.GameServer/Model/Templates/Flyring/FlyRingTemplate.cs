using System.Xml.Serialization;
using Aion.GameServer.Model.Geometry;

namespace Aion.GameServer.Model.Templates.Flyring;

/// <summary>Java parity: model/templates/flyring/FlyRingTemplate (M@xx).</summary>
[XmlType("FlyRing")]
public class FlyRingTemplate
{
    [XmlAttribute("name")] public string name;

    [XmlAttribute("map")] public int map;

    [XmlAttribute("radius")] public float radius;

    [XmlElement("center")] public FlyRingPoint center;

    [XmlElement("p1")] public FlyRingPoint p1;

    [XmlElement("p2")] public FlyRingPoint p2;

    public string GetName()
    {
        return name;
    }

    public int GetMap()
    {
        return map;
    }

    public float GetRadius()
    {
        return radius;
    }

    public FlyRingPoint GetCenter()
    {
        return center;
    }

    public FlyRingPoint GetP1()
    {
        return p1;
    }

    public FlyRingPoint GetP2()
    {
        return p2;
    }

    public FlyRingTemplate()
    {
    }

    public FlyRingTemplate(string name, int mapId, Point3D center, Point3D p1, Point3D p2, int radius)
    {
        this.name = name;
        this.map = mapId;
        this.radius = radius;
        this.center = new FlyRingPoint(center);
        this.p1 = new FlyRingPoint(p1);
        this.p2 = new FlyRingPoint(p2);
    }
}
