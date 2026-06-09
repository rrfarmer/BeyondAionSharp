using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/CasualBase (Estrayl).</summary>
public class CasualBase : Base<BaseLocation>
{
    public CasualBase(BaseLocation bLoc)
        : base(bLoc)
    {
    }

    protected override int GetAssaultDelay()
    {
        if (GetWorldId() == 600100000)
            return Rnd.Get(300, 1200) * 6000;
        return Rnd.Get(75, 200) * 6000;
    }

    protected override int GetAssaultDespawnDelay()
    {
        return 15 * 60000;
    }

    protected override int GetBossSpawnDelay()
    {
        if (GetWorldId() == 600090000)
            return 0;
        return Rnd.Get(100, 200) * 6000;
    }

    protected override int GetNpcSpawnDelay()
    {
        return Rnd.Get(60, 295) * 1000;
    }
}
