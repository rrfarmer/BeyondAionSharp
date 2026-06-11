using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/ReturningEventHandler (ATracer). NPC return-home / back-home dispatch (returning state, walk/move, dispel,
/// post-spawn skills, instance handler onBackHome). AIEventType.BACK_HOME -> AiEventType.BackHome; AIState.RETURNING/IDLE -> Returning/
/// Idle. EmoteManager/WalkManager/SkillEngine/DispelSlotType red-tolerated where unported.
/// </summary>
public class ReturningEventHandler
{
    public static void OnNotAtHome(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onNotAtHome");
        }
        if (!npcAI.IsMoveSupported())
        {
            npcAI.OnGeneralEvent(AiEventType.BackHome);
        }
        else if (npcAI.SetStateIfNot(AiState.Returning))
        {
            npcAI.SetSubStateIfNot(AiSubState.None);
            if (npcAI.IsLogging())
            {
                AILogger.Info(npcAI, "returning and restoring");
            }
            Npc npc = npcAI.GetOwner();
            EmoteManager.EmoteStartReturning(npc);
            if (npc.IsPathWalker() && WalkManager.StartWalking(npcAI))
                return;
            npc.GetMoveController().ReturnToLastStepOrSpawn();
        }
    }

    public static void OnBackHome(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onBackHome");
        }
        npcAI.GetOwner().GetMoveController().ClearBackSteps();
        if (npcAI.SetStateIfNot(AiState.Idle))
        {
            npcAI.SetSubStateIfNot(AiSubState.None);
            npcAI.GetOwner().GetEffectController().RemoveByDispelSlotType(DispelSlotType.BUFF);
            EmoteManager.EmoteStartIdling(npcAI.GetOwner());
            npcAI.Think();
            Npc npc = npcAI.GetOwner();
            List<NpcSkillEntry> skills = npc.GetSkillList().GetPostSpawnSkills();
            if (skills.Count != 0)
                foreach (NpcSkillEntry s in skills)
                    SkillEngine.GetInstance().GetSkill(npc, s.GetSkillId(), s.GetSkillLevel(), npc).UseWithoutPropSkill();
        }
        npcAI.GetOwner().GetPosition().GetWorldMapInstance().GetInstanceHandler().OnBackHome(npcAI.GetOwner());
    }
}
