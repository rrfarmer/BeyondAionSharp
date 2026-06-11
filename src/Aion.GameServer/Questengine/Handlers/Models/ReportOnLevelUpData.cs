using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers.Template;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/ReportOnLevelUpData.</summary>
[XmlType("ReportOnLevelUpData")]
public class ReportOnLevelUpData : XMLQuest
{
    protected List<int> endNpcIds;

    [XmlAttribute("end_npc_ids")]
    public string EndNpcIdsRaw
    {
        get => endNpcIds == null ? null : string.Join(" ", endNpcIds);
        set => endNpcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new ReportOnLevelUp(id, endNpcIds));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        if (endNpcIds != null && endNpcIds.Count > 1 && endNpcIds.Contains(npcId))
            return new HashSet<int>(endNpcIds.Where(id => id != npcId));
        return null;
    }
}
