using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestKill (Rolandas).</summary>
[XmlType("QuestKill")]
public class QuestKill
{
    [XmlAttribute("seq")] private int seq;

    // Java parity: @XmlAttribute(name="npc_ids") List<Integer> — space-separated.
    private List<int> npcIds;

    [XmlAttribute("npc_ids")]
    public string NpcIdsRaw
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set => npcIds = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("count")] private int kill;
    [XmlAttribute("var")] private int var;
    [XmlAttribute("step")] private int step;

    [XmlIgnore] private List<int> npcIdSet;

    public int GetSequenceNumber()
    {
        return seq;
    }

    public int GetKillCount()
    {
        return kill;
    }

    public int GetVar()
    {
        return var;
    }

    public int GetQuestStep()
    {
        return step;
    }

    public List<int> GetNpcIds()
    {
        if (npcIdSet == null)
            npcIdSet = new List<int>();
        if (npcIds != null)
        {
            npcIdSet.AddRange(npcIds);
            npcIds.Clear();
            npcIds = null;
        }
        return npcIdSet;
    }
}
