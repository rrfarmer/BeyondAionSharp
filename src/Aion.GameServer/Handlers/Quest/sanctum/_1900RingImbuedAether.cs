using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1900RingImbuedAether : AbstractQuestHandler
{
    public _1900RingImbuedAether() : base(1900)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203757).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203757).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203739).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203766).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203797).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203795).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203830).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        if (SendQuestNoneDialog(env, 203757, 182206003, 1))
            return true;

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        if (env.GetTargetId() == 203739)
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (env.GetTargetId() == 203766)
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 2);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (env.GetTargetId() == 203797)
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    return DefaultCloseDialog(env, 2, 3);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (env.GetTargetId() == 203795)
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 3)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SETPRO4)
                {
                    return DefaultCloseDialog(env, 3, 0, true, false);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (env.GetTargetId() == 203830)
        {
            if (env.GetDialogActionId() == DialogAction.USE_OBJECT && qs.GetStatus() == QuestStatus.REWARD)
                return SendQuestDialog(env, 2716);
            else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
            {
                RemoveQuestItem(env, 182206003, 1);
                return SendQuestDialog(env, 5);
            }
            else
                return SendQuestEndDialog(env);
        }
        return SendQuestRewardDialog(env, 203830, 0);
    }
}
