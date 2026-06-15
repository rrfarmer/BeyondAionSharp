using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/IdyunHardXasta (Estrayl).</summary>
[AIName("idyun_hard_xasta")]
public class IdyunHardXasta : GeneralNpcAI
{
    private AtomicBoolean isDestinationReached = new AtomicBoolean(false);

    public IdyunHardXasta(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().IsStop())
        {
            if (isDestinationReached.CompareAndSet(false, true))
                GetOwner().GetPosition().GetWorldMapInstance().GetInstanceHandler().OnSpecialEvent(GetOwner());
        }
    }
}
