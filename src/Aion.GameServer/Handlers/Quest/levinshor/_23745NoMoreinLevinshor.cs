using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Pad
/// </summary>
public class _23745NoMoreinLevinshor : AbstractQuestHandler
{
    private const int npcId = 802353;
    private const int worldId = 600100000;

    public _23745NoMoreinLevinshor() : base(23745)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
        qe.RegisterOnEnterWorld(questId);
        qe.RegisterOnKillInWorld(worldId, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();

        if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcId)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (player.GetWorldId() == worldId && (qs == null || qs.IsStartable()))
            return QuestService.StartQuest(env);
        return false;
    }

    public override bool OnKillInWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.GetStatus() != QuestStatus.START || qs.GetQuestVarById(0) != 0)
            return false;

        if (!(env.GetVisibleObject() is Player target))
            return false;

        if (player.GetLevel() >= target.GetLevel() - 5 && player.GetLevel() <= target.GetLevel() + 9)
        {
            if (qs.GetQuestVarById(1) == 0)
            {
                qs.SetQuestVarById(1, 1);
            }
            else
            {
                qs.SetQuestVarById(0, 1);
                qs.SetStatus(QuestStatus.REWARD);
            }
            UpdateQuestStatus(env);
            return true;
        }

        return false;
    }
}
