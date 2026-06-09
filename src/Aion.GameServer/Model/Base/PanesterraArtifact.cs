namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/PanesterraArtifact (Estrayl).</summary>
public class PanesterraArtifact : PanesterraBase
{
    public PanesterraArtifact(PanesterraBaseLocation loc)
        : base(loc)
    {
    }

    protected override int GetBossSpawnDelay()
    {
        return 5 * 60000; // Retail delay
    }
}
