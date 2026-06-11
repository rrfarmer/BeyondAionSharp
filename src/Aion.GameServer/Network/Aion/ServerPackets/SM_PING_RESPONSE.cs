using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PING_RESPONSE (dragoon112). Ping response (0x04).</summary>
public class SM_PING_RESPONSE : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteC(0x04);
    }
}
