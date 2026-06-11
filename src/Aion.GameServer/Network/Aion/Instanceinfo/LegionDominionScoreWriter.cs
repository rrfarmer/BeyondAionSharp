using Aion.Commons.Nio;
using Aion.GameServer.Model.Instance.Instancescore;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/LegionDominionScoreWriter (Yeats) : InstanceScoreWriter&lt;LegionDominionScore&gt;. Writes
/// points/GP/AP + 4 reward item/count pairs. LegionDominionScore red-tolerated.
/// </summary>
public class LegionDominionScoreWriter : InstanceScoreWriter<LegionDominionScore>
{
    public LegionDominionScoreWriter(LegionDominionScore reward)
        : base(reward)
    {
    }

    protected override void WriteMe(ByteBuffer buf)
    {
        WriteD(buf, instanceScore.GetPoints());
        WriteD(buf, 0); // unk
        WriteD(buf, 0); // unk
        WriteD(buf, 0); // unk
        WriteD(buf, instanceScore.GetFinalGP());
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
