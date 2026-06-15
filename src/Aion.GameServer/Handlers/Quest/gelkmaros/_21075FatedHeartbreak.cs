using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _21075FatedHeartbreak : AbstractQuestHandler
{
    public _21075FatedHeartbreak() : base(21075)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799409).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799409).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798392).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799410).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204138).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799409)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798392)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    GiveQuestItem(env, 182207917, 1);
                    return DefaultCloseDialog(env, 0, 1);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    GiveQuestItem(env, 182207917, 1);
                    return DefaultCloseDialog(env, 0, 2);
                }
            }
            else if (targetId == 799410)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    qs.SetRewardGroup(0);
                    RemoveQuestItem(env, 182207917, 1);
                    return DefaultCloseDialog(env, 1, 1, true, false);
                }
            }
            else if (targetId == 204138)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                        return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    qs.SetRewardGroup(1);
                    RemoveQuestItem(env, 182207917, 1);
                    return DefaultCloseDialog(env, 2, 2, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799409)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
