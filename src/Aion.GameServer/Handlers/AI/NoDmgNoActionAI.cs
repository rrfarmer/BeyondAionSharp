using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/NoDmgNoActionAI (Yeats). Takes no damage.</summary>
[AIName("no_dmg_no_action")]
public class NoDmgNoActionAI : NpcAI
{
    public NoDmgNoActionAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }
}
