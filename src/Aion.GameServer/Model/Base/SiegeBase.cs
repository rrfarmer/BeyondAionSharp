namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/SiegeBase (Estrayl). TODO: Implement Anoha/Wealhtheow features!</summary>
public class SiegeBase : Base<SiegeBaseLocation>
{
    public SiegeBase(SiegeBaseLocation bLoc)
        : base(bLoc)
    {
    }

    protected override int GetAssaultDelay()
    {
        return 180 * 60000;
    }

    protected override int GetAssaultDespawnDelay()
    {
        return 295 * 1000; // TODO: Dont Reassault with Despawntask
    }

    protected override int GetBossSpawnDelay()
    {
        return 0;
    }

    protected override int GetNpcSpawnDelay()
    {
        return 0;
    }
}
