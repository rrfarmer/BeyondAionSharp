using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_STATUPDATE_EXP (Luno, alexa026). Updates current/recoverable/max exp + boost exp.</summary>
public class SM_STATUPDATE_EXP : AionServerPacket
{
    private long currentExp;
    private long recoverableExp;
    private long maxExp;

    private long curBoostExp = 0;
    private long maxBoostExp = 0;

    public SM_STATUPDATE_EXP(long currentExp, long recoverableExp, long maxExp, long rep1, long rep2)
    {
        this.currentExp = currentExp;
        this.recoverableExp = recoverableExp;
        this.maxExp = maxExp;
        curBoostExp = rep1;
        maxBoostExp = rep2;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteQ(currentExp);
        WriteQ(recoverableExp);
        WriteQ(maxExp);
        WriteQ(curBoostExp);
        WriteQ(maxBoostExp);
    }
}
