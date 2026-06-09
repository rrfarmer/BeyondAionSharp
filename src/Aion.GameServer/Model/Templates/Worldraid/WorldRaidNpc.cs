using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Worldraid;

/// <summary>Java parity: model/templates/worldraid/WorldRaidNpc (Sykra).</summary>
[XmlType("WorldRaidNpc")]
public class WorldRaidNpc
{
    [XmlAttribute("npc_id")] private int npcId = 0;
    [XmlAttribute("death_msg_id")] private int? deathMsgId = 0;

    public int GetNpcId()
    {
        return npcId;
    }

    public int? GetDeathMsgId()
    {
        return deathMsgId;
    }
}
