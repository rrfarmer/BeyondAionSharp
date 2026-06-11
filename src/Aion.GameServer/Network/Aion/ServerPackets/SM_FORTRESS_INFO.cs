using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FORTRESS_INFO. Per-fortress teleport availability (locationId + teleport flag).</summary>
public class SM_FORTRESS_INFO : AionServerPacket
{
    private int locationId;
    private bool teleportStatus;

    public SM_FORTRESS_INFO(int locationId, bool teleportStatus)
    {
        this.locationId = locationId;
        this.teleportStatus = teleportStatus;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(locationId);
        WriteC(teleportStatus ? 1 : 0);
    }
}
