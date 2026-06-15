using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _3725MyLuckyNumber : AbstractQuestHandler
{
    public _3725MyLuckyNumber() : base(3725)
    {
    }

    public override void Register()
    {
        qe.RegisterOnDredgionReward(questId);
        qe.RegisterQuestNpc(799069).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799069).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798928).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(281866).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(216866).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799069)
            { // Yannis
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
            if (targetId == 798928)
            { // Yulia
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var1 == 6 && var2 == 15)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798928)
            { // Yulia
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        int[] mobs = { 281866, 216866 };
        return DefaultOnKillEvent(env, mobs, 0, 15, 2); // 2: 0 - 15
    }

    public override bool OnDredgionRewardEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var1 = qs.GetQuestVarById(1);
            if (var1 < 6)
            {
                ChangeQuestStep(env, var1, var1 + 1, false, 1); // 1: 0 - 6
                return true;
            }
        }
        return false;
    }
}
