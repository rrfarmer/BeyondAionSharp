using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FRIEND_STATUS (Rolandas). Sends a friend status byte.</summary>
public class SM_FRIEND_STATUS : AionServerPacket
{
    private int status;

    public SM_FRIEND_STATUS(int status)
    {
        this.status = status;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(status);
    }
}
