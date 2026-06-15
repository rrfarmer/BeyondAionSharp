using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Estrayl
/// </summary>
public class _30710TheGreatRelease : AbstractQuestHandler
{
    private const int QUEST_ITEM_ID = 182213262; // Yustiel's Protection
    private const int START_END_NPC_ID = 804870; // Monroe
    private const int STATUE_NPC_ID = 701498; // Elyos Hero's Statue

    public _30710TheGreatRelease() : base(30710)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(START_END_NPC_ID).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(START_END_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterCanAct(questId, STATUE_NPC_ID);
        qe.RegisterQuestNpc(STATUE_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(QUEST_ITEM_ID, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == START_END_NPC_ID)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env, QUEST_ITEM_ID, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == STATUE_NPC_ID)
            {
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == START_END_NPC_ID)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnCanAct(QuestEnv env, QuestActionType questEventType, params object[] objects)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(env.GetQuestId());
        return qs != null && qs.GetStatus() == QuestStatus.START && env.GetTargetId() == STATUE_NPC_ID
            && player.GetInventory().GetItemCountByItemId(QUEST_ITEM_ID) > 0;
    }
}
