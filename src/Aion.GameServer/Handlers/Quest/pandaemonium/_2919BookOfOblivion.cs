using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2919BookOfOblivion : AbstractQuestHandler
{
    public _2919BookOfOblivion() : base(2919)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204206).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204206).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204215).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204192).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700212).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204224).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204206)
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
            if (targetId == 204215)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 204192)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 700212)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                        return SendQuestDialog(env, 2034);
                    else if (qs.GetQuestVarById(0) == 6)
                        return SendQuestDialog(env, 3057);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    ChangeQuestStep(env, 2, 3);
                    return CloseDialogWindow(env);
                }
                else if (dialogActionId == DialogAction.SETPRO7)
                {
                    GiveQuestItem(env, 182207013, 1);
                    ChangeQuestStep(env, 6, 7);
                    return CloseDialogWindow(env);
                }
            }
            else if (targetId == 204206)
            {
                if (qs.GetQuestVarById(0) == 7)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 3398);
                    }
                }
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 3)
                        return SendQuestDialog(env, 2375);
                }
                else if (dialogActionId == DialogAction.SETPRO5)
                {
                    return DefaultCloseDialog(env, 3, 4);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    RemoveQuestItem(env, 182207013, 1);
                    return DefaultCloseDialog(env, 7, 7, true, true);
                }
            }
            else if (targetId == 204224)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 4)
                        return SendQuestDialog(env, 2716);
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    return CheckQuestItems(env, 4, 6, false, 2802, 2717);
                }
                else if (dialogActionId == DialogAction.SETPRO6)
                {
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204206)
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
}
