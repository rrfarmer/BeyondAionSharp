using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_KEY (-Nemesiss-). Sends the crypt-enable key. enableCryptKey()->EnableCryptKey() red-tolerated.</summary>
public class SM_KEY : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(con.EnableCryptKey());
    }
}
