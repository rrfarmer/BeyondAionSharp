using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Talk with Sefrim.
/// Talk with Julia.
/// Investigate the Mindboggle Waste.
/// Eliminate the Enigmatic Drakan who appears.
/// Defeat the Interhypno Ego.
/// Talk with Jadun.
/// Talk with Julia.
/// Talk with Sefrim.
/// Order: Sefrim is looking for you. Go and see him.
/// @author Majka, Pad
/// </summary>
public class _20506MuscleOverMind : AbstractQuestHandler
{
    public _20506MuscleOverMind() : base(20506)
    {
    }

    public override void Register()
    {
        // Sefrim 804736
        // Julia 804737
        // Jadun 804743
        int[] npcs = { 804736, 804737, 804743 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        int[] mobs = { 219956, 219957 };
        foreach (int mob in mobs)
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("DF5_SENSORYAREA_Q20506A_206376_2_220080000"), questId); // Mindboggle Waste zone
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
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
            case 804736: // Sefrim
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Sefrim.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.SETPRO1)
                            return DefaultCloseDialog(env, var, var + 1);
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
            case 804737: // Julia
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 1) // Step 1: Talk with Julia.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);

                        if (dialogActionId == DialogAction.SETPRO2)
                            return DefaultCloseDialog(env, var, var + 1);
                    }

                    if (var == 6) // Step 6: Talk with Julia.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 3057);

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
            case 804743: // Jadun
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 5) // Step 5: Talk with Jadun.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2716);

                        if (dialogActionId == DialogAction.SETPRO6)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                }
                break;
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName == ZoneName.Get("DF5_SENSORYAREA_Q20506A_206376_2_220080000"))
        {
            Player player = env.GetPlayer();
            if (player == null)
                return false;

            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);

                if (var == 2) // Step 2: Investigate the Mindboggle Waste.
                {
                    qs.SetQuestVar(var + 1);
                    UpdateQuestStatus(env);
                    SpawnTemporarily(219956, player.GetWorldMapInstance(), 1938.0f, 83.9f, 235.0f, (byte)90, 10);
                    ThreadPoolManager.GetInstance().Schedule(ct =>
                    {
                        RestoreQuestStep(env);
                        return ValueTask.CompletedTask;
                    }, 600000L);
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
            case 219956:
                if (var == 3) // Step 3: Eliminate the Enigmatic Drakan who appears.
                {
                    qs.SetQuestVar(var + 1);
                    UpdateQuestStatus(env);
                    SpawnTemporarily(219957, player.GetWorldMapInstance(), 1938.0f, 83.9f, 235.0f, (byte)90, 8);
                    return true;
                }
                break;
            case 219957:
                if (var == 4) // Step 4: Defeat the Interhypno Ego.
                {
                    qs.SetQuestVar(var + 1);
                    UpdateQuestStatus(env);
                    SpawnTemporarily(804743, player.GetWorldMapInstance(), 1938.0f, 83.9f, 235.0f, (byte)90, 6); // Jadun
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

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 20500);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 20500);
    }

    private bool RestoreQuestStep(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        if (var >= 3 && var <= 5)
        {
            qs.SetQuestVar(2);
            UpdateQuestStatus(env);
        }
        return true;
    }
}
