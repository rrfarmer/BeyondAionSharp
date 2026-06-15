using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/quests/AscensationNpcAI (Cheatkiller).</summary>
[AIName("ascensationquestnpc")]
public class AscensationNpcAI : AggressiveNpcAI
{
    public AscensationNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return 1;
    }
}
