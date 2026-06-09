using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Road;

/// <summary>Java parity: model/templates/road/RoadTemplate (SheppeR).</summary>
[XmlType("Road")]
public class RoadTemplate
{
    [XmlAttribute("name")] protected string name;

    [XmlAttribute("map")] protected int map;

    [XmlAttribute("radius")] protected float radius;

    [XmlElement("center")] protected RoadPoint center;

    [XmlElement("p1")] protected RoadPoint p1;

    [XmlElement("p2")] protected RoadPoint p2;

    [XmlElement("roadexit")] protected RoadExit roadExit;

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

    public RoadPoint GetCenter()
    {
        return center;
    }

    public RoadPoint GetP1()
    {
        return p1;
    }

    public RoadPoint GetP2()
    {
        return p2;
    }

    public RoadExit GetRoadExit()
    {
        return roadExit;
    }
}
