using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1604ToCatchASpy : AbstractQuestHandler
{
    public _1604ToCatchASpy() : base(1604)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204576).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204576).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(212615).AddOnAttackEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204576)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204576)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 10002);
                else
                    return SendQuestEndDialog(env);
            }
            return false;
        }
        return false;
    }

    public override bool OnAttackEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START || qs.GetQuestVars().GetQuestVars() != 0)
        {
            return false;
        }

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
        {
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        }
        if (targetId != 212615)
        {
            return false;
        }

        if (PositionUtil.GetDistance(env.GetVisibleObject(), 717.78f, 623.50f, 130) < 30)
        {
            ((Npc)env.GetVisibleObject()).GetController().Die(player);
            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
        }
        return false;
    }
}
