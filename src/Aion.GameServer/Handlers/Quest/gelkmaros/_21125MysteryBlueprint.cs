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
public class _21125MysteryBlueprint : AbstractQuestHandler
{
    public _21125MysteryBlueprint() : base(21125)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182207865, questId);
        qe.RegisterQuestNpc(799295).AddOnTalkEvent(questId);
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
            if (targetId == 799295)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                    return DefaultCloseDialog(env, 0, 1, true, true);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799295)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                RemoveQuestItem(env, 182207865, 1);
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
