using Aion.GameServer.Commons.Nio;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/InstanceScoreWriter (xTz). Abstract base for instance scoreboard writers, extending
/// PacketWriteHelper. Java bound &lt;T extends InstanceScore&lt;?&gt;&gt; relaxed to where T : class (base uses no InstanceScore API; keeps
/// the deferred *ScoreWriter subclasses 1:1 single-parameter with Java). Default WriteMe is an empty override (subclasses override it).
/// </summary>
public abstract class InstanceScoreWriter<T> : Aion.GameServer.Network.PacketWriteHelper
    where T : class
{
    protected readonly T instanceScore;

    public InstanceScoreWriter(T instanceScore)
    {
        this.instanceScore = instanceScore;
    }

    public T GetInstanceScore()
    {
        return instanceScore;
    }

    protected override void WriteMe(ByteBuffer buf)
    {
    }
}
