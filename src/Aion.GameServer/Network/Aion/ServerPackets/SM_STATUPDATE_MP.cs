using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_STATUPDATE_MP (Luno). Updates current/max MP.</summary>
public class SM_STATUPDATE_MP : AionServerPacket
{
    private int currentMp;
    private int maxMp;

    public SM_STATUPDATE_MP(int currentMp, int maxMp)
    {
        this.currentMp = currentMp;
        this.maxMp = maxMp;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(currentMp);
        WriteD(maxMp);
    }
}
