using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _28213TheColiseumSecret : AbstractQuestHandler
{
    public _28213TheColiseumSecret() : base(28213)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182212223, questId);
        qe.RegisterQuestNpc(205986).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205986).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(205320).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798804).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205986)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestStartDialog(env, 182212222, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 205320)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SELECT2_1:
                        RemoveQuestItem(env, 182212222, 1);
                        GiveQuestItem(env, 182212223, 1);
                        return SendQuestDialog(env, 1353);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 798804)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        else if (var == 3)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2);
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 3, 3, true);
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798804)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
        {
            RemoveQuestItem(env, 182212223, 1);
            ChangeQuestStep(env, 2, 3);
            return HandlerResult.SUCCESS;
        }
        return HandlerResult.FAILED;
    }
}
