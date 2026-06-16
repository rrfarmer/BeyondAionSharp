using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Hotspot;

/// <summary>Java parity: model/templates/hotspot/HotspotTemplate (ginho1).</summary>
[XmlType("Hotspot")]
public class HotspotTemplate
{
    // Public so XmlSerializer can bind these attributes (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("id")] public int id;
    [XmlAttribute("worldId")] public int worldId;
    [XmlAttribute("x")] public float x;
    [XmlAttribute("y")] public float y;
    [XmlAttribute("z")] public float z;
    [XmlAttribute("race")] public Race race;
    [XmlAttribute("price")] public long price;

    public int GetId()
    {
        return id;
    }

    public int GetWorldId()
    {
        return worldId;
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }

    public Race GetRace()
    {
        return race;
    }

    public long GetPrice()
    {
        return price;
    }
}
