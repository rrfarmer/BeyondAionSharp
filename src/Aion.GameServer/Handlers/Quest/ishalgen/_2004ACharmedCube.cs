using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, vlog, Majka
/// </summary>
public class _2004ACharmedCube : AbstractQuestHandler
{
    private int[] mobs = { 210402, 210403 };

    public _2004ACharmedCube() : base(2004)
    {
    }

    public override void Register()
    {
        int[] npcs = { 203539, 700047, 203550 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203539: // Derot
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
                        case DialogAction.SETPRO2:
                            GiveQuestItem(env, 182203005, 1);
                            return SendQuestSelectionDialog(env);
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 1, 2, false, 1438, 1353); // 2
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                case 700047: // Tombstone
                    if (var == 1 && env.GetVisibleObject().GetObjectTemplate().GetTemplateId() == 700047 && dialogActionId == DialogAction.USE_OBJECT)
                    {
                        SpawnForFiveMinutesInFront(211755, env.GetVisibleObject(), env.GetVisibleObject().GetHeading(), 2);
                        return true;
                    }
                    return false;
                case 203550: // Munin
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            else if (var == 6)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            return DefaultCloseDialog(env, 2, 3, 0, 0, 182203005, 1); // 3
                        case DialogAction.SETPRO4:
                            return DefaultCloseDialog(env, 6, 6, true, false); // reward
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203539)
            { // Derot
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, mobs, 3, 6); // 3 - 6
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 2100);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 2100);
    }
}
