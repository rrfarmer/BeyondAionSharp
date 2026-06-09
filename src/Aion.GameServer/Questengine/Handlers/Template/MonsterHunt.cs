using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Rift;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Questengine.Handlers.Models;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Questengine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/MonsterHunt (MrPoke, vlog, Bobobear, Pad, Majka). Set.addAll→UnionWith; isEmpty→Count==0; ZoneName.get(s).name().equalsIgnoreCase→Name comparison; bit-packed quest vars preserved; DataManager/QuestService/RiftService/VortexService red-tolerated.</summary>
public class MonsterHunt : AbstractTemplateQuestHandler
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly HashSet<int> startNpcIds = new();
    private readonly HashSet<int> endNpcIds = new();
    private readonly List<Monster> monsters;
    private readonly int startDialogId;
    private readonly int endDialogId;
    private readonly HashSet<int> aggroNpcIds = new();
    private readonly int invasionWorldId;
    private QuestItems workItem;
    private readonly string startZone;
    private readonly int startDistanceNpcId;
    private readonly bool reward;
    private readonly bool rewardNextStep;
    private readonly bool isDataDriven;

    public MonsterHunt(int questId, List<int> startNpcIds, List<int> endNpcIds, List<Monster> monsters, int startDialogId,
        int endDialogId, List<int> aggroNpcIds, int invasionWorld, string startZone, int startDistanceNpcId, bool reward, bool rewardNextStep) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        if (endNpcIds != null)
            this.endNpcIds.UnionWith(endNpcIds);
        else
            this.endNpcIds.UnionWith(this.startNpcIds);
        this.monsters = monsters;
        this.startDialogId = startDialogId;
        this.endDialogId = endDialogId;
        if (aggroNpcIds != null)
            this.aggroNpcIds.UnionWith(aggroNpcIds);
        this.invasionWorldId = invasionWorld;
        this.startZone = startZone;
        this.startDistanceNpcId = startDistanceNpcId;
        this.reward = reward;
        this.rewardNextStep = rewardNextStep;
        this.isDataDriven = DataManager.QUEST_DATA.GetQuestById(questId).IsDataDriven();
        if (workItems != null)
        {
            if (workItems.Count > 1)
                log.LogWarning("Q{QuestId} has more than 1 work item", questId);
            workItem = workItems[0];
        }
    }

    public override void Register()
    {
        foreach (int startNpcId in startNpcIds)
        {
            qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
        }

        foreach (Monster monster in monsters)
        {
            foreach (int monsterId in monster.GetNpcIds())
                qe.RegisterQuestNpc(monsterId).AddOnKillEvent(questId);
        }

        foreach (int endNpcId in endNpcIds)
            qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);

        foreach (int aggroNpcId in aggroNpcIds)
            qe.RegisterQuestNpc(aggroNpcId).AddOnAddAggroListEvent(questId);

        if (invasionWorldId != 0)
            qe.RegisterOnEnterWorld(questId);

        if (startZone != null && !ZoneName.Get(startZone).Name().Equals("NONE", StringComparison.OrdinalIgnoreCase))
            qe.RegisterOnEnterZone(ZoneName.Get(startZone), questId);

        if (startDistanceNpcId != 0)
            qe.RegisterQuestNpc(startDistanceNpcId, 300).AddOnAtDistanceEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (startNpcIds.Count == 0 || startNpcIds.Contains(targetId)
                || DataManager.QUEST_DATA.GetQuestById(questId).GetCategory() == QuestCategory.FACTION)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, startDialogId != 0 ? startDialogId : isDataDriven ? 4762 : 1011);
                    case DialogAction.QUEST_ACCEPT:
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env, workItem);
                    default:
                        return base.OnDialogEvent(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (endNpcIds.Contains(targetId))
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, endDialogId != 0 ? endDialogId : 1352);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    foreach (Monster mi in monsters)
                    {
                        int endVar = mi.GetEndVar();
                        int varId = mi.GetVar();
                        int total = 0;
                        do
                        {
                            int currentVar = qs.GetQuestVarById(varId);
                            total += currentVar << ((varId - mi.GetVar()) * 6);
                            endVar >>= 6;
                            varId++;
                        } while (endVar > 0);
                        if (mi.GetEndVar() > total)
                        {
                            if (player.HasAccess(AdminConfig.DIALOG_INFO))
                                PacketSendUtility.SendMessage(player, "varId: " + varId + "; req endVar: " + mi.GetEndVar() + "; curr total: " + total);
                            return false;
                        }
                    }
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (endNpcIds.Contains(targetId))
            {
                if (aggroNpcIds.Count != 0 || isDataDriven)
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 10002);
                        case DialogAction.SELECT_QUEST_REWARD:
                            if (workItem != null)
                            {
                                long currentCount = player.GetInventory().GetItemCountByItemId(workItem.GetItemId());
                                if (currentCount > 0)
                                    RemoveQuestItem(env, workItem.GetItemId(), currentCount, QuestStatus.COMPLETE);
                            }
                            break;
                    }
                }
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
            int currentTotalVar = 0;
            int totalEndVar = 0;
            int curStep = qs.GetQuestVarById(0);
            int lastStep = 0;

            foreach (Monster m in monsters)
            {
                lastStep = Math.Max(lastStep, m.GetStep());
                if (isDataDriven && m.GetStep() != curStep) // Check only for current step for new style quests
                    continue;
                if (m.GetNpcIds().Contains(env.GetTargetId()))
                {
                    int endVar = m.GetEndVar();
                    int varId = m.GetVar();
                    int total = 0;
                    do
                    {
                        int currentVar = qs.GetQuestVarById(varId);
                        total += currentVar << ((varId - m.GetVar()) * 6);
                        endVar >>= 6;
                        varId++;
                    } while (endVar > 0);
                    total += 1;
                    if (total <= m.GetEndVar())
                    {
                        if (aggroNpcIds.Count != 0)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return true;
                        }
                        else
                        {
                            int tmpTotal = total;
                            for (int varsUsed = m.GetVar(); varsUsed < varId; varsUsed++)
                            {
                                int value = total & 0x3F;
                                total >>= 6;
                                qs.SetQuestVarById(varsUsed, value);
                            }
                            UpdateQuestStatus(env);
                            if (!isDataDriven) // Old quest style
                            {
                                if (tmpTotal == m.GetEndVar() && (reward || rewardNextStep))
                                {
                                    if (rewardNextStep)
                                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                }
                                return true;
                            }
                        }
                    }
                }
                // Totals for quest step
                totalEndVar += m.GetEndVar();
                currentTotalVar += qs.GetQuestVarById(m.GetVar());
            }

            // Checks if step is completed
            if (currentTotalVar >= totalEndVar && isDataDriven) // New quest style
            {
                qs.SetQuestVar(curStep + 1);
                if (curStep >= lastStep)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                }
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }

    public override bool OnAddAggroListEvent(QuestEnv env)
    {
        return StartQuest(env);
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        VortexLocation vortexLoc = VortexService.GetInstance().GetLocationByWorld(invasionWorldId);
        if (player.GetWorldId() == invasionWorldId)
        {
            if (qs == null || qs.IsStartable())
            {
                if (vortexLoc != null && vortexLoc.IsActive() || SearchOpenRift())
                    return QuestService.StartQuest(env);
            }
        }
        return false;
    }

    private bool SearchOpenRift()
    {
        foreach (RiftLocation loc in RiftService.GetInstance().GetRiftLocations().Values)
        {
            if (loc.GetWorldId() == invasionWorldId && loc.IsOpened())
            {
                return true;
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName.Name().Equals(startZone, StringComparison.OrdinalIgnoreCase))
            return StartQuest(env);
        return false;
    }

    public override bool OnAtDistanceEvent(QuestEnv env)
    {
        return StartQuest(env);
    }

    public bool StartQuest(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            return QuestService.StartQuest(env);
        }
        return false;
    }
}
