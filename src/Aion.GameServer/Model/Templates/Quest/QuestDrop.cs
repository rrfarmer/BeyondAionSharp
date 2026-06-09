using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestDrop (MrPoke).</summary>
[XmlType("QuestDrop")]
public class QuestDrop
{
    // Java parity: npcId/itemId/dropEachMember are read cross-instance by sibling subclass HandlerSideDrop.
    // Java `protected` includes same-package access; C# `protected internal` (derived OR same-assembly) is the closest analog.
    [XmlAttribute("npc_id")] protected internal int? npcId;
    [XmlAttribute("item_id")] protected internal int? itemId;
    [XmlAttribute("chance")] protected int? chance;
    [XmlAttribute("drop_each_member")] protected internal int dropEachMember = 0;
    [XmlAttribute("collecting_step")] protected int collecting_step = 0;

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
