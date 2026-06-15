using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _11467DeathToTheQueen : AbstractQuestHandler
{
    public _11467DeathToTheQueen() : base(11467)
    {
    }

    public override void Register()
    {
        qe.RegisterOnDie(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterOnQuestTimerEnd(questId);
        qe.RegisterQuestNpc(799527).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799503).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(215480).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799527)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.QuestTimerStart(env, 480);
                    return SendQuestStartDialog(env);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799503)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 215480)
            {
                int var = qs.GetQuestVarById(0);
                QuestService.QuestTimerEnd(env);
                qs.SetRewardGroup(var); // reward group = count of timer resets (quest retries)
                ChangeQuestStep(env, var, var, true);
                return true;
            }
        }
        return false;
    }

    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 0)
            {
                QuestService.QuestTimerStart(env, 240);
                ChangeQuestStep(env, var, 1);
            }
            else if (var == 1)
            {
                QuestService.QuestTimerStart(env, 240);
                ChangeQuestStep(env, var, 2);
            }
            else if (var == 2)
            {
                ChangeQuestStep(env, var, 3);
            }
            return true;
        }
        return false;
    }

    public override bool OnDieEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            QuestService.AbandonQuest(player, questId);
            return true;
        }
        return false;
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            QuestService.AbandonQuest(player, questId);
        }
        return false;
    }
}
