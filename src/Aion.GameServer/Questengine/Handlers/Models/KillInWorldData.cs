using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers.Template;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/KillInWorldData.</summary>
[XmlType("KillInWorldData")]
public class KillInWorldData : XMLQuest
{
    protected List<int> startNpcIds;
    protected List<int> endNpcIds;

    [XmlAttribute("start_npc_ids")]
    public string StartNpcIdsRaw
    {
        get => startNpcIds == null ? null : string.Join(" ", startNpcIds);
        set => startNpcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("end_npc_ids")]
    public string EndNpcIdsRaw
    {
        get => endNpcIds == null ? null : string.Join(" ", endNpcIds);
        set => endNpcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("amount")] protected int amount;
    [XmlAttribute("min_rank")] protected int minRank;
    [XmlAttribute("level_diff")] protected int levelDiff;

    protected List<int> worldIds;

    [XmlAttribute("worlds")]
    public string WorldIdsRaw
    {
        get => worldIds == null ? null : string.Join(" ", worldIds);
        set => worldIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("invasion_world")] protected int invasionWorld;
    [XmlAttribute("start_dialog_id")] protected int startDialogId;
    [XmlAttribute("start_dist_npc_id")] protected int startDistanceNpcId;
    [XmlAttribute("end_dialog_id")] protected int endDialogId;

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new KillInWorld(id, endNpcIds, startNpcIds, worldIds, amount, minRank, levelDiff, invasionWorld, startDialogId,
            startDistanceNpcId, endDialogId));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        if (startNpcIds != null && startNpcIds.Count > 1 && startNpcIds.Contains(npcId))
            return new HashSet<int>(startNpcIds.Where(id => id != npcId));
        if (endNpcIds != null && endNpcIds.Count > 1 && endNpcIds.Contains(npcId))
            return new HashSet<int>(endNpcIds.Where(id => id != npcId));
        return null;
    }
}
