using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_EXCHANGE_ADD_KINAH (Avol). Sends kinah added to an exchange slot (action 0 self / 1 other).</summary>
public class SM_EXCHANGE_ADD_KINAH : AionServerPacket
{
    private long kinahCount;
    private int action;

    public SM_EXCHANGE_ADD_KINAH(long kinahCount, int action)
    {
        this.kinahCount = kinahCount;
        this.action = action;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action); // 0 -self 1-other
        WriteQ(kinahCount);
    }
}
