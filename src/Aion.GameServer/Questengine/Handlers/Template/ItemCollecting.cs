using System;
using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/ItemCollecting (MrPoke, vlog, Rolandas, Majka, Pad). Set.addAll→UnionWith; Set.equals→SetEquals; isEmpty→Count==0; ZoneName.get(s).name().equalsIgnoreCase→Name comparison; super.onDialogEvent→base; DataManager/QuestService red-tolerated.</summary>
public class ItemCollecting : AbstractTemplateQuestHandler
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly HashSet<int> startNpcIds = new();
    private readonly HashSet<int> endNpcIds = new();
    private readonly int questMovie;
    private readonly int nextNpcId;
    private readonly int startDialogId;
    private readonly int startDialogId2;
    private readonly int checkOkDialogId;
    private readonly int checkFailDialogId;
    private readonly bool isDataDriven;
    private readonly string startZone;
    private QuestItems workItem;

    public ItemCollecting(int questId, List<int> startNpcIds, int nextNpcId, List<int> endNpcIds, string startZone, int questMovie,
        int startDialogId, int startDialogId2, int checkOkDialogId, int checkFailDialogId) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        this.nextNpcId = nextNpcId;
        if (endNpcIds != null)
            this.endNpcIds.UnionWith(endNpcIds);
        else
            this.endNpcIds.UnionWith(this.startNpcIds);
        this.startZone = startZone;
        this.questMovie = questMovie;
        this.startDialogId = startDialogId;
        this.startDialogId2 = startDialogId2;
        this.checkOkDialogId = checkOkDialogId;
        this.checkFailDialogId = checkFailDialogId;
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
        if (nextNpcId != 0)
        {
            qe.RegisterQuestNpc(nextNpcId).AddOnTalkEvent(questId);
        }
        if (!endNpcIds.SetEquals(startNpcIds))
        {
            foreach (int endNpcId in endNpcIds)
                qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);
        }
        if (actionItems != null)
        {
            foreach (int actionItem in actionItems)
            {
                qe.RegisterQuestNpc(actionItem).AddOnTalkEvent(questId);
                qe.RegisterCanAct(questId, actionItem);
            }
        }
        if (startZone != null && !ZoneName.Get(startZone).Name().Equals("NONE", StringComparison.OrdinalIgnoreCase))
            qe.RegisterOnEnterZone(ZoneName.Get(startZone), questId);
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
                    case DialogAction.SETPRO1:
                        QuestService.StartQuest(env);
                        return CloseDialogWindow(env);
                    case DialogAction.SELECT1_1:
                        if (questMovie != 0)
                        {
                            PlayQuestMovie(env, questMovie);
                        }
                        return SendQuestDialog(env, 1012);
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
            int var = qs.GetQuestVarById(0);
            if (targetId == nextNpcId && var == 0)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (endNpcIds.Contains(targetId))
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, startDialogId2 != 0 ? startDialogId2 : isDataDriven ? 1011 : 2375);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        int okDialogId = checkOkDialogId != 0 ? checkOkDialogId : isDataDriven ? 10000 : 5;
                        int failDialogId = checkFailDialogId != 0 ? checkFailDialogId : isDataDriven ? 10001 : 2716;
                        return CheckQuestItems(env, var, var, true, okDialogId, failDialogId); // reward
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM_SIMPLE:
                        return CheckQuestItemsSimple(env, var, var, true, 5, 0, 0); // reward
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                    case DialogAction.SET_SUCCEED:
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return CloseDialogWindow(env);
                    case DialogAction.SETPRO1:
                        return CheckQuestItemsSimple(env, var, var, true, 5, 0, 0);
                    case DialogAction.SETPRO2:
                        return CheckQuestItemsSimple(env, var, var, true, 6, 0, 0);
                    case DialogAction.SETPRO3:
                        return CheckQuestItemsSimple(env, var, var, true, 7, 0, 0);
                    case DialogAction.SETPRO4:
                        return CheckQuestItemsSimple(env, var, var, true, 8, 0, 0);
                }
            }
            else if (actionItems != null && actionItems.Contains(targetId))
            {
                return true; // looting
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (endNpcIds.Contains(targetId))
            {
                if (workItem != null)
                {
                    long currentCount = player.GetInventory().GetItemCountByItemId(workItem.GetItemId());
                    if (currentCount > 0)
                        RemoveQuestItem(env, workItem.GetItemId(), currentCount, QuestStatus.COMPLETE);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName.Name().Equals(startZone, StringComparison.OrdinalIgnoreCase))
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.IsStartable())
            {
                QuestService.StartQuest(env);
                return true;
            }
        }
        return false;
    }
}
