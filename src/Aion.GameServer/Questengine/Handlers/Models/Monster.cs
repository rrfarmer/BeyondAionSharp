using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/Monster. npc_ids @XmlAttribute List<Integer>→Raw space-sep.</summary>
[XmlType("Monster")]
public class Monster
{
    [XmlAttribute("var")] protected int var;
    [XmlAttribute("start_var")] protected int startVar;
    [XmlAttribute("end_var")] protected int endVar;

    private List<int> npcIds;

    [XmlAttribute("npc_ids")]
    public string NpcIdsRaw
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set => npcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("npc_seq")] private int npcSequence;
    [XmlAttribute("step")] private int step;
    [XmlAttribute("spawner_object_id")] protected int spawnerObjectId;

    public int GetVar() { return var; }
    public void SetVar(int value) { this.var = value; }
    public int GetStartVar() { return startVar; }
    public void SetStartVar(int value) { this.startVar = value; }
    public int GetEndVar() { return endVar; }
    public void SetEndVar(int value) { this.endVar = value; }

    public List<int> GetNpcIds() { return npcIds; }

    public void AddNpcIds(List<int> value)
    {
        if (this.npcIds == null)
            this.npcIds = new List<int>();
        foreach (int npc in value)
        {
            if (!this.npcIds.Contains(npc))
                this.npcIds.Add(npc);
        }
    }

    public int GetNpcSequence() { return npcSequence; }
    public void SetNpcSequence(int value) { this.npcSequence = value; }
    public int GetStep() { return step; }
    public void SetStep(int value) { this.step = value; }
    public int GetSpawnerNpcId() { return spawnerObjectId; }
}
