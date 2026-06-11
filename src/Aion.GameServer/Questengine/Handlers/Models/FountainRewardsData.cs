using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers.Template;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/FountainRewardsData.</summary>
[XmlType("FountainRewardsData")]
public class FountainRewardsData : XMLQuest
{
    protected List<int> startNpcIds;

    [XmlAttribute("start_npc_ids")]
    public string StartNpcIdsRaw
    {
        get => startNpcIds == null ? null : string.Join(" ", startNpcIds);
        set => startNpcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new FountainRewards(id, startNpcIds));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        if (startNpcIds != null && startNpcIds.Count > 1 && startNpcIds.Contains(npcId))
            return new HashSet<int>(startNpcIds.Where(id => id != npcId));
        return null;
    }
}
