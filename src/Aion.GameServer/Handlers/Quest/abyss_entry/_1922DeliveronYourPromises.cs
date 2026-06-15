using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Hellboy, aion4Free, Gigi, Rolandas, vlog
/// </summary>
public class _1922DeliveronYourPromises : AbstractQuestHandler
{
    public _1922DeliveronYourPromises() : base(1922)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnQuestTimerEnd(questId);
        qe.RegisterOnEnterWorld(questId);
        qe.RegisterQuestNpc(203830).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203901).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203764).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(213582).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(213580).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(213581).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203830: // Fuchsia
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO12:
                            return DefaultCloseDialog(env, 0, 4); // 4
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                case 203901: // Telemachus
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 7)
                                return SendQuestDialog(env, 3739);
                            return false;
                        // Should be removed (it's for backward compatibility)
                        // Epeios sets REWARD status now and doesn't reach this part
                        case DialogAction.SELECT_QUEST_REWARD:
                            if (var == 7)
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 6);
                            }
                            break;
                    }
                    break;
                case 203764: // Epeios
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 6)
                            {
                                return SendQuestDialog(env, 1779);
                            }
                            return false;
                        case DialogAction.QUEST_SELECT:
                            if (var == 4)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            else if (qs.GetQuestVarById(4) == 10)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.SANCTUM_UNDERGROUND_ARENA.GetId(), player);
                            TeleportService.TeleportTo(player, newInstance, 276, 293, 163, (byte)90, TeleportAnimation.NONE);
                            if (var == 4 || var == 6)
                                ChangeQuestStep(env, var, 5, false); // 5
                            return CloseDialogWindow(env);
                        case DialogAction.SETPRO4:
                            qs.SetQuestVarById(0, 7);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return DefaultCloseDialog(env, 7, 7); // 7
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203901)
            { // Telemachus
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 3739);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 6);
                else
                {
                    qs.SetRewardGroup(1); // always reward group 1 since other choices than arena are not available anymore
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
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 5)
            {
                int var4 = qs.GetQuestVarById(4);
                int[] mobs = { 213580, 213581, 213582 };
                if (var4 < 9)
                {
                    return DefaultOnKillEvent(env, mobs, 0, 9, 4); // 4: 1 - 9
                }
                else if (var4 == 9)
                {
                    DefaultOnKillEvent(env, mobs, 9, 10, 4); // 4: 10
                    QuestService.QuestTimerEnd(env);
                    PlayQuestMovie(env, 166);
                    return true;
                }
            }
        }
        return false;
    }

    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var4 = qs.GetQuestVarById(4);
            if (var4 < 10)
            {
                qs.SetQuestVar(6);
                UpdateQuestStatus(env);
                TeleportService.TeleportTo(player, 110010000, 1466.036f, 1337.2749f, 566.41583f, (byte)86);
                return true;
            }
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            int var4 = qs.GetQuestVars().GetVarById(4);
            if (var == 5 && var4 != 10)
            {
                if (player.GetWorldId() != 310080000)
                {
                    QuestService.QuestTimerEnd(env);
                    qs.SetQuestVar(6);
                    UpdateQuestStatus(env);
                    return true;
                }
                else
                {
                    PlayQuestMovie(env, 165);
                    QuestService.QuestTimerStart(env, 240);
                    return true;
                }
            }
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId == 166)
        {
            TeleportService.TeleportTo(env.GetPlayer(), 110010000, 1466.036f, 1337.2749f, 566.41583f, (byte)86);
        }
        else if (movieId == 165)
        {
            QuestService.QuestTimerStart(env, 240);
        }
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 1921);
    }
}
