using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TOLL_INFO (xTz). Sends the player's toll (AP-token) count.</summary>
public class SM_TOLL_INFO : AionServerPacket
{
    private long tollCount;

    public SM_TOLL_INFO(long tollCount)
    {
        this.tollCount = tollCount;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteQ(tollCount);
    }
}
