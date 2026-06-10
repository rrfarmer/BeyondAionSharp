using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_ACQUIRE (Rolandas). House acquire/release notice (playerId + address + acquire flag).</summary>
public class SM_HOUSE_ACQUIRE : AionServerPacket
{
    private int playerId;
    private int address;
    private bool acquire;

    public SM_HOUSE_ACQUIRE(int playerId, int address, bool acquire)
    {
        this.playerId = playerId;
        this.address = address;
        this.acquire = acquire;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerId);
        WriteD(address);
        WriteD(acquire ? 1 : 0); // now it has value 2 sometimes, maybe initial door state ?
    }
}
