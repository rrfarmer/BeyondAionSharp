using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu, Majka
/// </summary>
public class _24020AegirsOrders : AbstractQuestHandler
{
    public _24020AegirsOrders() : base(24020)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204301).AddOnTalkEvent(questId);
        qe.RegisterOnEnterWorld(questId);
        qe.RegisterOnLevelChanged(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (targetId != 204301)
            return false;
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (dialogActionId == DialogAction.QUEST_SELECT)
            {
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                return SendQuestDialog(env, 1011);
            }
            else
                return SendQuestStartDialog(env);
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        if (player.GetWorldId() == WorldMapType.MORHEIM.GetId() && !player.GetQuestStateList().HasQuest(questId))
            return QuestService.StartQuest(env);
        return false;
    }

    public override void OnLevelChangedEvent(Player player)
    {
        OnEnterWorldEvent(new QuestEnv(null, player, questId));
    }
}
