using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Find Poppy (203191), the lost Porgus. Take Poppy safely to Cannon (203145). Talk with Cannon.
///
/// @author Rhys2002, vlog
/// </summary>
public class _1149MissingPoppy : AbstractQuestHandler
{
    public _1149MissingPoppy() : base(1149)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203145).AddOnQuestStart(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterQuestNpc(203145).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203191).AddOnTalkEvent(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterAddOnLostTargetEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (targetId == 203145)
        { // Cannon
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
            else
            {
                return false;
            }
        }
        else if (targetId == 203191)
        { // Poppy
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && var == 0)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 203145, 0, 1); // 1
                }
            }
        }
        return false;
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 1)
            {
                ChangeQuestStep(env, 1, 0);
            }
        }
        return false;
    }

    public override bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 1, true, 12); // reward
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 0, false); // 0
    }
}
