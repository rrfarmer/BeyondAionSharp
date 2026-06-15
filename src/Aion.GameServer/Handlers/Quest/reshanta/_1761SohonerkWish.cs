using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1761SohonerkWish : AbstractQuestHandler
{
    public _1761SohonerkWish() : base(1761)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(279014).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(279014).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(279017).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(279018).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 279014)
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
            if (targetId == 279014)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
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
                    ChangeQuestStep(env, 0, 1);
                    qs.SetRewardGroup(0);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
                else if (dialogActionId == DialogAction.SETPRO20)
                {
                    ChangeQuestStep(env, 0, 2);
                    qs.SetRewardGroup(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 279017)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1352);
                }
            }
            else if (targetId == 279018)
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
