using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _4718PressingTheAttack : AbstractQuestHandler
{
    public _4718PressingTheAttack() : base(4718)
    {
    }

    public override void Register()
    {
        qe.RegisterOnDredgionReward(questId);
        qe.RegisterQuestNpc(278001).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(278001).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(214814).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 278001)
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
            int var1 = qs.GetQuestVarById(1);
            int var2 = qs.GetQuestVarById(2);
            if (targetId == 278001)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1011);
                    }
                    else if (var1 == 3 && var2 == 8)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 1, 1, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 278001)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, 214814, 0, 8, 2);
    }

    public override bool OnDredgionRewardEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var1 = qs.GetQuestVarById(1);
            if (var1 < 3)
            {
                ChangeQuestStep(env, var1, var1 + 1, false, 1);
                return true;
            }
        }
        return false;
    }
}
