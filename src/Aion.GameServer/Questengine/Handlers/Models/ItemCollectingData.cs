using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers.Template;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/ItemCollectingData.</summary>
[XmlType("ItemCollectingData")]
public class ItemCollectingData : XMLQuest
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

    [XmlAttribute("next_npc_id")] public int nextNpcId;
    [XmlAttribute("start_zone")] public string startZone;
    [XmlAttribute("start_dialog_id")] public int startDialogId;
    [XmlAttribute("start_dialog_id2")] public int startDialogId2;
    [XmlAttribute("check_ok_dialog_id")] public int checkOkDialogId;
    [XmlAttribute("check_fail_dialog_id")] public int checkFailDialogId;

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new ItemCollecting(id, startNpcIds, nextNpcId, endNpcIds, startZone, questMovie, startDialogId, startDialogId2,
            checkOkDialogId, checkFailDialogId));
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
