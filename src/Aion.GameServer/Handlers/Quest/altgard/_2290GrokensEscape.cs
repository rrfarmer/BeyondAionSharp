using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Escort Groken (203608) to the sailboat (700178). Talk with Manir (203607).
///
/// @author Mr. Poke, vlog
/// </summary>
public class _2290GrokensEscape : AbstractQuestHandler
{
    public _2290GrokensEscape() : base(2290)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203608).AddOnQuestStart(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterQuestNpc(203608).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700178).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203607).AddOnTalkEvent(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterAddOnLostTargetEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203608) // Groken
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestDialog(env, 1003);
                    case DialogAction.QUEST_REFUSE_1:
                        return SendQuestDialog(env, 1004);
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                    case DialogAction.SELECT1_1:
                        if (QuestService.StartQuest(env))
                        {
                            return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 700178, 0, 1); // 1
                        }
                        break;
                }
                return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203608) // Groken
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetQuestVarById(0) == 0)
                {
                    return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 700178, 0, 1); // 1
                }
            }
            else if (targetId == 203607) // Groken
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetQuestVarById(0) == 3)
                {
                    return SendQuestDialog(env, 1693);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 3, 3, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203607) // Manir
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 5);
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
        return DefaultFollowEndEvent(env, 1, 3, false, 69);
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 0, false); // 0
    }
}
