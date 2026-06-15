using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Go to the Mudthorn Experiment Lab and find Belbua (204645). When you're ready to leave, talk to Belbua. Escort Belbua outside the Mudthorn
/// Experiment Lab. Let Phuthollo (204519) know Belbua is free.
/// </summary>
public class _1614WheresBelbua : AbstractQuestHandler
{
    public _1614WheresBelbua() : base(1614)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204519).AddOnQuestStart(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterQuestNpc(204519).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204645).AddOnTalkEvent(questId);
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
            if (targetId == 204519) // Phuthollo
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
                case 204645: // Belbua
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (qs.GetQuestVarById(0) == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            else if (qs.GetQuestVarById(0) == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            ChangeQuestStep(env, 0, 1);
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO2:
                            return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 376f, 529f, 133f, 1, 2);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204519) // Phuthollo
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
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
            if (qs.GetQuestVarById(0) == 2)
            {
                ChangeQuestStep(env, 2, 1);
            }
        }
        return false;
    }

    public override bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 2, 2, true); // reward
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 2, 1, false); // 1
    }
}
