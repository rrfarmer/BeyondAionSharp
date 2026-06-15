using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2921LoveAtFirstSight : AbstractQuestHandler
{
    public _2921LoveAtFirstSight() : base(2921)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204261).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204261).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204256).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204192).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204130).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204261)
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
            if (targetId == 204256)
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
            else if (targetId == 204192)
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
            else if (targetId == 204130)
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
            else if (targetId == 204261)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 3)
                        return SendQuestDialog(env, 2375);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 3, 3, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204261)
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
