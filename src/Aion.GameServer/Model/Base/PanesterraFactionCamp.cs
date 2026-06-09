using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/PanesterraFactionCamp (Estrayl).</summary>
public class PanesterraFactionCamp : PanesterraBase
{
    public PanesterraFactionCamp(PanesterraBaseLocation loc)
        : base(loc)
    {
    }

    protected override int GetBossSpawnDelay()
    {
        return 10 * 60000;
    }

    protected override int GetNpcSpawnDelay()
    {
        return 10 * 60000; // Retail delay
    }

    public override BaseOccupier GetOccupier(Creature bossKiller)
    {
        return BaseOccupier.PEACE; // If the soul anchor (boss) is destroyed, the camp will be eliminated
    }
}
