using Aion.Commons.Nio;
using Aion.GameServer.Model.Instance.Instancescore;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/EternalBastionScoreWriter (xTz, Estrayl) : InstanceScoreWriter&lt;NormalScore&gt;. Writes
/// points/rank/AP + 4 reward item/count pairs. NormalScore red-tolerated.
/// </summary>
public class EternalBastionScoreWriter : InstanceScoreWriter<NormalScore>
{
    public EternalBastionScoreWriter(NormalScore reward)
        : base(reward)
    {
    }

    public override void WriteMe(ByteBuffer buf)
    {
        WriteD(buf, instanceScore.GetPoints());
        WriteQ(buf, 0); // unk
        WriteD(buf, instanceScore.GetRank());
        WriteD(buf, 0); // unk
        WriteD(buf, instanceScore.GetFinalAp());
        WriteD(buf, instanceScore.GetRewardItem1());
        WriteD(buf, instanceScore.GetRewardItem1Count());
        WriteD(buf, instanceScore.GetRewardItem2());
        WriteD(buf, instanceScore.GetRewardItem2Count());
        WriteD(buf, instanceScore.GetRewardItem3());
        WriteD(buf, instanceScore.GetRewardItem3Count());
        WriteD(buf, instanceScore.GetRewardItem4());
        WriteD(buf, instanceScore.GetRewardItem4Count());
    }
}
