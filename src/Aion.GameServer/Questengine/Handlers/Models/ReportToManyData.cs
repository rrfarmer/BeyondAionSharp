using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Questengine;
using Aion.GameServer.Questengine.Handlers.Template;

namespace Aion.GameServer.Questengine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/ReportToManyData (propOrder npcInfos). NpcInfos red until ported.</summary>
[XmlType("ReportToManyData")]
public class ReportToManyData : XMLQuest
{
    [XmlElement("npc_infos")] private List<NpcInfos> npcInfos;
    [XmlAttribute("start_item_id")] private int startItemId;

    private List<int> startNpcIds;

    [XmlAttribute("start_npc_ids")]
    public string StartNpcIdsRaw
    {
        get => startNpcIds == null ? null : string.Join(" ", startNpcIds);
        set => startNpcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("start_dialog_id")] private int startDialogId;

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new ReportToMany(id, startItemId, startNpcIds, npcInfos, startDialogId, mission));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        if (startNpcIds != null && startNpcIds.Count > 1 && startNpcIds.Contains(npcId))
            return new HashSet<int>(startNpcIds.Where(id => id != npcId));
        foreach (NpcInfos npcInfo in npcInfos)
        {
            List<int> npcIds = npcInfo.GetNpcIds();
            if (npcIds.Count > 1 && npcIds.Contains(npcId))
                return new HashSet<int>(npcIds.Where(id => id != npcId));
        }
        return null;
    }
}
