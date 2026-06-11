using Aion.Commons.Nio;
using Aion.GameServer.Model.Instance.Instancescore;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/DarkPoetaScoreWriter (xTz) : InstanceScoreWriter&lt;DarkPoetaScore&gt;. Writes points/kills/
/// gathers/rank. DarkPoetaScore red-tolerated.
/// </summary>
public class DarkPoetaScoreWriter : InstanceScoreWriter<DarkPoetaScore>
{
    public DarkPoetaScoreWriter(DarkPoetaScore reward)
        : base(reward)
    {
    }

    protected override void WriteMe(ByteBuffer buf)
    {
        WriteD(buf, instanceScore.GetPoints());
        WriteD(buf, instanceScore.GetNpcKills());
        WriteD(buf, instanceScore.GetGatherCollections()); // gathers
        WriteD(buf, instanceScore.GetRank()); // 7 for none, 8 for F, 5 for D, 4 C, 3 B, 2 A, 1 S
    }
}
