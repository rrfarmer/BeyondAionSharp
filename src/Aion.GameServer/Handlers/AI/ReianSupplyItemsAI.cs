using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/kamarBf/ReianSupplyItemsAI (Cheatkiller).</summary>
[AIName("reiansupplyitems")]
public class ReianSupplyItemsAI : ChestAI
{
    private Npc flag;

    public ReianSupplyItemsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        flag = (Npc)Spawn(801959, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        if (flag != null)
            flag.GetController().Delete();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (flag != null)
            flag.GetController().Delete();
    }
}
