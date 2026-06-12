using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/ReportTo (MrPoke, Rolandas, Pad). Set.addAll→UnionWith; Set.equals→SetEquals; isEmpty→Count==0; super.onDialogEvent→base; DataManager red-tolerated.</summary>
public class ReportTo : AbstractTemplateQuestHandler
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly HashSet<int> startNpcIds = new();
    private readonly HashSet<int> endNpcIds = new();
    private readonly int startDialogId;
    private readonly bool isDataDriven;
    private QuestItems workItem;

    public ReportTo(int questId, List<int> startNpcIds, List<int> endNpcIds, int startDialogId) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        if (endNpcIds != null)
            this.endNpcIds.UnionWith(endNpcIds);
        else
            this.endNpcIds.UnionWith(this.startNpcIds);
        this.startDialogId = startDialogId;
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
        if (!endNpcIds.SetEquals(startNpcIds))
        {
            foreach (int endNpcId in endNpcIds)
            {
                qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);
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
            if (startNpcIds.Count == 0 || startNpcIds.Contains(targetId))
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
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, isDataDriven ? 10002 : 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        if (workItem != null)
                        {
                            long currentCount = player.GetInventory().GetItemCountByItemId(workItem.GetItemId());
                            if (currentCount < workItem.GetCount())
                            {
                                return SendQuestSelectionDialog(env);
                            }
                            RemoveQuestItem(env, workItem.GetItemId(), currentCount, QuestStatus.COMPLETE);
                        }
                        qs.SetQuestVar(1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestEndDialog(env);
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
}
