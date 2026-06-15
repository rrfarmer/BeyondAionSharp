using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _2533BeritrasCurse : AbstractQuestHandler
{
    public _2533BeritrasCurse() : base(2533)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204801).AddOnQuestStart(questId); // Gigrite
        qe.RegisterQuestNpc(204801).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182204425, questId); // Empty Durable Potion Bottle
        qe.RegisterOnQuestTimerEnd(questId);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (player.IsInsideZone(ZoneName.Get("BERITRAS_WEAPON_220040000")))
            {
                QuestService.QuestTimerStart(env, 300);
                return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 0, 1, false, 182204426, 1, 0));
            }
        }
        return HandlerResult.SUCCESS; // ??
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204801)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    if (!GiveQuestItem(env, 182204425, 1))
                        return true;
                    return SendQuestStartDialog(env);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 204801)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 1352);
                        }
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        QuestService.QuestTimerEnd(env);
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204801)
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
            RemoveQuestItem(env, 182204426, 1);
            QuestService.AbandonQuest(player, questId);
            return true;
        }
        return false;
    }
}
