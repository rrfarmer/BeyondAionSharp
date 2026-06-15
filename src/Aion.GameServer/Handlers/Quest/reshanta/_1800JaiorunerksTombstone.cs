using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _1800JaiorunerksTombstone : AbstractQuestHandler
{
    public _1800JaiorunerksTombstone() : base(1800)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(279016).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(279016).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730141).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 279016) // Vindachinerk
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env, 182202163, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 730141: // Jaiorunerk's Tomb
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 0)
                            {
                                if (player.GetInventory().GetItemCountByItemId(182202163) > 0)
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            RemoveQuestItem(env, 182202163, 1);
                            ChangeQuestStep(env, 0, 1);
                            return CloseDialogWindow(env);
                    }
                    break;
                case 279016: // Vindachinerk
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 1, 1, true); // reward
                            return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 279016) // Vindachinerk
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
