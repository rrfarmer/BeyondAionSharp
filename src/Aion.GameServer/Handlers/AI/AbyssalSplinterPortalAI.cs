using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/abyssalsplinter/AbyssalSplinterPortalAI (Ritsu).</summary>
[AIName("teleportation_device")]
public class AbyssalSplinterPortalAI : ActionItemNpcAI
{
    public AbyssalSplinterPortalAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        float x = GetOwner().GetX();
        if (x == 302.201f)
            TeleportService.TeleportTo(player, GetOwner().GetWorldId(), 294.632f, 732.189f, 215.854f);
        else if (x == 334.001f)
            TeleportService.TeleportTo(player, GetOwner().GetWorldId(), 338.475f, 701.417f, 215.916f);
        else if (x == 362.192f)
            TeleportService.TeleportTo(player, GetOwner().GetWorldId(), 373.611f, 739.125f, 215.903f);
    }
}
