using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1909ASongOfPraise : AbstractQuestHandler
{
    public _1909ASongOfPraise() : base(1909)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203739).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203739).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203726).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203099).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetTargetId() == 203739)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (env.GetTargetId() == 203726)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    DefaultCloseDialog(env, 0, 1, 182206001, 1, 0, 0);
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (env.GetTargetId() == 203099)
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    return DefaultCloseDialog(env, 1, 2, true, true, 0, 0, 182206001, 1);
                }
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
