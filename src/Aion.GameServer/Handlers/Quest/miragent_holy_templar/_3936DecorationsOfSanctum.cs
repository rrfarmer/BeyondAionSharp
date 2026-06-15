using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Nanou, Gigi
/// </summary>
public class _3936DecorationsOfSanctum : AbstractQuestHandler
{
    public _3936DecorationsOfSanctum() : base(3936)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203710).AddOnQuestStart(questId);// Dairos
        qe.RegisterQuestNpc(203710).AddOnTalkEvent(questId);// Dairos
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        // Start to Dairos
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203710)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                // 1 - Report the result to Dairos.
                case 203710:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            else if (var == 1)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182206091);
                            long itemCount2 = player.GetInventory().GetItemCountByItemId(182206092);
                            long itemCount3 = player.GetInventory().GetItemCountByItemId(182206093);
                            long itemCount4 = player.GetInventory().GetItemCountByItemId(182206094);
                            if (itemCount1 >= 10 && itemCount2 >= 10 && itemCount3 >= 10 && itemCount4 >= 10)
                            {
                                RemoveQuestItem(env, 182206091, 10);
                                RemoveQuestItem(env, 182206092, 10);
                                RemoveQuestItem(env, 182206093, 10);
                                RemoveQuestItem(env, 182206094, 10);
                                ChangeQuestStep(env, 1, 1, true);
                                return SendQuestDialog(env, 5);
                            }
                            else
                                return SendQuestDialog(env, 10001);
                    }
                    break;
                // No match
                default:
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203710)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
