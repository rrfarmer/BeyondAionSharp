using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_STATUPDATE_DP (Luno). Updates current DP (divine points).</summary>
public class SM_STATUPDATE_DP : AionServerPacket
{
    private int currentDp;

    public SM_STATUPDATE_DP(int currentDp)
    {
        this.currentDp = currentDp;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(currentDp);
    }
}
