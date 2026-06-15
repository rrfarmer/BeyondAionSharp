using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2367APrizedPossession : AbstractQuestHandler
{
    public _2367APrizedPossession() : base(2367)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204339).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798080).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798079).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204339)
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
            if (targetId == 204339)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1003);
                }
                else if (dialogActionId == DialogAction.SELECT1_1)
                {
                    GiveQuestItem(env, 182204147, 1);
                    return SendQuestDialog(env, 1012);
                }
                else if (dialogActionId == DialogAction.SELECT1_2)
                {
                    GiveQuestItem(env, 182204147, 1);
                    return SendQuestDialog(env, 1097);
                }
                else if (dialogActionId == DialogAction.SETPRO10)
                {
                    ChangeQuestStep(env, 0, 10);
                    qs.SetRewardGroup(0);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
                else if (dialogActionId == DialogAction.SETPRO20)
                {
                    ChangeQuestStep(env, 0, 20);
                    qs.SetRewardGroup(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798079)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1352);
                }
            }
            else if (targetId == 798080)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1693);
                }
            }
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
