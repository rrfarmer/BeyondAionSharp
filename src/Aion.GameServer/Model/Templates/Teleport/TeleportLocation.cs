using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Teleport;

/// <summary>Java parity: model/templates/teleport/TeleportLocation (ATracer).</summary>
[XmlRoot("telelocation")]
public class TeleportLocation
{
    [XmlAttribute("loc_id")] public int locId;
    [XmlAttribute("teleportid")] public int teleportid = 0;
    [XmlAttribute("price")] public int price = 0;
    [XmlAttribute("pricePvp")] public int pricePvp = 0;
    [XmlAttribute("required_quest")] public int required_quest = 0;

    [XmlAttribute("type")] public TeleportType type;

    public int GetLocId()
    {
        return locId;
    }

    public int GetTeleportId()
    {
        return teleportid;
    }

    public int GetPrice()
    {
        return price;
    }

    public int GetPricePvp()
    {
        return pricePvp;
    }

    public int GetRequiredQuest()
    {
        return required_quest;
    }

    public TeleportType GetType_()
    {
        return type;
    }
}
