using System;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/StainedBase (Estrayl).</summary>
public class StainedBase : Base<StainedBaseLocation>
{
    private ScheduledTask enhancedSpawnTask;

    public StainedBase(StainedBaseLocation bLoc)
        : base(bLoc)
    {
    }

    protected override void HandleStop()
    {
        CancelTask(enhancedSpawnTask);
        base.HandleStop();
    }

    public void ScheduleEnhancedSpawns()
    {
        enhancedSpawnTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (IsStopped())
                return ValueTask.CompletedTask;
            DespawnByHandlerType(SpawnHandlerType.GUARDIAN); // prevents double spawns
            DespawnByHandlerType(SpawnHandlerType.OUTRIDER_ENHANCED);
            SpawnBySpawnHandler(SpawnHandlerType.GUARDIAN, GetOccupier());
            SpawnBySpawnHandler(SpawnHandlerType.OUTRIDER_ENHANCED, GetOccupier());
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(295 * 1000));
    }

    public void DeactivateEnhancedSpawns()
    {
        CancelTask(enhancedSpawnTask);
        DespawnByHandlerType(SpawnHandlerType.GUARDIAN);
        DespawnByHandlerType(SpawnHandlerType.OUTRIDER_ENHANCED);
    }

    protected override int GetAssaultDelay()
    {
        return Rnd.Get(300, 1200) * 6000;
    }

    protected override int GetAssaultDespawnDelay()
    {
        return Rnd.Get(100, 150) * 6000;
    }

    protected override int GetBossSpawnDelay()
    {
        return Rnd.Get(100, 200) * 6000;
    }

    protected override int GetNpcSpawnDelay()
    {
        return 30 * 1000;
    }

    public BaseColorType GetColor()
    {
        return GetLocation().GetColor();
    }
}
