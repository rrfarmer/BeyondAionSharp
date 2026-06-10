using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services.Summons;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/TrapController : NpcController. onDie/onDelete→override; super→base. TrapService red-tolerated.</summary>
public class TrapController : NpcController
{
    public override void OnDie(Creature lastAttacker)
    {
        TrapService.UnregisterTrap(GetOwner().GetObjectId());
        base.OnDie(lastAttacker);
    }

    public override void OnDelete()
    {
        TrapService.UnregisterTrap(GetOwner().GetObjectId());
        base.OnDelete();
    }
}
