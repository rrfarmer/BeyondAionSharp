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
public class _11056EliminationOrder : AbstractQuestHandler
{
    public _11056EliminationOrder() : base(11056)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182206842, questId);
        qe.RegisterQuestNpc(799043).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(296493).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(296494).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(296495).AddOnKillEvent(questId);
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
                    RemoveQuestItem(env, 182206842, 1);
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799043)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else if (dialogActionId == DialogAction.SELECT4)
                {
                    return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    if (player.GetInventory().GetKinah() >= 10000000)
                    {
                        player.GetInventory().DecreaseKinah(10000000);
                        return SendQuestDialog(env, 5);
                    }
                    else
                        return SendQuestDialog(env, 3739);
                }
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 0)
                return DefaultOnKillEvent(env, 296493, 0, 1);
            else if (var == 1)
                return DefaultOnKillEvent(env, 296494, 1, 2);
            else if (var == 2)
                return DefaultOnKillEvent(env, 296495, 2, true);
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
