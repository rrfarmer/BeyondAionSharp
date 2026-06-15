using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/FlagNpcAI (Cheatkiller, Sykra). Takes no damage.</summary>
[AIName("flag")]
public class FlagNpcAI : NpcAI
{
    public FlagNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }
}
