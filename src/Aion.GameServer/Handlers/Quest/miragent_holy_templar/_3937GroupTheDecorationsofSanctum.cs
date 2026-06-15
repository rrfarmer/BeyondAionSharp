using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Gigi
 */
public class _3937GroupTheDecorationsofSanctum : AbstractQuestHandler
{
    public _3937GroupTheDecorationsofSanctum() : base(3937)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203708).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203708).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203708)
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
                case 203708:
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
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182206095);
                            if (itemCount1 >= 1)
                            {
                                RemoveQuestItem(env, 182206095, 1);
                                ChangeQuestStep(env, 1, 1, true);
                                return SendQuestDialog(env, 5);
                            }
                            else
                                return SendQuestDialog(env, 10001);
                    }
                    break;
                default:
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203708)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
