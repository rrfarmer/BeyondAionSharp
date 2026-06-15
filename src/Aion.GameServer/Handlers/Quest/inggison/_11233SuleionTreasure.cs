using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _11233SuleionTreasure : AbstractQuestHandler
{
    public _11233SuleionTreasure() : base(11233)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182206875, questId);
        qe.RegisterQuestNpc(799075).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798976).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798948).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 0)
            {
                if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799075)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    GiveQuestItem(env, 182206876, 1);
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 798976)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    GiveQuestItem(env, 182206877, 1);
                    qs.SetQuestVar(2);
                    return DefaultCloseDialog(env, 2, 2, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798948)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                RemoveQuestItem(env, 182206875, 1);
                RemoveQuestItem(env, 182206876, 1);
                RemoveQuestItem(env, 182206877, 1);
                return SendQuestEndDialog(env);
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
