using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.SkillEngine;

namespace Aion.GameServer.Ai.Handler;

/// <summary>Java parity: ai/handler/SpawnEventHandler (ATracer). NPC spawn/despawn dispatch; casts post-spawn skills. SkillEngine red-tolerated.</summary>
public class SpawnEventHandler
{
    public static void OnSpawn(NpcAI npcAI)
    {
        if (npcAI.SetStateIfNot(AiState.Idle))
        {
            npcAI.Think();
            Npc npc = npcAI.GetOwner();
            List<NpcSkillEntry> skills = npc.GetSkillList().GetPostSpawnSkills();
            if (skills.Count != 0)
                foreach (NpcSkillEntry s in skills)
                    SkillEngine.GetInstance().GetSkill(npc, s.GetSkillId(), s.GetSkillLevel(), npc).UseWithoutPropSkill();
        }
    }

    public static void OnDespawn(NpcAI npcAI)
    {
        npcAI.SetStateIfNot(AiState.Despawned);
    }

    public static void OnBeforeSpawn(NpcAI npcAI)
    {
    }
}
