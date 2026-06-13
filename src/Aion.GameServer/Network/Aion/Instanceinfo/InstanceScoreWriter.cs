using Aion.Commons.Nio;
using Aion.GameServer.Model.Instance.Instancescore;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/InstanceScoreWriter (xTz). Abstract base for instance scoreboard writers, extending
/// PacketWriteHelper. Java &lt;T extends InstanceScore&lt;?&gt;&gt; wildcard rendered as a non-generic InstanceScoreWriter base
/// (matching Java's InstanceScoreWriter&lt;?&gt; usage in SM_INSTANCE_SCORE) plus the generic InstanceScoreWriter&lt;T&gt; whose
/// GetInstanceScore() covariantly returns T. Default WriteMe is an empty override (subclasses override it).
/// </summary>
public abstract class InstanceScoreWriter : global::Aion.GameServer.Network.PacketWriteHelper
{
    public abstract InstanceScore GetInstanceScore();
}

public abstract class InstanceScoreWriter<T> : InstanceScoreWriter
    where T : InstanceScore
{
    protected readonly T instanceScore;

    public InstanceScoreWriter(T instanceScore)
    {
        this.instanceScore = instanceScore;
    }

    public override T GetInstanceScore()
    {
        return instanceScore;
    }

    public override void WriteMe(ByteBuffer buf)
    {
    }
}
