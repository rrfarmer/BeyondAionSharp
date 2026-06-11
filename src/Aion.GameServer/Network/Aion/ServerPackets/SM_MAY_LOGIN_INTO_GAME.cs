using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MAY_LOGIN_INTO_GAME (-Nemesiss-). Response for CM_MAY_LOGIN_INTO_GAME (0 = ok). AionServerPacket red-tolerated.</summary>
public class SM_MAY_LOGIN_INTO_GAME : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        // probably here is msg if fail.
        WriteD(0x00);
    }
}
