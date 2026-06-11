using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_AFTER_TIME_CHECK_4_7_5 (Ritsu). Sent after the enter-world time check. Converges PlayerEnterWorldService. AionServerPacket red-tolerated.</summary>
public class SM_AFTER_TIME_CHECK_4_7_5 : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteH(1);
        WriteD(0);
    }
}
