using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PONG. Ping response. AionServerPacket red-tolerated.</summary>
public class SM_PONG : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteC(0x00);
        WriteC(0x00);
    }
}
