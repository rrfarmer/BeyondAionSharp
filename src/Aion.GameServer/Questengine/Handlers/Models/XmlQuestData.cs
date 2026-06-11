using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Events;
using Aion.GameServer.QuestEngine.Handlers.Template;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/XmlQuestData (@XmlType "XmlQuest", propOrder onTalkEvents/onKillEvents).</summary>
[XmlType("XmlQuest")]
public class XmlQuestData : XMLQuest
{
    [XmlElement("on_talk_event")] protected List<OnTalkEvent> onTalkEvents;
    [XmlElement("on_kill_event")] protected List<OnKillEvent> onKillEvents;

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

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new Aion.GameServer.QuestEngine.Handlers.Template.XmlQuest(id, startNpcIds, endNpcIds, onTalkEvents, onKillEvents));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        if (startNpcIds != null && startNpcIds.Count > 1 && startNpcIds.Contains(npcId))
            return new HashSet<int>(startNpcIds.Where(id => id != npcId));
        if (endNpcIds != null && endNpcIds.Count > 1 && endNpcIds.Contains(npcId))
            return new HashSet<int>(endNpcIds.Where(id => id != npcId));
        if (onTalkEvents != null)
        {
            foreach (OnTalkEvent onTalkEvent in onTalkEvents)
            {
                List<int> npcIds = onTalkEvent.GetIds();
                if (npcIds.Count > 1 && npcIds.Contains(npcId))
                    return new HashSet<int>(npcIds.Where(id => id != npcId));
            }
        }
        if (onKillEvents != null)
        {
            foreach (OnKillEvent onKillEvent in onKillEvents)
            {
                foreach (Monster monster in onKillEvent.GetMonsters())
                {
                    List<int> npcIds = monster.GetNpcIds();
                    if (npcIds.Count > 1 && npcIds.Contains(npcId))
                        return new HashSet<int>(npcIds.Where(id => id != npcId));
                }
            }
        }
        return null;
    }
}
