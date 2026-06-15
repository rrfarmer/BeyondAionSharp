using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2922FascinatingGift : AbstractQuestHandler
{
    public _2922FascinatingGift() : base(2922)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204261).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204261).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798058).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204108).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204261)
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
            if (targetId == 204261)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1003);
                }
                else if (dialogActionId == DialogAction.SELECT1_1)
                {
                    return SendQuestDialog(env, 1012);
                }
                else if (dialogActionId == DialogAction.SELECT1_2)
                {
                    return SendQuestDialog(env, 1097);
                }
                else if (dialogActionId == DialogAction.SETPRO10)
                {
                    qs.SetQuestVar(10);
                    qs.SetRewardGroup(0);
                    return DefaultCloseDialog(env, 10, 10, true, false);
                }
                else if (dialogActionId == DialogAction.SETPRO20)
                {
                    qs.SetQuestVar(20);
                    qs.SetRewardGroup(1);
                    return DefaultCloseDialog(env, 20, 20, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798058 && qs.GetQuestVarById(0) == 10)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                return SendQuestEndDialog(env);
            }
            else if (targetId == 204108 && qs.GetQuestVarById(0) == 20)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1693);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
