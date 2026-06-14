using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _30246YourOathToOutremus : AbstractQuestHandler
{
    public _30246YourOathToOutremus() : base(30246)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798926).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798926).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798926)
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
            if (targetId == 798926)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    qs.SetQuestVar(1);
                    return DefaultCloseDialog(env, 1, 1, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798926)
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
