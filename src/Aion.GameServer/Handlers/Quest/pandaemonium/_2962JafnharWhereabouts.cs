using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2962JafnharWhereabouts : AbstractQuestHandler
{
    public _2962JafnharWhereabouts() : base(2962)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204253).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204253).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278067).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278137).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204253)
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
            if (targetId == 278067)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1011);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 278137)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 204253)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                        return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SELECT3_1)
                {
                    return SendQuestDialog(env, 1694);
                }
                else if (dialogActionId == DialogAction.SELECT3_2)
                {
                    return SendQuestDialog(env, 1779);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    qs.SetRewardGroup(0);
                    return DefaultCloseDialog(env, 2, 2, true, true);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    qs.SetRewardGroup(1);
                    return DefaultCloseDialog(env, 2, 2, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204253)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
