using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author HellBoy, Majka
/// </summary>
public class _2230AFriendlyWager : AbstractQuestHandler
{
    private static readonly int questDropItemId = 182203223; // Mosbear Tusks
    private static readonly int questStartNpcId = 203621; // Shania
    private static readonly int questDurationTime = 1800; // Duration time of the quest 1800

    public _2230AFriendlyWager() : base(2230)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(questStartNpcId).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(questStartNpcId).AddOnTalkEvent(questId);
        qe.RegisterOnQuestTimerEnd(questId);
        qe.RegisterOnLogOut(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (targetId == questStartNpcId)
        {
            if (qs == null || qs.IsStartable())
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_ACCEPT_1:
                        if (QuestService.StartQuest(env))
                        {
                            QuestService.QuestTimerStart(env, questDurationTime);
                            return SendQuestDialog(env, 1003);
                        }
                        break;
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SETPRO1:
                        if (!player.GetController().HasTask(TaskId.QUEST_TIMER)) // A new chance starts
                        {
                            QuestService.QuestTimerStart(env, questDurationTime);
                        }
                        return SendQuestSelectionDialog(env);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        // Still time left; check collected items: reward if right number otherwise dialogue to continue
                        if (player.GetController().HasTask(TaskId.QUEST_TIMER))
                        {
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                QuestService.QuestTimerEnd(env);
                                return SendQuestDialog(env, 5);
                            }
                            else
                            {
                                return SendQuestDialog(env, 2716);
                            }
                        }
                        else // Time ended; remove quest items and ask for new chance;
                        {
                            long mosbearTusks = player.GetInventory().GetItemCountByItemId(questDropItemId);
                            RemoveQuestItem(env, questDropItemId, mosbearTusks);
                            return SendQuestDialog(env, 3057);
                        }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    // On time end if not in reward status delete timer task
    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            player.GetController().CancelTask(TaskId.QUEST_TIMER);
            return true;
        }
        return false;
    }

    // On logout if not in reward status delete quest items and quest itself
    public override bool OnLogOutEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            long mosbearTusks = player.GetInventory().GetItemCountByItemId(questDropItemId);
            RemoveQuestItem(env, questDropItemId, mosbearTusks);
            QuestService.AbandonQuest(player, questId);
            return true;
        }
        return false;
    }
}
