using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Handlers.Models;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/ReportToMany (Hilgert, vlog, Pad, Neon). Set.addAll→UnionWith; isEmpty→Count==0; super.onDialogEvent→base; HandlerResult.fromBoolean→FromBoolean/UNKNOWN; DataManager/QuestService/NpcInfos red-tolerated.</summary>
public class ReportToMany : AbstractTemplateQuestHandler
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly int startItemId;
    private readonly HashSet<int> startNpcIds = new();
    private readonly int startDialogId;
    private readonly List<NpcInfos> npcInfos = new();
    private readonly bool mission;
    private readonly bool isDataDriven;
    private bool rewardStatusFromRewardNpc = true; // workaround flag for end npc dialog behavior (see below)

    public ReportToMany(int questId, int startItemId, List<int> startNpcIds, List<NpcInfos> npcInfos, int startDialogId, bool mission) : base(questId)
    {
        this.startItemId = startItemId;
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        this.npcInfos.AddRange(npcInfos);
        this.startDialogId = startDialogId;
        this.mission = mission;
        this.isDataDriven = DataManager.QUEST_DATA.GetQuestById(questId).IsDataDriven();
        if (workItems != null && workItems.Count > this.npcInfos.Count)
            log.LogWarning("Q{QuestId} has more work items than quest steps", questId);
    }

    public override void Register()
    {
        if (mission)
        {
            qe.RegisterOnLevelChanged(questId);
        }
        if (startItemId != 0)
            qe.RegisterQuestItem(startItemId, questId);
        else
        {
            foreach (int startNpcId in startNpcIds)
            {
                qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
                qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
            }
        }
        foreach (NpcInfos npcInfo in npcInfos)
        {
            foreach (int npcId in npcInfo.GetNpcIds())
            {
                qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
            }
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if ((startNpcIds.Count == 0 || startNpcIds.Contains(targetId))
                && (startItemId == 0 || player.GetInventory().GetFirstItemByItemId(startItemId) != null))
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_ACCEPT:
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env);
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, startDialogId != 0 ? startDialogId : isDataDriven ? 4762 : 1011);
                    default:
                        return base.OnDialogEvent(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int step = qs.GetQuestVarById(0); // starting from 0
            if (step > GetMaxStep())
            {
                log.LogWarning("Missing NpcInfo for quest {QuestId} step #{Step}", questId, step + 1);
                return false;
            }
            NpcInfos targetNpcInfo = npcInfos[step];
            if (!targetNpcInfo.GetNpcIds().Contains(targetId))
                return false;

            switch (dialogActionId)
            {
                case DialogAction.QUEST_SELECT:
                    return SendQuestDialog(env, GetDialogId(step));
                case DialogAction.SETPRO1:
                case DialogAction.SETPRO2:
                case DialogAction.SETPRO3:
                case DialogAction.SETPRO4:
                case DialogAction.SETPRO5:
                case DialogAction.SETPRO6:
                case DialogAction.SETPRO7:
                case DialogAction.SETPRO8:
                case DialogAction.SETPRO9:
                case DialogAction.SETPRO10:
                case DialogAction.SETPRO11:
                case DialogAction.SETPRO12:
                    ChangeQuestStep(env, step, step + 1);
                    if (workItems != null && workItems.Count > step)
                        GiveQuestItem(env, workItems[step].GetItemId(), workItems[step].GetCount());
                    return CloseDialogWindow(env);
                case DialogAction.SET_SUCCEED:
                case DialogAction.SELECT_QUEST_REWARD:
                case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                case DialogAction.CHECK_USER_HAS_QUEST_ITEM_SIMPLE:
                    if (dialogActionId == DialogAction.SET_SUCCEED) // set reward from pre-end npc (end npc is another one who will then give the reward)
                    {
                        rewardStatusFromRewardNpc = false;
                        step++;
                    }
                    if (step < GetMaxStep() || !ValidateAndRemoveItems(env))
                        return SendQuestSelectionDialog(env);
                    qs.SetQuestVarById(0, step);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestEndDialog(env);
                default:
                    if (targetNpcInfo.GetMovie() != 0)
                        PlayQuestMovie(env, targetNpcInfo.GetMovie());
                    return base.OnDialogEvent(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            NpcInfos endNpcInfo = npcInfos[GetMaxStep()];
            if (!endNpcInfo.GetNpcIds().Contains(targetId))
                return false;
            if (dialogActionId == DialogAction.USE_OBJECT && !rewardStatusFromRewardNpc) // if talking to an end npc who did not set the reward state himself
                return SendQuestDialog(env, isDataDriven ? 10002 : 2375); // show full reward dialog instead of only last page (otherwise it's never readable)
            return SendQuestEndDialog(env);
        }
        return false;
    }

    private int GetMaxStep()
    {
        return npcInfos.Count - 1;
    }

    private int GetDialogId(int var)
    {
        if (var == GetMaxStep())
            return isDataDriven ? 10002 : 2375;
        else
            return (isDataDriven ? 1011 : 1352) + var * 341;
    }

    private bool ValidateAndRemoveItems(QuestEnv env)
    {
        if (!QuestService.CollectItemCheck(env, true))
            return false;
        if (startItemId != 0 && !RemoveQuestItem(env, startItemId, 1))
            return false;
        if (workItems != null)
        {
            foreach (QuestItems workItem in workItems)
                RemoveQuestItem(env, workItem.GetItemId(), workItem.GetCount(), QuestStatus.COMPLETE);
        }
        return true;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        if (startItemId != 0)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.IsStartable())
            {
                return HandlerResultExtensions.FromBoolean(SendQuestDialog(env, 4));
            }
        }
        return HandlerResult.UNKNOWN;
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player);
    }
}
