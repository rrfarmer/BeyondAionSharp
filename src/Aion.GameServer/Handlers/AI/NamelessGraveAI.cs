using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/NamelessGraveAI (@author Tibald).</summary>
[AIName("namelessgrave")]
public class NamelessGraveAI : GeneralNpcAI
{
    private AtomicBoolean isSpawned = new AtomicBoolean(false);

    public NamelessGraveAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (isSpawned.CompareAndSet(false, true))
        {
            RndSpawnInRange(283905, 1, 2);
            RndSpawnInRange(283905, 1, 2);
        }
    }
}
