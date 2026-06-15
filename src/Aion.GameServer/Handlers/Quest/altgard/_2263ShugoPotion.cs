using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _2263ShugoPotion : AbstractQuestHandler
{
    private static readonly int questDropItemId = 182203242; // Malodor Pollen
    private static readonly int questStartNpcId = 798036; // Mabrunerk

    public _2263ShugoPotion() : base(2263)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(questStartNpcId).AddOnQuestStart(questId); // Mabrunerk
        qe.RegisterQuestNpc(questStartNpcId).AddOnTalkEvent(questId); // Mabrunerk
        qe.RegisterOnQuestTimerEnd(questId);
        qe.RegisterOnLogOut(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
        {
            targetId = npc.GetNpcId();
        }
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == questStartNpcId) // Mabrunerk
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_ACCEPT_1:
                        if (QuestService.StartQuest(env))
                        {
                            QuestService.QuestTimerStart(env, 300);
                            return SendQuestDialog(env, 1003);
                        }
                        break;
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == questStartNpcId) // Mabrunerk
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        // Checks if player has 3 Malodor Pollens [ID: 182203242]
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
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == questStartNpcId) // Mabrunerk
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    // On time end if not in reward status delete quest items and quest itself
    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            long malodorPollen = player.GetInventory().GetItemCountByItemId(questDropItemId);
            RemoveQuestItem(env, questDropItemId, malodorPollen);
            QuestService.AbandonQuest(player, questId);
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
            long malodorPollen = player.GetInventory().GetItemCountByItemId(questDropItemId);
            RemoveQuestItem(env, questDropItemId, malodorPollen);
            QuestService.AbandonQuest(player, questId);
            return true;
        }
        return false;
    }
}
