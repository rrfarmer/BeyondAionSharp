using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _19047JustBetweenMeAndFasimedes : AbstractQuestHandler
{
    public _19047JustBetweenMeAndFasimedes() : base(19047)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203700).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203700).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203700)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203700)
            {
                if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                    return DefaultCloseDialog(env, 0, 0, true, true);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203700)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
