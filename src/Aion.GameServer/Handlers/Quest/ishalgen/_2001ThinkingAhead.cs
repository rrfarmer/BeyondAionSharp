using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog, Majka
/// </summary>
public class _2001ThinkingAhead : AbstractQuestHandler
{
    private int[] mobs = { 210369, 210368 };

    public _2001ThinkingAhead() : base(2001)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203518).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700093).AddOnTalkEvent(questId);
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
            if (targetId == 203518)
            { // Boromer
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
                        else if (var == 2)
                        {
                            return SendQuestDialog(env, 1694);
                        }
                        break;
                    case DialogAction.SELECT1_1:
                        PlayQuestMovie(env, 51);
                        return SendQuestDialog(env, 1012);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1); // 1
                    case DialogAction.SETPRO3:
                        return DefaultCloseDialog(env, 2, 3); // 3
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 1, 2, false, 1694, 1693);
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                }
            }
            else if (targetId == 700093)
            {
                return true; // just give quest drop on use
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203518)
            { // Boromer
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2034);
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
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        if (var >= 3 && var < 8)
        {
            return DefaultOnKillEvent(env, mobs, 3, 8); // 3 - 8
        }
        else if (var == 8)
        {
            return DefaultOnKillEvent(env, mobs, 8, true); // reward
        }
        return false;
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
