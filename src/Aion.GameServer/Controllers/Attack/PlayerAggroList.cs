using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>Java parity: controllers/attack/PlayerAggroList (ATracer).</summary>
public class PlayerAggroList : AggroList
{
    public PlayerAggroList(Creature owner)
        : base(owner)
    {
    }

    protected override bool IsAware(Creature creature)
    {
        return creature != null && Owner.GetKnownList().Knows(creature);
    }
}
