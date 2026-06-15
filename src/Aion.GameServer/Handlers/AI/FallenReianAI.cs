using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/FallenReianAI (xTz).</summary>
[AIName("fallen_reian")]
public class FallenReianAI : NpcAI
{
    private AtomicBoolean isCollapsed = new AtomicBoolean(false);

    public FallenReianAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (isCollapsed.Get())
            return;
        if (!(creature is Player))
            return;
        Player player = (Player)creature;
        if (PositionUtil.IsInRange(GetOwner(), player, 20) && isCollapsed.CompareAndSet(false, true))
            GetPosition().GetWorldMapInstance().SetDoorState(GetNpcId() == 799661 ? 16 : 54, true);
    }
}
