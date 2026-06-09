using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/SiegeMercenaryZone (Whoop).</summary>
[XmlType("SiegeMercenaryZone")]
public class SiegeMercenaryZone
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("costs")] protected int costs;
    [XmlAttribute("cooldown")] protected int cooldown;
    [XmlAttribute("msg_id")] protected int msgId;
    [XmlAttribute("announce_id")] protected int announceId;

    public int GetId()
    {
        return id;
    }

    public int GetCosts()
    {
        return costs;
    }

    public int GetCooldown()
    {
        return cooldown * 1000;
    }

    public int GetMsgId()
    {
        return msgId;
    }

    public int GetAnnounceId()
    {
        return announceId;
    }
}
