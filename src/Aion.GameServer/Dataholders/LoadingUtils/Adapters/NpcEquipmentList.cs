using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders.LoadingUtils.Adapters;

/// <summary>Java parity: dataholders/loadingutils/adapters/NpcEquipmentList (Luno).</summary>
public class NpcEquipmentList
{
    // Java parity: @XmlElement("item") @XmlIDREF ItemTemplate[] — references item templates by id.
    // Resolved templates: populated by the cache/override path (CustomInstanceBossAI). XmlSerializer cannot
    // resolve @XmlIDREF, so when loading npc_templates.xml standalone the raw ids land in ItemIds (below)
    // and are resolved to ItemTemplates lazily in NpcEquippedGear.Init() via DataManager.ITEM_DATA.
    [XmlIgnore]
    public ItemTemplate[] items;

    // Java parity proxy: the <equipment><item>ID</item>… element list as raw ids (deferred IDREF resolution).
    [XmlElement("item")]
    public int[] ItemIds;
}
