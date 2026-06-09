using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Hotspot;

/// <summary>Java parity: model/templates/hotspot/HotspotTemplate (ginho1).</summary>
[XmlType("Hotspot")]
public class HotspotTemplate
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("worldId")] protected int worldId;
    [XmlAttribute("x")] protected float x;
    [XmlAttribute("y")] protected float y;
    [XmlAttribute("z")] protected float z;
    [XmlAttribute("race")] protected Race race;
    [XmlAttribute("price")] protected long price;

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
