using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _21106TheRealRhonnam : AbstractQuestHandler
{
    public _21106TheRealRhonnam() : base(21106)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799271).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799271).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799272).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799273).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799274).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799271)
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
            if (targetId == 799272)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 799273)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 799274)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    qs.SetQuestVar(3);
                    return DefaultCloseDialog(env, 3, 3, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799271)
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
