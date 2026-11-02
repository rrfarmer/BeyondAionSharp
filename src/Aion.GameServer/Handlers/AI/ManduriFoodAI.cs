using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/ManduriFoodAI.
/// Feed or water for the stage 5 giant. It only stands where the supply device put it out; whoever finishes it decides what the giant gets.
/// </summary>
[AIName("empyrean_manduri_food")]
public class ManduriFoodAI : NpcAI
{
    private volatile Creature? lastAttacker;

    public ManduriFoodAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (Giant() is Npc giant && giant.GetAi() is ManduriGiantAI ai)
            ai.OnFoodSpawned(GetOwner());
    }

    protected override void HandleAttack(Creature creature)
    {
        lastAttacker = creature;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        Npc giant = Giant();
        if (giant != null && giant.GetAi() is ManduriGiantAI ai)
            ai.OnFoodDied(GetNpcId(), giant.Equals(lastAttacker));
    }

    private Npc Giant()
    {
        return GetPosition().GetWorldMapInstance().GetNpc(217553);
    }
}
