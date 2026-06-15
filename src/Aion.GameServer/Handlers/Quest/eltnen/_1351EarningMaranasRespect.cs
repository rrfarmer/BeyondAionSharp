using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Atomics
/// </summary>
public class _1351EarningMaranasRespect : AbstractQuestHandler
{
    public _1351EarningMaranasRespect() : base(1351)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203965).AddOnQuestStart(questId); // Castor
        qe.RegisterQuestNpc(203965).AddOnTalkEvent(questId); // Castor
        qe.RegisterQuestNpc(203983).AddOnTalkEvent(questId); // Marana
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        long itemCount;
        if (targetId == 203965)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203983)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    itemCount = player.GetInventory().GetItemCountByItemId(182201321);
                    if (itemCount > 9)
                    {
                        RemoveQuestItem(env, 182201321, 10);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 5);
                    }
                    else
                        return SendQuestDialog(env, 2716);
                }
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
