using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2920ElementaryMyDearDaeva : AbstractQuestHandler
{
    public _2920ElementaryMyDearDaeva() : base(2920)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204141).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204141).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204141) // Deyla
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 204141) // Deyla
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SETPRO1:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO2:
                        return SendQuestDialog(env, 1693);
                    case DialogAction.SETPRO11:
                        ChangeQuestStep(env, 0, 0, true); // reward
                        qs.SetRewardGroup(0);
                        return SendQuestDialog(env, 5);
                    case DialogAction.SETPRO12:
                        ChangeQuestStep(env, 0, 0, true); // reward
                        qs.SetRewardGroup(1);
                        return SendQuestDialog(env, 6);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204141) // Deyla
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
