using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/custom/eternalChallenge/CustomInstanceScapiAI (@author Jo, Estrayl).</summary>
[AIName("custom_instance_scapi")]
public class CustomInstanceScapiAI : AggressiveNoLootNpcAI
{
    public CustomInstanceScapiAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 1;
    }
}
