using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

public class _1373WaterTherapy : AbstractQuestHandler
{
    public _1373WaterTherapy() : base(1373)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203949).AddOnQuestStart(questId); // Aerope
        qe.RegisterQuestNpc(203949).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182201372, questId);
        qe.RegisterOnQuestTimerEnd(questId);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (player.IsInsideItemUseZone(ZoneName.Get("LF2_ITEMUSEAREA_Q1373")))
            {
                RemoveQuestItem(env, 182201372, 1);
                UseQuestItem(env, item, 0, 2, false, 182201373, 1, 0);
                QuestService.QuestTimerStart(env, 180);
                return HandlerResult.SUCCESS;
            }
        }
        return HandlerResult.FAILED;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        int dialogActionId = env.GetDialogActionId();
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203949) // Aerope
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    if (!GiveQuestItem(env, 182201372, 1))
                        return true;
                    return SendQuestStartDialog(env);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203949:
                    if (qs.GetQuestVarById(0) == 2)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182201373) == 1)
                            {
                                QuestService.QuestTimerEnd(env);
                                return CheckQuestItems(env, 2, 3, true, 5, 2716);
                            }
                        }
                        else
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203949)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            RemoveQuestItem(env, 182201373, 1);
            QuestService.AbandonQuest(player, questId);
            return true;
        }
        return false;
    }
}
