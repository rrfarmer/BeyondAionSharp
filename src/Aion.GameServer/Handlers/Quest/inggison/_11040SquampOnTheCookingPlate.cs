using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _11040SquampOnTheCookingPlate : AbstractQuestHandler
{
    private static readonly int[] npcs = { 799036, 799035 };

    public _11040SquampOnTheCookingPlate() : base(11040)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799036).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        qe.RegisterOnLogOut(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterAddOnLostTargetEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc) env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799036)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 799036:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (qs.GetQuestVarById(0) == 0)
                                return SendQuestDialog(env, 1011);
                            return false;
                        }
                        case DialogAction.SETPRO1:
                        {
                            return DefaultStartFollowEvent(env, (Npc) env.GetVisibleObject(), 296.63f, 482.98f, 574.37f, 0, 1); // 1
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799035)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
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
        return DefaultFollowEndEvent(env, 1, 1, true); // reward
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 0, false); // 0
    }
}
