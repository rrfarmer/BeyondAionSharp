using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rikka
/// </summary>
public class _30264ANecklacewithHistory : AbstractQuestHandler
{
    public _30264ANecklacewithHistory() : base(30264)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203706).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182209802, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        if (qs == null || qs.IsStartable())
        {
            return false;
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203706) // Charna
            {
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
            if (QuestService.StartQuest(env))
            {
                ChangeQuestStep(env, 0, 0, true); // reward
                return HandlerResult.SUCCESS;
            }
        }
        return HandlerResult.FAILED;
    }
}
