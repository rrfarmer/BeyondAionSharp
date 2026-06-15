using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/FlagBaseNpcAI (@author Bobobear).</summary>
[AIName("base_flag")]
public class FlagBaseNpcAI : NpcAI
{
    public FlagBaseNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return 0;
    }
}
