using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_EXCHANGE_REQUEST (-Avol-). Sends the exchange receiver name.</summary>
public class SM_EXCHANGE_REQUEST : AionServerPacket
{
    private string receiver;

    public SM_EXCHANGE_REQUEST(string receiver)
    {
        this.receiver = receiver;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(receiver);
    }
}
