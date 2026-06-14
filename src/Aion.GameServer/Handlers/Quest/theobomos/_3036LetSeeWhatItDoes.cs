using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3036LetSeeWhatItDoes : AbstractQuestHandler
{
    public _3036LetSeeWhatItDoes() : base(3036)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798155).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798155).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700398).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798155)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env, 182208026, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 700398)
            {
                RemoveQuestItem(env, 182208026, 1);
                return UseQuestObject(env, 0, 1, true, false);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798155)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    default:
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
        }
        return false;
    }
}
