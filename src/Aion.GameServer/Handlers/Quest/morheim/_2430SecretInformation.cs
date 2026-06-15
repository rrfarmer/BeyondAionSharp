using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Nephis, vlog
/// </summary>
public class _2430SecretInformation : AbstractQuestHandler
{
    public _2430SecretInformation() : base(2430)
    {
    }

    public override void Register()
    {
        int[] npcs = { 204327, 204377, 205244, 798081, 798082, 204300 };
        qe.RegisterQuestNpc(204327).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204327)
            { // Sveinn
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_REFUSE_1:
                        return SendQuestDialog(env, 1004);
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestDialog(env, 1003);
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                    case DialogAction.SETPRO1:
                        if (player.GetInventory().GetKinah() >= 500)
                        {
                            if (QuestService.StartQuest(env))
                            {
                                player.GetInventory().DecreaseKinah(500);
                                ChangeQuestStep(env, 0, 1); // 1
                                return SendQuestDialog(env, 1352);
                            }
                        }
                        else
                        {
                            return SendQuestDialog(env, 1267);
                        }
                        return false;
                    case DialogAction.SETPRO3:
                        if (player.GetInventory().GetKinah() >= 5000)
                        {
                            if (QuestService.StartQuest(env))
                            {
                                player.GetInventory().DecreaseKinah(5000);
                                ChangeQuestStep(env, 0, 3); // 3
                                return SendQuestDialog(env, 2034);
                            }
                        }
                        else
                        {
                            return SendQuestDialog(env, 1267);
                        }
                        return false;
                    case DialogAction.SETPRO7:
                        if (player.GetInventory().GetKinah() >= 50000)
                        {
                            if (QuestService.StartQuest(env))
                            {
                                player.GetInventory().DecreaseKinah(50000);
                                ChangeQuestStep(env, 0, 7); // 7
                                return SendQuestDialog(env, 3398);
                            }
                        }
                        else
                        {
                            return SendQuestDialog(env, 1267);
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 204327: // Sveinn
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            switch (var)
                            {
                                case 1:
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                case 3:
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                case 7:
                                {
                                    return SendQuestDialog(env, 3398);
                                }
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2, 182204221, 1, 0, 0); // 2
                        case DialogAction.SETPRO4:
                            return DefaultCloseDialog(env, 3, 4); // 4
                        case DialogAction.SETPRO8:
                            return DefaultCloseDialog(env, 7, 8); // 8
                    }
                    break;
                case 204377: // Grall
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            RemoveQuestItem(env, 182204221, 1);
                            ChangeQuestStep(env, 2, 2, true);
                            qs.SetRewardGroup(0);
                            return SendQuestEndDialog(env);
                    }
                    break;
                case 205244: // Hugorunerk
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 4)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SETPRO5:
                            return DefaultCloseDialog(env, 4, 5); // 5
                    }
                    break;
                case 798081: // Nicoyerk
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 5)
                            {
                                return SendQuestDialog(env, 2716);
                            }
                            return false;
                        case DialogAction.SETPRO6:
                            return DefaultCloseDialog(env, 5, 6); // 6
                    }
                    break;
                case 798082: // Bicorunerk
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 6)
                            {
                                return SendQuestDialog(env, 3057);
                            }
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 6, 6, true);
                            qs.SetRewardGroup(1);
                            return SendQuestEndDialog(env);
                    }
                    break;
                case 204300: // Bolverk
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 8)
                            {
                                if (player.GetInventory().GetItemCountByItemId(182204222) > 0)
                                {
                                    return SendQuestDialog(env, 3739);
                                }
                                else
                                {
                                    return SendQuestDialog(env, 3825);
                                }
                            }
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            RemoveQuestItem(env, 182204222, 1);
                            ChangeQuestStep(env, 8, 8, true);
                            qs.SetRewardGroup(2);
                            return SendQuestEndDialog(env);
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 204377: // Grall
                    if (var == 2)
                        return SendQuestEndDialog(env);
                    break;
                case 798082: // Bicorunerk
                    if (var == 6)
                        return SendQuestEndDialog(env);
                    break;
                case 204300: // Bolverk
                    if (var == 8)
                        return SendQuestEndDialog(env);
                    break;
            }
        }
        return false;
    }
}
