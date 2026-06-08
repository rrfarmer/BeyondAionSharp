using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/NpcLifeStats.</summary>
public class NpcLifeStats : CreatureLifeStats<Npc>
{
    public NpcLifeStats(Npc owner)
        : base(owner, owner.GetGameStats().GetMaxHp().GetCurrent(), owner.GetGameStats().GetMaxMp().GetCurrent())
    {
    }

    public override void TriggerRestoreTask()
    {
        lock (restoreLock)
        {
            if (lifeRestoreTask == null && !IsDead())
            {
                this.lifeRestoreTask = Aion.GameServer.Services.LifeStatsRestoreService.GetInstance().ScheduleHpRestoreTask(this);
            }
        }
    }
}
