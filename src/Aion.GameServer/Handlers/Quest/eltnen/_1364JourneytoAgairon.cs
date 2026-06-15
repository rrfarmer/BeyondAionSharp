using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Starts Ernia (203944). Take Teos (203945) to Dellome (790007) in Agairon Village. Talk to Dellome.
///
/// @author Rhys2002, vlog
/// </summary>
public class _1364JourneytoAgairon : AbstractQuestHandler
{
    public _1364JourneytoAgairon() : base(1364)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203944).AddOnQuestStart(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterQuestNpc(203945).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(790007).AddOnTalkEvent(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterAddOnLostTargetEvent(questId);
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
            if (targetId == 203944)
            { // Ernia
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203945: // Teos
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (qs.GetQuestVarById(0) == 0)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 790007, 0, 1); // 1
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 790007)
            { // Dellome
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else
                    return SendQuestEndDialog(env);
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
        ChangeQuestStep(env, 1, 3);
        return DefaultFollowEndEvent(env, 3, 3, true, 47); // reward
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 0, false); // 0
    }
}
