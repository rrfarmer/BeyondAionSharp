using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2436LookingForBuBuPat : AbstractQuestHandler
{
    private static readonly int[] npcs = { 204390, 204401 };

    public _2436LookingForBuBuPat() : base(2436)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204390).AddOnQuestStart(questId);
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
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204390)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 204401)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1011);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 204390, 0, 1); // 1
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204390)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
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
