using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Ritsu
 */
public class _28407GroupTheGathering : AbstractQuestHandler
{
    public _28407GroupTheGathering() : base(28407)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799557).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799557).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730380).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799558).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 799557)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                    return DefaultCloseDialog(env, 1, 2);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 730380)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    return DefaultCloseDialog(env, 0, 1, 182215016, 1, 0, 0);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 799558)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, 5);
                }
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
