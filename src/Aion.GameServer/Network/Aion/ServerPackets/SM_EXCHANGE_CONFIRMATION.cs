using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_EXCHANGE_CONFIRMATION (-Avol-). Sends an exchange confirmation/cancel action byte.</summary>
public class SM_EXCHANGE_CONFIRMATION : AionServerPacket
{
    private int action;

    public SM_EXCHANGE_CONFIRMATION(int action)
    {
        this.action = action;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);
    }
}
