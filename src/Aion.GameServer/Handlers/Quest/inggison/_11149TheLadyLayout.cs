using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _11149TheLadyLayout : AbstractQuestHandler
{
    public _11149TheLadyLayout() : base(11149)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(296491).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(296491).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(206155).AddOnAtDistanceEvent(questId);
        qe.RegisterQuestNpc(206156).AddOnAtDistanceEvent(questId);
        qe.RegisterQuestNpc(206157).AddOnAtDistanceEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 296491)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 296491)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnAtDistanceEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc) env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(1);
            int var1 = qs.GetQuestVarById(2);
            int var2 = qs.GetQuestVarById(3);
            if (targetId == 206155 && var == 0)
            {
                qs.SetQuestVarById(1, 1);
                UpdateQuestStatus(env);
                CheakReward(env);
                return true;
            }
            else if (targetId == 206156 && var1 == 0)
            {
                qs.SetQuestVarById(2, 1);
                UpdateQuestStatus(env);
                CheakReward(env);
                return true;
            }
            else if (targetId == 206157 && var2 == 0)
            {
                qs.SetQuestVarById(3, 1);
                UpdateQuestStatus(env);
                CheakReward(env);
                return true;
            }
        }
        return false;
    }

    private void CheakReward(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int var = qs.GetQuestVarById(1);
        int var1 = qs.GetQuestVarById(2);
        int var2 = qs.GetQuestVarById(3);
        if (var == 1 && var1 == 1 && var2 == 1)
        {
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
        }
    }
}
