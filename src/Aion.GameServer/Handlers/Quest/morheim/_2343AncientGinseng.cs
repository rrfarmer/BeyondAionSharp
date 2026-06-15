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
public class _2343AncientGinseng : AbstractQuestHandler
{
    public _2343AncientGinseng() : base(2343)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182204134, questId);
        qe.RegisterQuestNpc(700243).AddOnTalkEvent(questId);
        qe.RegisterCanAct(questId, 700243);
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
            if (targetId == 700243)
            {
                SpawnForFiveMinutes(212814, player.GetWorldMapInstance(), 1008.36f, 481.88f, 509.3f, (byte)60);
                RemoveQuestItem(env, 182204134, 1);
                qs.SetStatus(QuestStatus.REWARD);
                QuestService.FinishQuest(env);
                return true;
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
