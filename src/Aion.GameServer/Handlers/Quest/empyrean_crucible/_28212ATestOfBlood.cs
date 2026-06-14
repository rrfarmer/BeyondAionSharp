using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _28212ATestOfBlood : AbstractQuestHandler
{
    public _28212ATestOfBlood() : base(28212)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205986).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205986).AddOnTalkEvent(questId);
        qe.RegisterOnKillInWorld(300350000, questId);
        qe.RegisterOnKillInWorld(300360000, questId);
    }

    public override bool OnKillInWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            GiveQuestItem(env, 182212221, 1);
            return DefaultOnKillRankedEvent(env, 0, 1, true);
        }
        return false;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetTargetId() == 205986)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else
                {
                    RemoveQuestItem(env, 182212221, 1);
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
