using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FLY_TIME (Nemiroff). Sends current + max fly time (FP).</summary>
public class SM_FLY_TIME : AionServerPacket
{
    private int currentFp;
    private int maxFp;

    public SM_FLY_TIME(int currentFp, int maxFp)
    {
        this.currentFp = currentFp;
        this.maxFp = maxFp;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(currentFp); // current fly time
        WriteD(maxFp); // max flytime
    }
}
