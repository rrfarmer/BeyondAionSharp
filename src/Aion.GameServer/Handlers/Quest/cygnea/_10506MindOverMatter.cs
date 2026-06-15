using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// @ToCheck: correct behaviour of Corridor and spawn.
/// </summary>
public class _10506MindOverMatter : AbstractQuestHandler
{
    public _10506MindOverMatter() : base(10506)
    {
    }

    public override void Register()
    {
        // Beritra Invasion Corridor 702666, 702667 (fake)
        // Brunte 804709
        // Noep 804710
        int[] npcs = { 702666, 804709, 804710 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        int[] mobs = { 236259, 236263 };
        foreach (int mob in mobs)
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("LF5_SENSORYAREA_Q10506_206365_5_210070000"), questId); // Beritra Invasion Corridor zone
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnInvisibleTimerEnd(questId);
        qe.RegisterOnLogOut(questId);
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

        switch (targetId)
        {
            case 804709: // Brunte
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Brunte at Aequis Outpost.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1011);
                        }

                        if (dialogActionId == DialogAction.SETPRO1)
                        {
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }
                }

                if (qs.GetStatus() == QuestStatus.REWARD)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    return SendQuestEndDialog(env);
                }
                break;
            case 804710: // Noep
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 1)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1352);
                        }

                        if (dialogActionId == DialogAction.SETPRO2)
                        {
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }

                    if (var == 5)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2716);
                        }

                        if (dialogActionId == DialogAction.SETPRO6)
                        {
                            Npc npc = (Npc)env.GetVisibleObject();
                            // Spawn Noep's Ego for two minutes
                            SpawnTemporarily(236263, npc.GetWorldMapInstance(), npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading(), 2);
                            QuestService.InvisibleTimerStart(env, 120);
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }

                    if (var == 7)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 3398);
                        }

                        if (dialogActionId == DialogAction.SET_SUCCEED)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            qs.SetQuestVar(var + 1);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                        }
                    }
                }
                break;
            case 702666: // Beritra Invasion Corridor
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        if (var == 4)
                        {
                            TeleportService.TeleportTo(player, 210070000, 2837f, 2991f, 680f, (byte)67, TeleportAnimation.FADE_OUT_BEAM);
                            qs.SetQuestVar(var + 1);
                            UpdateQuestStatus(env);
                        }
                    }
                }
                return true;
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName) // Step 2: Look for the Beritra Invasion Corridor.
    {
        if (zoneName == ZoneName.Get("LF5_SENSORYAREA_Q10506_206365_5_210070000"))
        {
            Player player = env.GetPlayer();
            if (player == null)
            {
                return false;
            }

            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);

                if (var == 2)
                {
                    qs.SetQuestVar(var + 1);
                    UpdateQuestStatus(env);
                }
            }
            return true;
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
        int targetId = env.GetTargetId();

        switch (targetId)
        {
            case 236259:
                if (var == 3) // Step 3: Eliminate Beritra Invasion Spelltongues (2).
                {
                    int varKill = qs.GetQuestVarById(1); // Kill counter
                    qs.SetQuestVarById(1, varKill + 1); // 1-2 with varNum 1
                    if (varKill >= 1)
                    {
                        qs.SetQuestVar(var + 1);
                    }
                    UpdateQuestStatus(env);
                    return true;
                }
                return false;
            case 236263:
                if (var == 6) // Step 6: Subdue Noep's Ego (1).
                {
                    qs.SetQuestVarById(1, 1);
                    UpdateQuestStatus(env);

                    qs.SetQuestVar(var + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
        }
        return false;
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        return RestoreQuestStep(env);
    }

    public override bool OnInvisibleTimerEndEvent(QuestEnv env)
    {
        return RestoreQuestStep(env);
    }

    private bool RestoreQuestStep(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (var == 6)
        {
            qs.SetQuestVar(var - 1);
            UpdateQuestStatus(env);
        }
        return true;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 10500);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 10500);
    }
}
