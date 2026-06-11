using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_PAY_RENT (Rolandas). House rent payment result (weeks paid).</summary>
public class SM_HOUSE_PAY_RENT : AionServerPacket
{
    private int weeksPaid;

    public SM_HOUSE_PAY_RENT(int weeksPaid)
    {
        this.weeksPaid = weeksPaid;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(0);
        WriteC(weeksPaid);
    }
}
