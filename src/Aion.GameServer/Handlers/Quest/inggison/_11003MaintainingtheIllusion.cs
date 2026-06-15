using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi
/// </summary>
public class _11003MaintainingtheIllusion : AbstractQuestHandler
{
    public _11003MaintainingtheIllusion() : base(11003)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798933).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798942).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc) env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798933)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798942:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182206701);
                            long itemCount2 = player.GetInventory().GetItemCountByItemId(182206702);
                            if (itemCount1 >= 6 && itemCount2 >= 4)
                            {
                                RemoveQuestItem(env, 182206701, 6);
                                RemoveQuestItem(env, 182206702, 4);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 2375);
                            }
                            else
                                return SendQuestDialog(env, 2716);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798942)
            {
                if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
