using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2488PretorsInBeluslan : AbstractQuestHandler
{
    public _2488PretorsInBeluslan() : base(2488)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204053).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204208).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204702).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204053)
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
            if (targetId == 204208)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    qs.SetQuestVar(1);
                    return DefaultCloseDialog(env, 1, 1, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204702)
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
