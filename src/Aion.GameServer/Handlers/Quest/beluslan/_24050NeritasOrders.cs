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
public class _24050NeritasOrders : AbstractQuestHandler
{
    public _24050NeritasOrders() : base(24050)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204702).AddOnTalkEvent(questId);
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

        if (targetId != 204702)
            return false;
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (dialogActionId == DialogAction.QUEST_SELECT)
                return SendQuestDialog(env, 10002);
            else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
            {
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                return SendQuestDialog(env, 5);
            }
            return false;
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
        if (player.GetWorldId() == WorldMapType.BELUSLAN.GetId() && !player.GetQuestStateList().HasQuest(questId))
            return QuestService.StartQuest(env);
        return false;
    }

    public override void OnLevelChangedEvent(Player player)
    {
        OnEnterWorldEvent(new QuestEnv(null, player, questId));
    }
}
