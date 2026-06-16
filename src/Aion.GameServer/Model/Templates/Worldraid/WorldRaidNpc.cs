using System.Globalization;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Worldraid;

/// <summary>Java parity: model/templates/worldraid/WorldRaidNpc (Sykra).</summary>
[XmlType("WorldRaidNpc")]
public class WorldRaidNpc
{
    [XmlAttribute("npc_id")] public int npcId = 0;

    // Java parity: nullable Integer death_msg_id (field initializer = 0). String-proxy: XmlSerializer
    // cannot bind a nullable value type to an [XmlAttribute]; back it with a string and parse it, mirroring
    // JAXB's native nullable-attribute handling — missing attribute -> keeps the 0 initializer, present -> parseInt.
    [XmlIgnore] private int? deathMsgId = 0;

    [XmlAttribute("death_msg_id")]
    public string DeathMsgIdRaw
    {
        get => deathMsgId?.ToString(CultureInfo.InvariantCulture);
        set => deathMsgId = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public int? GetDeathMsgId()
    {
        return deathMsgId;
    }
}
