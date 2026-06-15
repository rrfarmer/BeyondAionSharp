using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2916ManInTheLongBlackRobe : AbstractQuestHandler
{
    public _2916ManInTheLongBlackRobe() : base(2916)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204141).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204141).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204152).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204150).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798033).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203673).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700211).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700211).AddOnAtDistanceEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204141)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 204152)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 204150)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 204151)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                        return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    return DefaultCloseDialog(env, 2, 3);
                }
            }
            else if (targetId == 798033)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 3)
                        return SendQuestDialog(env, 2375);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    return DefaultCloseDialog(env, 3, 4);
                }
            }
            else if (targetId == 203673)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 4)
                        return SendQuestDialog(env, 2716);
                }
                else if (dialogActionId == DialogAction.SETPRO5)
                {
                    return DefaultCloseDialog(env, 4, 5);
                }
            }
            else if (targetId == 700211)
            {
                if (qs.GetQuestVarById(0) == 6)
                    return true;
            }
            else if (targetId == 204141)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 6)
                        return SendQuestDialog(env, 3057);
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    return CheckQuestItems(env, 6, 6, true, 5, 3143);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204141)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 5);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnAtDistanceEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 5)
            {
                ChangeQuestStep(env, 5, 6);
                return true;
            }
        }
        return false;
    }
}
