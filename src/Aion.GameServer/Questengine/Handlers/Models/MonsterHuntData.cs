using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Questengine;
using Aion.GameServer.Questengine.Handlers.Template;

namespace Aion.GameServer.Questengine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/MonsterHuntData. @XmlSeeAlso→[XmlInclude] (KillSpawned/MentorMonsterHunt).</summary>
[XmlType("MonsterHuntData")]
[XmlInclude(typeof(KillSpawnedData))]
[XmlInclude(typeof(MentorMonsterHuntData))]
public class MonsterHuntData : XMLQuest
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

    [XmlAttribute("start_dialog_id")] protected int startDialogId;
    [XmlAttribute("end_dialog_id")] protected int endDialogId;

    protected List<int> aggroNpcIds;

    [XmlAttribute("aggro_start_npc_ids")]
    public string AggroNpcIdsRaw
    {
        get => aggroNpcIds == null ? null : string.Join(" ", aggroNpcIds);
        set => aggroNpcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("invasion_world")] protected int invasionWorld;
    [XmlAttribute("start_zone")] protected string startZone;
    [XmlAttribute("start_dist_npc_id")] protected int startDistanceNpcId;
    [XmlAttribute("end_reward")] protected bool reward;
    [XmlAttribute("end_reward_next_step")] protected bool rewardNextStep;

    public override void Register(QuestEngine questEngine)
    {
        List<Monster> monsters;
        QuestTemplate questTemplate = DataManager.QUEST_DATA.GetQuestById(id);

        if (questTemplate.GetQuestKill().Count != 0)
        {
            monsters = new List<Monster>();
            foreach (QuestKill qk in questTemplate.GetQuestKill())
            {
                Monster m = new Monster();
                if (qk.GetKillCount() > 0)
                    m.SetEndVar(qk.GetKillCount());
                if (qk.GetNpcIds() != null)
                    m.AddNpcIds(qk.GetNpcIds());
                if (qk.GetVar() > 0)
                    m.SetVar(qk.GetVar());
                if (qk.GetQuestStep() > 0)
                    m.SetStep(qk.GetQuestStep());
                if (qk.GetSequenceNumber() > 0)
                    m.SetVar(qk.GetSequenceNumber());
                monsters.Add(m);
            }
        }
        else
        {
            monsters = new List<Monster>();
        }

        questEngine.AddQuestHandler(new MonsterHunt(id, startNpcIds, endNpcIds, monsters, startDialogId, endDialogId, aggroNpcIds, invasionWorld,
            startZone, startDistanceNpcId, reward, rewardNextStep));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        if (startNpcIds != null && startNpcIds.Count > 1 && startNpcIds.Contains(npcId))
            return new HashSet<int>(startNpcIds.Where(id => id != npcId));
        if (endNpcIds != null && endNpcIds.Count > 1 && endNpcIds.Contains(npcId))
            return new HashSet<int>(endNpcIds.Where(id => id != npcId));
        if (aggroNpcIds != null && aggroNpcIds.Count > 1 && aggroNpcIds.Contains(npcId))
            return new HashSet<int>(aggroNpcIds.Where(id => id != npcId));
        foreach (QuestKill qk in DataManager.QUEST_DATA.GetQuestById(id).GetQuestKill())
        {
            List<int> npcIds = qk.GetNpcIds();
            if (npcIds != null && npcIds.Count > 1 && npcIds.Contains(npcId))
                return new HashSet<int>(npcIds.Where(id => id != npcId));
        }
        return null;
    }
}
