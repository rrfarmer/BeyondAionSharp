using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AhserionGate (Yeats).</summary>
[AIName("ahserion_gate")]
public class AhserionGate : AhserionConstructAI
{
    public AhserionGate(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        UseBuff();
    }

    private void UseBuff()
    {
        SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21515, GetSkillLevel(), GetOwner(), GetOwner(), null, Effect.ForceType.DEFAULT);
        GetOwner().SetTarget(null);
    }

    /// <summary>
    /// Each level corresponds to 30s buff time.
    /// Retail values: extracted from npcs_abyss_monsters.xml
    /// </summary>
    private int GetSkillLevel()
    {
        return GetNpcId() switch
        {
            277229 => 40, // Hangar Barricade
            277230 => 50, // Ahserion's Flight Barrier
            277231 => 60, // Bulwark Shield
            _ => 0,
        };
    }
}
