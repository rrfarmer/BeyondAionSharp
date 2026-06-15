using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3970KinahDiggingDaughter : AbstractQuestHandler
{
    public _3970KinahDiggingDaughter() : base(3970)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203893).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798072).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(279020).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798053).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798386).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203893)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env, 182206112, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798072)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    RemoveQuestItem(env, 182206112, 1);
                    GiveQuestItem(env, 182206113, 1);
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            if (targetId == 279020)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        return SendQuestDialog(env, 1693);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    RemoveQuestItem(env, 182206113, 1);
                    GiveQuestItem(env, 182206114, 1);
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            if (targetId == 798053)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                    {
                        return SendQuestDialog(env, 2034);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    RemoveQuestItem(env, 182206114, 1);
                    GiveQuestItem(env, 182206115, 1);
                    qs.SetQuestVar(3);
                    return DefaultCloseDialog(env, 3, 3, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798386)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                RemoveQuestItem(env, 182206115, 1);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
