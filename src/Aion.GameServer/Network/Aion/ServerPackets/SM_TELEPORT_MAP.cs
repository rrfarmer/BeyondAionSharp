using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TELEPORT_MAP (alexa026, orz). Opens a teleporter map (target objId + teleport id).</summary>
public class SM_TELEPORT_MAP : AionServerPacket
{
    private int targetObjId;
    private int teleportId;

    public SM_TELEPORT_MAP(int targetObjId, int teleportId)
    {
        this.targetObjId = targetObjId;
        this.teleportId = teleportId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjId);
        WriteH(teleportId);
    }
}
