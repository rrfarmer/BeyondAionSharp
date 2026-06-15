using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Majka
/// </summary>
public class _10033PetrifiedSubside : AbstractQuestHandler
{
    public _10033PetrifiedSubside() : base(10033)
    {
    }

    public override void Register()
    {
        // Pomponia ID: 798970
        // Sulla ID: 798975
        // Philon ID: 798981
        // Western Petrified Mass ID: 730226
        // Eastern Petrified Mass ID: 730227
        // Southern Petrified Mass ID: 730228
        int[] npcs = { 798970, 798975, 798981, 730226, 730227, 730228 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            return false;
        }
        int dialogActionId = env.GetDialogActionId();
        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798970: // Pomponia
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                    }
                    break;
                case 798975: // Sulla
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            else if (var == 6)
                            {
                                return SendQuestDialog(env, 3057);
                            }
                            break;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2);
                        case DialogAction.SET_SUCCEED:
                            if (var == 6)
                            {
                                RemoveQuestItem(env, 182215625, 1);
                                qs.SetQuestVar(var + 1); // 11
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                            }
                            break;
                    }
                    break;
                case 798981: // Philon
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            GiveQuestItem(env, 182215622, 1);
                            return DefaultCloseDialog(env, 2, 3);
                    }
                    break;
                case 730226: // Western Petrified Mass
                    if (var == 3)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2034);
                        }

                        if (dialogActionId == DialogAction.SETPRO4)
                        {
                            RemoveQuestItem(env, 182215622, 1);
                            GiveQuestItem(env, 182215623, 1);
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }
                    break;
                case 730227: // Eastern Petrified Mass
                    if (var == 4)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2375);
                        }

                        if (dialogActionId == DialogAction.SETPRO5)
                        {
                            RemoveQuestItem(env, 182215623, 1);
                            GiveQuestItem(env, 182215624, 1);
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }
                    break;
                case 730228: // Southern Petrified Mass
                    if (var == 5)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2716);
                        }

                        if (dialogActionId == DialogAction.SETPRO6)
                        {
                            RemoveQuestItem(env, 182215624, 1);
                            GiveQuestItem(env, 182215625, 1);
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798970)
            { // Pomponia
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 10031);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 10031);
    }
}
