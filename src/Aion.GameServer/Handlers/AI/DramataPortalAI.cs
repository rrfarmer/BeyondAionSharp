using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/DramataPortalAI (Estrayl).</summary>
[AIName("dramata_portal")]
public class DramataPortalAI : ActionItemNpcAI
{
    private WorldPosition targetLocation;

    public DramataPortalAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetPosition().GetMapId())
        {
            case 110070000:
                PacketSendUtility.BroadcastToWorld(new SM_MESSAGE(GetOwner(),
                    "A dangerous rift is materializing inside [pos:Kaisinel's Academy;0 110070000 485.69 407.96 0.0 0]. Use it and destroy the source of this anomaly!",
                    ChatType.BRIGHT_YELLOW_CENTER), p => p.GetLevel() >= 65 && p.GetRace() == Race.ELYOS);
                targetLocation = new WorldPosition(220070000, 2928.98f, 919.95f, 35.289f, (byte)94);
                break;
            case 120080000:
                PacketSendUtility.BroadcastToWorld(new SM_MESSAGE(GetOwner(),
                    "A dangerous rift is materializing inside [pos:Marchutan Priory;1 120080000 392.58 231.57 0.0 0]. Use it and destroy the source of this anomaly!",
                    ChatType.BRIGHT_YELLOW_CENTER), p => p.GetLevel() >= 65 && p.GetRace() == Race.ASMODIANS);
                targetLocation = new WorldPosition(210050000, 141.7f, 2066.3f, 438.66f, (byte)34);
                break;
        }
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (targetLocation == null)
            return;
        if (player.GetLevel() < 60)
        {
            PacketSendUtility.SendPacket(player,
                SM_SYSTEM_MESSAGE.STR_CANNOT_USE_DIRECT_PORTAL_LEVEL_LIMIT_COMMON(GetOwner().GetObjectTemplate().GetL10n()));
            return;
        }
        long timeDelta = GetOwner().GetMillisSinceSpawn();
        if (timeDelta < 10 * 60 * 1000L)
        {
            long approxMin = 10 - timeDelta / 1000 / 60;
            PacketSendUtility.SendMessage(player,
                "Wait Daeva! The rift is not stable enough to enter. We estimate it is safe to use in about " + approxMin + " minutes.");
            return;
        }
        TeleportService.TeleportTo(player, targetLocation);
        base.HandleUseItemFinish(player);
    }
}
