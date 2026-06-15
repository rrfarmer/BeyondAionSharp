using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Talk with Maochinicherk (798068). Bring the Glossy Aether Paper (186000091) and Kinah (50000) to Ninis (798385).
///
/// @author undertrey, vlog
/// </summary>
public class _4967GrowthNinissSecondCharm : AbstractQuestHandler
{
    public _4967GrowthNinissSecondCharm() : base(4967)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798385).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798385).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798068).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798385)
            { // Ninis
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env, 182207137, 1);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 798068: // Maochinicherk
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1, 0, 0, 182207137, 1); // 1
                    }
                    break;
                case 798385: // Ninis
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                RemoveQuestItem(env, 182207137, 1);
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            long itemAmount = player.GetInventory().GetItemCountByItemId(186000091);
                            if (var == 1 && itemAmount >= 1 && player.GetInventory().TryDecreaseKinah(50000))
                            {
                                RemoveQuestItem(env, 186000091, 1);
                                ChangeQuestStep(env, 1, 1, true); // reward
                                return SendQuestDialog(env, 5);
                            }
                            else
                                return SendQuestDialog(env, 2716);
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, 1, 1);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798385)
            { // Ninis
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
