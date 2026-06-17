using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_KEY (-Nemesiss-). Sends the crypt-enable key. enableCryptKey()->EnableCryptKey() red-tolerated.</summary>
public class SM_KEY : AionServerPacket
{
    // Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_KEY.java): 2026-06-17. Reads con.enableCryptKey().
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(con.EnableCryptKey());
    }
}
