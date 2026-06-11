using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.QuestEngine.Handlers.Models;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/KillSpawned (vlog, Pad). Set.addAll→UnionWith; Set.equals→SetEquals; isEmpty→Count==0; Collections.emptyList→new List; DataManager/SpawnSpotTemplate/spawnForFiveMinutes red-tolerated.</summary>
public class KillSpawned : AbstractTemplateQuestHandler
{
    private readonly HashSet<int> startNpcIds = new();
    private readonly HashSet<int> endNpcIds = new();
    private readonly HashSet<int> spawnerObjectIds = new();
    private readonly List<Monster> spawnedMonsters;
    private readonly bool isDataDriven;

    public KillSpawned(int questId, List<int> startNpcIds, List<int> endNpcIds, List<Monster> spawnedMonsters) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        if (endNpcIds != null)
            this.endNpcIds.UnionWith(endNpcIds);
        else
            this.endNpcIds.UnionWith(this.startNpcIds);
        this.spawnedMonsters = spawnedMonsters == null ? new List<Monster>() : spawnedMonsters;
        foreach (Monster m in this.spawnedMonsters)
            spawnerObjectIds.Add(m.GetSpawnerNpcId());
        this.isDataDriven = DataManager.QUEST_DATA.GetQuestById(questId).IsDataDriven();
    }

    public override void Register()
    {
        foreach (int startNpcId in startNpcIds)
        {
            qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
        }
        if (!endNpcIds.SetEquals(startNpcIds))
        {
            foreach (int endNpcId in endNpcIds)
                qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);
        }
        foreach (Monster spawnedMonster in spawnedMonsters)
        {
            foreach (int spawnedMonsterId in spawnedMonster.GetNpcIds())
                qe.RegisterQuestNpc(spawnedMonsterId).AddOnKillEvent(questId);
        }
        foreach (int spawnerObjectId in spawnerObjectIds)
            qe.RegisterQuestNpc(spawnerObjectId).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (startNpcIds.Count == 0 || startNpcIds.Contains(targetId))
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, isDataDriven ? 4762 : 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (spawnerObjectIds.Contains(targetId))
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    int monsterId = 0;
                    foreach (Monster m in spawnedMonsters)
                    {
                        if (m.GetSpawnerNpcId() == targetId)
                        {
                            monsterId = m.GetNpcIds()[0];
                            break;
                        }
                    }
                    if (monsterId == 0)
                        return false;
                    SpawnSpotTemplate spot = DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(player.GetWorldId(), targetId).GetSpot();
                    SpawnForFiveMinutes(monsterId, player.GetWorldMapInstance(), spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading());
                    return true;
                }
            }
            else
            {
                foreach (Monster m in spawnedMonsters)
                {
                    if (m.GetEndVar() > qs.GetQuestVarById(m.GetVar()))
                    {
                        return false;
                    }
                }
                if (endNpcIds.Contains(targetId))
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                    {
                        return SendQuestDialog(env, 5);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (endNpcIds.Contains(targetId))
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            foreach (Monster m in spawnedMonsters)
            {
                if (m.GetNpcIds().Contains(env.GetTargetId()))
                {
                    if (qs.GetQuestVarById(m.GetVar()) < m.GetEndVar())
                    {
                        qs.SetQuestVarById(m.GetVar(), qs.GetQuestVarById(m.GetVar()) + 1);
                        foreach (Monster n in spawnedMonsters)
                        {
                            if (qs.GetQuestVarById(n.GetVar()) < n.GetEndVar())
                            {
                                UpdateQuestStatus(env);
                                return true;
                            }
                        }
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
