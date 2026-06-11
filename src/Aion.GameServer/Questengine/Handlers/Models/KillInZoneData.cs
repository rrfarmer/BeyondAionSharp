using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers.Template;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/KillInZoneData.</summary>
[XmlType("KillInZoneData")]
public class KillInZoneData : XMLQuest
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

    private List<string> zones;

    [XmlAttribute("zones")]
    public string ZonesRaw
    {
        get => zones == null ? null : string.Join(" ", zones);
        set => zones = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    [XmlAttribute("start_dist_npc_id")] protected int startDistanceNpc;

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new KillInZone(id, endNpcIds, startNpcIds, zones, amount, minRank, levelDiff, startDistanceNpc));
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
