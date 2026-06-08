using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/SummonLifeStats.</summary>
public class SummonLifeStats : CreatureLifeStats<Summon>
{
    public SummonLifeStats(Summon owner)
        : base(owner, owner.GetGameStats().GetMaxHp().GetCurrent(), owner.GetGameStats().GetMaxMp().GetCurrent())
    {
    }

    public override void TriggerRestoreTask()
    {
        lock (restoreLock)
        {
            if (lifeRestoreTask == null && !IsDead())
                lifeRestoreTask = Aion.GameServer.Services.LifeStatsRestoreService.GetInstance().ScheduleHpRestoreTask(this);
        }
    }
}
