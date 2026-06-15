using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2411EarthSpiritWaterSpirit : AbstractQuestHandler
{
    public _2411EarthSpiritWaterSpirit() : base(2411)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204369).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204366).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204364).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204369)
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
            if (targetId == 204369)
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
            if (targetId == 204366 && qs.GetQuestVarById(0) == 1)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1352);
                }
            }
            else if (targetId == 204364 && qs.GetQuestVarById(0) == 2)
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
