using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_STATS_STATUS_UNK (Rolandas). Stat-points status (points + level-50-conditional fields).</summary>
public class SM_STATS_STATUS_UNK : AionServerPacket
{
    private int lvl;
    private int points;

    public SM_STATS_STATUS_UNK(int lvl, int points)
    {
        this.lvl = lvl;
        this.points = points;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(points);
        WriteC(1);
        if (lvl == 50)
            WriteC(1);
        else
            WriteC(2);
        WriteD(lvl);
        WriteD(lvl);
        WriteD(lvl == 50 ? 1 : 0);
        WriteC(0);
    }
}
