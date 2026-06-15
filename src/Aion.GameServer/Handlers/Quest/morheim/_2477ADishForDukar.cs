using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2477ADishForDukar : AbstractQuestHandler
{
    public _2477ADishForDukar() : base(2477)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204355).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204355).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204100).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204355)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    ChangeQuestStep(env, 0, 1);
                    return SendQuestDialog(env, 1003);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 204355)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    return CheckItemExistence(env, 1, 2, false, 182204196, 5, true, 10000, 10001, 182204234, 1);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                    return DefaultCloseDialog(env, 1, 2);
            }
            else if (targetId == 204100)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    RemoveQuestItem(env, 182204234, 1);
                    GiveQuestItem(env, 182204197, 1);
                    return DefaultCloseDialog(env, 2, 3, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204355)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    RemoveQuestItem(env, 182204197, 1);
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
