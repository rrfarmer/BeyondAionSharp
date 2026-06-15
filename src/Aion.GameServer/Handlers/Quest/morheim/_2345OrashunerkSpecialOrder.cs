using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2345OrashunerkSpecialOrder : AbstractQuestHandler
{
    public _2345OrashunerkSpecialOrder() : base(2345)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798084).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798084).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700238).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204304).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798084)
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
            int var = qs.GetQuestVarById(0);
            if (targetId == 798084)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 0)
                        return SendQuestDialog(env, 1011);
                    else if (var == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    return CheckQuestItems(env, 0, 1, false, 10000, 10001);
                }
                else if (dialogActionId == DialogAction.SETPRO10)
                {
                    GiveQuestItem(env, 182204137, 1);
                    ChangeQuestStep(env, 1, 10);
                    qs.SetRewardGroup(0);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
                else if (dialogActionId == DialogAction.SETPRO20)
                {
                    GiveQuestItem(env, 182204138, 1);
                    ChangeQuestStep(env, 1, 20);
                    qs.SetRewardGroup(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
            }
            else if (targetId == 700238 && player.GetInventory().GetItemCountByItemId(182204136) < 3)
            {
                return true; // looting
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204304)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    if (qs.GetQuestVarById(0) == 10)
                    {
                        RemoveQuestItem(env, 182204137, 1);
                        return SendQuestDialog(env, 1693);
                    }
                    else if (qs.GetQuestVarById(0) == 20)
                    {
                        RemoveQuestItem(env, 182204138, 1);
                        return SendQuestDialog(env, 2034);
                    }
                }
                return SendQuestEndDialog(env);
            }
        }

        return false;
    }
}
