using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Find Litonos (204616) (bring him the Berone's Necklace (182201780)). Talk with Litonos. Take Litonos to Berone (204589). Talk with Berone.
///
/// @author Balthazar, vlog
/// </summary>
public class _1562CrossedDestiny : AbstractQuestHandler
{
    public _1562CrossedDestiny() : base(1562)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204589).AddOnQuestStart(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterQuestNpc(204589).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204616).AddOnTalkEvent(questId);
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
            if (targetId == 204589) // Berone
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                if (env.GetDialogActionId() == DialogAction.ASK_QUEST_ACCEPT)
                    return SendQuestDialog(env, 4);
                if (env.GetDialogActionId() == DialogAction.QUEST_REFUSE_1)
                    return SendQuestDialog(env, 1004);
                if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    return DefaultCloseDialog(env, 0, 1, false, false, 182201780, 1, 0, 0);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204616: // Litonos
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (qs.GetQuestVarById(0) == 1 && player.GetInventory().GetItemCountByItemId(182201780) == 1)
                                return SendQuestDialog(env, 1352);
                            else
                                return SendQuestDialog(env, 1438);
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, 0, 0);
                        case DialogAction.SETPRO2:
                            if (qs.GetQuestVarById(0) == 1)
                            {
                                DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 204589, 0, 0);
                                return DefaultCloseDialog(env, 1, 2, false, false, 0, 0, 182201780, 1); // 2
                            }
                            return false;
                        case DialogAction.USE_OBJECT:
                            if (qs.GetQuestVarById(0) == 1)
                            {
                                return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 204589, 1, 2);
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204589) // Berone
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
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
            if (var == 2)
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
