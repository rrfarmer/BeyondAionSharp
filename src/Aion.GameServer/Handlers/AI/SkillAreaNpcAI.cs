using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/SkillAreaNpcAI (ATracer).</summary>
[AIName("skillarea")]
public class SkillAreaNpcAI : NpcAI
{
    public SkillAreaNpcAI(Npc owner)
        : base(owner)
    {
    }
}
