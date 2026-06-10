using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RECEIVE_BIDS (Rolandas). Notifies the client that auction data changed (it will request CM_GET_HOUSE_BIDS).</summary>
public class SM_RECEIVE_BIDS : AionServerPacket
{
    private readonly int unk;

    public SM_RECEIVE_BIDS(int unk)
    {
        this.unk = unk;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(unk);
    }
}
