using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _24010SuthransOrders : AbstractQuestHandler
{
    public _24010SuthransOrders() : base(24010)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203557).AddOnTalkEvent(questId);
        qe.RegisterOnEnterWorld(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        if (targetId != 203557)
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
        if (player.GetWorldId() == WorldMapType.ALTGARD.GetId() && !player.GetQuestStateList().HasQuest(questId))
            return QuestService.StartQuest(env);
        return false;
    }
}
