using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2122AshesToAshes : AbstractQuestHandler
{
    private int[] npcs = { 203551, 700148, 730029 };

    public _2122AshesToAshes() : base(2122)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182203120, questId);
        qe.RegisterCanAct(GetQuestId(), 700148);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    return SendQuestStartDialog(env);
                }
                else if (env.GetDialogActionId() == DialogAction.QUEST_REFUSE_1)
                {
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203551)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SELECT1_1:
                        RemoveQuestItem(env, 182203120, 1);
                        return SendQuestDialog(env, 1012);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 730029)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (player.GetInventory().GetItemCountByItemId(182203133) >= 1)
                            return SendQuestDialog(env, 1352);
                        else
                            return SendQuestDialog(env, 1693);
                    case DialogAction.SELECT2_1:
                        RemoveQuestItem(env, 182203133, 1);
                        return SendQuestDialog(env, 1353);
                    case DialogAction.FINISH_DIALOG:
                        return CloseDialogWindow(env);
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 1, true, false);
                }
            }
            else if (targetId == 700148)
            {
                return true; // just give quest drop on use
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203551)
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
        if (qs == null || qs.IsStartable())
        {
            return HandlerResultExtensions.FromBoolean(SendQuestDialog(env, 4));
        }
        return HandlerResult.FAILED;
    }
}
