using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Shield;

/// <summary>Java parity: model/templates/shield/ShieldTemplate (M@xx, Wakizashi).</summary>
[XmlType("Shield")]
public class ShieldTemplate
{
    [XmlAttribute("name")] public string name;

    [XmlAttribute("map")] public int map;

    [XmlAttribute("id")] public int id;

    [XmlAttribute("radius")] public float radius;

    [XmlElement("center")] public ShieldPoint center;

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
