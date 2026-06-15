using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _4038AlasPoorGroznak : AbstractQuestHandler
{
    public _4038AlasPoorGroznak() : base(4038)
    {
    }

    public override void Register()
    {
        int[] npcs = { 205150, 730155, 700380, 700381, 700382 };
        qe.RegisterQuestNpc(205150).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205150) // Surt
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 730155: // Groznak's Skull
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            else if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 1, 2, false, 10000, 10001); // 2
                        case DialogAction.SETPRO3:
                            return DefaultCloseDialog(env, 2, 2, true, false); // reward
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                }
                case 700380: // Weathered Skeleton
                    if (var == 1)
                    {
                        return true; // loot
                    }
                    break;
                case 700381: // Intact Skeleton
                    if (var == 1)
                    {
                        return true; // loot
                    }
                    break;
                case 700382: // Muddy Skeleton
                    if (var == 1)
                    {
                        return true; // loot
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205150) // Surt
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
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
}
