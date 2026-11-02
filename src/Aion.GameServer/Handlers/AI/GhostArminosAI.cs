using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/GhostArminosAI.
/// Administrator Arminos of the stage 6 bonus: his fight starts on the first hit as well, since he only notices players within 15 meters.
/// </summary>
[AIName("empyrean_ghost_arminos")]
public class GhostArminosAI : SummonerAI
{
    private readonly AtomicBoolean engaged = new AtomicBoolean();

    public GhostArminosAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (engaged.CompareAndSet(false, true))
            GetPosition().GetWorldMapInstance().GetInstanceHandler().OnAggro(GetOwner());
    }
}
