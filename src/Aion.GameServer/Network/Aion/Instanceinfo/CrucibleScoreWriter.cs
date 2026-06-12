using Aion.Commons.Nio;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/CrucibleScoreWriter (xTz) : InstanceScoreWriter&lt;InstanceScore&lt;CruciblePlayerReward&gt;&gt;.
/// Writes per-player crucible rewards, padding to 6 slots. InstanceScore/CruciblePlayerReward red-tolerated.
/// </summary>
public class CrucibleScoreWriter : InstanceScoreWriter<InstanceScore<CruciblePlayerReward>>
{
    public CrucibleScoreWriter(InstanceScore<CruciblePlayerReward> reward)
        : base(reward)
    {
    }

    public override void WriteMe(ByteBuffer buf)
    {
        int playerCount = 0;
        foreach (CruciblePlayerReward playerReward in instanceScore.GetPlayerRewards())
        {
            WriteD(buf, playerReward.GetOwnerId()); // obj
            WriteD(buf, playerReward.GetPoints()); // points
            WriteD(buf, instanceScore.GetInstanceProgressionType().IsEndProgress() ? 3 : 1);
            WriteD(buf, playerReward.GetInsignia());
            playerCount++;
        }
        if (playerCount < 6)
        {
            WriteB(buf, new byte[16 * (6 - playerCount)]); // spaces
        }
    }
}
