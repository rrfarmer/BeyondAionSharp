using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEAVE_GROUP_MEMBER (Lyahim). Sent when leaving a group (fixed fields).</summary>
public class SM_LEAVE_GROUP_MEMBER : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(0x00);
        WriteC(0x00);
        WriteD(0x3F); // TODO: TeamType.getType
        WriteD(0x00); // TODO: TeamType.getSubType
        WriteH(0x00);
    }
}
