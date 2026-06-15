using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _20501WhattheRuinsSay : AbstractQuestHandler
{
    private static readonly int workItemId = 182215637; // Enshar Ruins Piece

    public _20501WhattheRuinsSay() : base(20501)
    {
    }

    public override void Register()
    {
        // Bonat 804720
        // Nixie 804721
        // Crumbled Ruins 731538
        // Destroyed Balaur Ruins 731539
        // Intact Balaur Ruins 731540
        // Putrid Balaur Corpse 804722
        int[] npcs = { 731538, 731539, 731540, 804720, 804721, 804722 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("DF5_SENSORYAREA_Q20501A_206375_1_220080000"), questId); // Dreg Findspot zone
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
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
            case 804720: // Bonat
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Meet with Bonat in Sky Fortress Insurgent Post.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.SETPRO1)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                }
                break;
            case 804721: // Nixie
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 1) // Step 1: Meet and talk with Nixie.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);

                        if (dialogActionId == DialogAction.SETPRO2)
                            return DefaultCloseDialog(env, var, var + 1, workItemId, 1); // Gives item Ruins Location Map [ID: 182215598]
                    }
                }

                if (qs.GetStatus() == QuestStatus.REWARD) // Step 7: Report back to Nixie.
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }

                    return SendQuestEndDialog(env);
                }
                break;
            case 731538: // Crumbled Ruins
                if (var == 3) // Step 3: Investigate the Crumbled Ruins.
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 2034);
                    if (dialogActionId == DialogAction.SETPRO4)
                    {
                        return DefaultCloseDialog(env, var, var + 1);
                    }
                }
                break;
            case 731539: // Destroyed Balaur Ruins
                if (var == 4) // Step 4: Investigate the Destroyed Balaur Ruins
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 2375);
                    if (dialogActionId == DialogAction.SETPRO5)
                        return DefaultCloseDialog(env, var, var + 1);
                }
                break;
            case 731540: // Intact Balaur Ruins
                if (var == 5) // Step 5: Look for the Intact Balaur Ruins.
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 2716);
                    if (dialogActionId == DialogAction.SETPRO6)
                        return DefaultCloseDialog(env, var, var + 1);
                }
                break;
            case 804722: // Putrid Balaur Corpse.
                if (var == 6) // Step 6: Examine the Putrid Balaur Corpse.
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 3057);
                    if (dialogActionId == DialogAction.SET_SUCCEED)
                    {
                        // Beritra Special Assassin [ID: 219935] is spawned
                        SpawnForFiveMinutes(219935, env.GetVisibleObject().GetPosition());
                        qs.SetQuestVar(var + 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return CloseDialogWindow(env);
                    }
                }
                break;
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName) // Step 2: Go to Dreg Findspot.
    {
        if (zoneName == ZoneName.Get("DF5_SENSORYAREA_Q20501A_206375_1_220080000"))
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

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 20500);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 20500);
    }
}
