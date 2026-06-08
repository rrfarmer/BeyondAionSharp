using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npc;

/// <summary>
/// Massive-loot parameters for an NPC.
/// Java parity: model/templates/npc/MassiveLoot (@XmlType("MassiveLoot")).
/// </summary>
[XmlType("MassiveLoot")]
public class MassiveLoot
{
    [XmlAttribute("m_loot_count")] public int MassiveLootCount { get; set; }
    [XmlAttribute("m_loot_item")] public int MassiveLootItem { get; set; }
    [XmlAttribute("m_loot_min_level")] public int MassiveLootMinLevel { get; set; }
    [XmlAttribute("m_loot_max_level")] public int MassiveLootMaxLevel { get; set; }

    public int GetMLootCount() => MassiveLootCount;
    public int GetMLootItem() => MassiveLootItem;
    public int GetMLootMinLevel() => MassiveLootMinLevel;
    public int GetMLootMaxLevel() => MassiveLootMaxLevel;
}
