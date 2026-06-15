using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/kamarBf/KamarTeleportAI (Luzien).</summary>
[AIName("kamar_teleport")]
public class KamarTeleportAI : PortalAI
{
    protected int remainingUses;

    public KamarTeleportAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        remainingUses = 25;
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (remainingUses <= 0)
        {
            PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1500906));
            return;
        }
        base.HandleUseItemFinish(player);
        if (--remainingUses == 0)
            PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1500906));
        else
            PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1500905, remainingUses));
    }
}
