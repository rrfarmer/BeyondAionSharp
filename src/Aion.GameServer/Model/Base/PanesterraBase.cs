using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/PanesterraBase (Estrayl).</summary>
public class PanesterraBase : Base<PanesterraBaseLocation>
{
    public PanesterraBase(PanesterraBaseLocation loc)
        : base(loc)
    {
    }

    protected override int GetAssaultDelay()
    {
        return Rnd.Get(75, 200) * 6000;
    }

    protected override int GetAssaultDespawnDelay()
    {
        return 15 * 60000; // Retail delay
    }

    protected override int GetBossSpawnDelay()
    {
        return 20 * 60000; // Retail delay
    }

    protected override int GetNpcSpawnDelay()
    {
        return 5 * 60000; // Retail delay
    }

    protected override BaseOccupier ChooseAssaultRace()
    {
        return BaseOccupier.BALAUR;
    }

    public override BaseOccupier GetOccupier(Creature bossKiller)
    {
        if (bossKiller is Player player && player.GetPanesterraFaction() != null)
            return BaseOccupierExtensions.FindBy(player.GetPanesterraFaction()).Value;
        return GetLocation().GetTemplate().GetDefaultOccupier();
    }
}
