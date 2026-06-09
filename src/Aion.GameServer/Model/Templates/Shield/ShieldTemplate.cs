using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Shield;

/// <summary>Java parity: model/templates/shield/ShieldTemplate (M@xx, Wakizashi).</summary>
[XmlType("Shield")]
public class ShieldTemplate
{
    [XmlAttribute("name")] protected string name;

    [XmlAttribute("map")] protected int map;

    [XmlAttribute("id")] protected int id;

    [XmlAttribute("radius")] protected float radius;

    [XmlElement("center")] protected ShieldPoint center;

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

    public ShieldPoint GetCenter()
    {
        return center;
    }

    public int GetId()
    {
        return id;
    }
}
