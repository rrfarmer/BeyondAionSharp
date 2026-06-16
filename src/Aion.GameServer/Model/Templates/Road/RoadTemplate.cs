using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Road;

/// <summary>Java parity: model/templates/road/RoadTemplate (SheppeR).</summary>
[XmlType("Road")]
public class RoadTemplate
{
    // Public so XmlSerializer can bind these members (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("name")] public string name;

    [XmlAttribute("map")] public int map;

    [XmlAttribute("radius")] public float radius;

    [XmlElement("center")] public RoadPoint center;

    [XmlElement("p1")] public RoadPoint p1;

    [XmlElement("p2")] public RoadPoint p2;

    [XmlElement("roadexit")] public RoadExit roadExit;

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
