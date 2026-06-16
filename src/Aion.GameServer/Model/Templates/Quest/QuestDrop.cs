using System.Globalization;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestDrop (MrPoke).</summary>
[XmlType("QuestDrop")]
public class QuestDrop
{
    // Java parity: npcId/itemId/chance are nullable Integer @XmlAttribute (absent -> null). XmlSerializer cannot
    // bind Nullable<int> to an attribute, so round-trip through string proxies (1:1 with JAXB). The fields are
    // read cross-instance by sibling subclass HandlerSideDrop, so keep them protected internal.
    protected internal int? npcId;
    protected internal int? itemId;
    protected int? chance;

    [XmlAttribute("npc_id")]
    public string NpcIdRaw
    {
        get => npcId?.ToString(CultureInfo.InvariantCulture);
        set => npcId = value == null ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("item_id")]
    public string ItemIdRaw
    {
        get => itemId?.ToString(CultureInfo.InvariantCulture);
        set => itemId = value == null ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("chance")]
    public string ChanceRaw
    {
        get => chance?.ToString(CultureInfo.InvariantCulture);
        set => chance = value == null ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("drop_each_member")] public int dropEachMember = 0;
    [XmlAttribute("collecting_step")] public int collecting_step = 0;

    [XmlIgnore] protected int? questId;

    /// <summary>Gets the value of the npcId property.</summary>
    public int? GetNpcId()
    {
        return npcId;
    }

    /// <summary>Gets the value of the itemId property.</summary>
    public int? GetItemId()
    {
        return itemId;
    }

    /// <summary>Gets the value of the chance property.</summary>
    public int GetChance()
    {
        if (chance == null)
            return 100;
        return chance.Value;
    }

    public bool IsDropEachMemberGroup()
    {
        return dropEachMember == 1;
    }

    public bool IsDropEachMemberAlliance()
    {
        return dropEachMember == 1 || dropEachMember == 2;
    }

    public int? GetQuestId()
    {
        return questId;
    }

    public int GetCollectingStep()
    {
        return collecting_step;
    }

    public void SetQuestId(int? questId)
    {
        this.questId = questId;
    }
}
