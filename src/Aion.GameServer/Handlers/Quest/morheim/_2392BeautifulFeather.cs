using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2392BeautifulFeather : AbstractQuestHandler
{
    public _2392BeautifulFeather() : base(2392)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798085).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798085).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798085)
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
            if (targetId == 798085)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    if (player.GetInventory().GetItemCountByItemId(182204159) != 0)
                    {
                        RemoveQuestItem(env, 182204159, 1);
                        ChangeQuestStep(env, 0, 1);
                        qs.SetRewardGroup(0);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 5);
                    }
                    else
                        return SendQuestDialog(env, 1097);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    if (player.GetInventory().GetItemCountByItemId(182204160) != 0)
                    {
                        RemoveQuestItem(env, 182204160, 1);
                        ChangeQuestStep(env, 0, 2);
                        qs.SetRewardGroup(1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 6);
                    }
                    else
                        return SendQuestDialog(env, 1097);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    if (player.GetInventory().GetItemCountByItemId(182204161) != 0)
                    {
                        RemoveQuestItem(env, 182204161, 1);
                        ChangeQuestStep(env, 0, 3);
                        qs.SetRewardGroup(2);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 7);
                    }
                    else
                        return SendQuestDialog(env, 1097);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798085)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
