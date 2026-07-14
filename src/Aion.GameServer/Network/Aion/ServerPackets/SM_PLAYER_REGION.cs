using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLAYER_REGION (Rolandas). Sends the player's current sub-zone. CRITICAL: Java writes subZone.name().hashCode() to the wire — must use Java's String.hashCode algorithm (h = 31*h + c), NOT C# string.GetHashCode(). ZoneName.name()->ZoneName.Name property. ZoneName red-tolerated.</summary>
public class SM_PLAYER_REGION : AionServerPacket
{
    private int playerObjId;
    private ZoneName subZone;

    public SM_PLAYER_REGION(Player player, ZoneName subZone)
    {
        this.playerObjId = player.GetObjectId();
        this.subZone = subZone; // player.getActiveRegion().getZones(player).stream().findFirst().get().getAreaTemplate().getZoneName()
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteC(0);
        WriteC(0);
        WriteC(0);
        WriteD(JavaString.HashCode(subZone.Name));
    }
}
