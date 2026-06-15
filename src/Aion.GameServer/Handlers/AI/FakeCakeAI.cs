using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/FakeCakeAI (Estrayl).</summary>
[AIName("pandoras_box")]
public class FakeCakeAI : ChestAI
{
    public FakeCakeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        Punishment(player);
    }

    private void Punishment(Player player)
    {
        AuditLogger.Log(player, string.Format("{0} used the fake cake at {1}.", player, player.GetPosition()));
        GetOwner().GetController().Die();
    }
}
