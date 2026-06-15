using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Get the antidote (182208035) from Calydon Sorcerer (214304) and bring it to Ruria (798211). Talk with Ruria. Escort Ruria to the place where
/// Melleas (798208) is. Talk with Melleas. Tell Rosina (798190) about Ruria.
/// </summary>
public class _3050RescuingRuria : AbstractQuestHandler
{
    public _3050RescuingRuria() : base(3050)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798211).AddOnQuestStart(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterQuestNpc(798211).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798208).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798190).AddOnTalkEvent(questId);
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
            if (targetId == 798211) // Ruria
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798211: // Ruria
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (qs.GetQuestVarById(0) == 0)
                            {
                                long itemCount = player.GetInventory().GetItemCountByItemId(182208035);
                                if (itemCount >= 1)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                return SendQuestDialog(env, 1097);
                            }
                            else if (qs.GetQuestVarById(0) == 1)
                            {
                                return SendQuestDialog(env, 1013);
                            }
                            return false;
                        case DialogAction.SELECT1_1:
                            RemoveQuestItem(env, 182208035, 1);
                            ChangeQuestStep(env, 0, 1);
                            return SendQuestDialog(env, 1012);
                        case DialogAction.SETPRO1:
                            PlayQuestMovie(env, 370);
                            return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 798208, 1, 2); // 1
                    }
                    break;
                case 798208: // Melleas
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (qs.GetQuestVarById(0) == 3)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SET_SUCCEED:
                            return DefaultCloseDialog(env, 3, 3, true, false);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798190) // Rosina
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
        return DefaultFollowEndEvent(env, 2, 3, false); // 2
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 2, 1, false); // 0
    }
}
