using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Atomics, Gigi, Majka
/// </summary>
public class _2288MoneyWhereYourMouthIs : AbstractQuestHandler
{
    private const int questStartNpcId = 203621; // Shania

    public _2288MoneyWhereYourMouthIs() : base(2288)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(questStartNpcId).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(questStartNpcId).AddOnTalkEvent(questId);
        // Register kill events for each monster
        int[] mobs = { 210436, 210437, 210440, 210564, 210581, 210584 };
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
        qe.RegisterOnQuestTimerEnd(questId);
        qe.RegisterOnLogOut(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npcObj)
        {
            targetId = npcObj.GetNpcId();
        }

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (targetId == questStartNpcId)
        {
            if (qs == null || qs.IsStartable())
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                switch (dialogActionId)
                {
                    case DialogAction.SELECT_QUEST_REWARD:
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        QuestService.QuestTimerEnd(env);
                        return SendQuestDialog(env, 5);
                    case DialogAction.QUEST_SELECT:
                        if (qs.GetQuestVarById(0) == 4)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        else if (qs.GetQuestVarById(0) == 0)
                        {
                            return SendQuestDialog(env, 1003);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        QuestService.QuestTimerStart(env, 600);
                        qs.SetQuestVarById(0, 1);
                        UpdateQuestStatus(env);
                        return SendQuestSelectionDialog(env);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npcObj)
        {
            targetId = npcObj.GetNpcId();
        }

        switch (targetId)
        {
            case 210436:
            case 210437:
            case 210440:
            case 210564:
            case 210581:
            case 210584:
                if (var > 0 && var < 3)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (var == 3)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
        }
        return false;
    }

    // On time end if not in reward status delete the quest
    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            QuestService.AbandonQuest(player, questId);
            return true;
        }
        return false;
    }

    // On logout if not in reward status delete the quest
    public override bool OnLogOutEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            QuestService.AbandonQuest(player, questId);
            return true;
        }
        return false;
    }
}
