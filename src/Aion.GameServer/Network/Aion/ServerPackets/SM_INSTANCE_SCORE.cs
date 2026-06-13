using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Instanceinfo;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_INSTANCE_SCORE (Dns, ginho1, nrg, xTz). Instance/arena score. Wildcard InstanceScoreWriter&lt;?&gt; erased to bound InstanceScoreWriter&lt;InstanceScore&lt;InstancePlayerReward&gt;&gt;; writeMe(buf)->WriteMe(GetBuf()). Instanceinfo writers red-tolerated.</summary>
public class SM_INSTANCE_SCORE : AionServerPacket
{
    private readonly int mapId;
    private readonly int instanceTime;
    private readonly InstanceScoreWriter instanceScoreWriter;

    public SM_INSTANCE_SCORE(int mapId, ArenaScoreWriter arenaScoreInfo)
        : this(mapId, arenaScoreInfo, arenaScoreInfo.GetInstanceScore().GetTime())
    {
    }

    public SM_INSTANCE_SCORE(int mapId, InstanceScoreWriter instanceScoreWriter)
        : this(mapId, instanceScoreWriter, 0)
    {
    }

    public SM_INSTANCE_SCORE(int mapId, InstanceScoreWriter instanceScoreWriter, int instanceTime)
    {
        this.mapId = mapId;
        this.instanceTime = instanceTime;
        this.instanceScoreWriter = instanceScoreWriter;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(mapId);
        WriteD(instanceTime);
        WriteD(instanceScoreWriter.GetInstanceScore().GetInstanceProgressionType().GetId());
        instanceScoreWriter.WriteMe(GetBuf());
    }
}
