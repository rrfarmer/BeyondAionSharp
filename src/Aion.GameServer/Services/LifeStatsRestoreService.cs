using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/LifeStatsRestoreService (ATracer).</summary>
public class LifeStatsRestoreService
{
    private const int DEFAULT_DELAY = 6000;

    private static LifeStatsRestoreService instance = new LifeStatsRestoreService();

    /// <summary>HP and MP restoring task.</summary>
    public ScheduledTask ScheduleRestoreTask(CreatureLifeStats lifeStats)
    {
        HpMpRestoreTask task = new HpMpRestoreTask(lifeStats);
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(1700), TimeSpan.FromMilliseconds(DEFAULT_DELAY));
    }

    public ScheduledTask ScheduleHpRestoreTask(CreatureLifeStats lifeStats)
    {
        HpRestoreTask task = new HpRestoreTask(lifeStats);
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(1700), TimeSpan.FromMilliseconds(DEFAULT_DELAY));
    }

    public ScheduledTask ScheduleFpReduceTask(PlayerLifeStats lifeStats)
    {
        FpReduceTask task = new FpReduceTask(lifeStats);
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));
    }

    public ScheduledTask ScheduleFpRestoreTask(PlayerLifeStats lifeStats)
    {
        FpRestoreTask task = new FpRestoreTask(lifeStats);
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(3000), TimeSpan.FromMilliseconds(DEFAULT_DELAY));
    }

    public static LifeStatsRestoreService GetInstance()
    {
        return instance;
    }

    private class HpRestoreTask
    {
        private CreatureLifeStats lifeStats;

        internal HpRestoreTask(CreatureLifeStats lifeStats)
        {
            this.lifeStats = lifeStats;
        }

        public void Run()
        {
            if (lifeStats.IsDead() || lifeStats.IsFullyRestoredHp() || !lifeStats.GetOwner().IsInWorld() || lifeStats.GetOwner().GetAi().GetState() == AiState.Fight)
            {
                lifeStats.CancelRestoreTask();
                lifeStats = null;
            }
            else
            {
                lifeStats.RestoreHp();
            }
        }
    }

    private class HpMpRestoreTask
    {
        private CreatureLifeStats lifeStats;

        internal HpMpRestoreTask(CreatureLifeStats lifeStats)
        {
            this.lifeStats = lifeStats;
        }

        public void Run()
        {
            if (lifeStats.IsDead() || lifeStats.IsFullyRestoredHpMp() || !lifeStats.GetOwner().IsInWorld())
            {
                lifeStats.CancelRestoreTask();
                lifeStats = null;
            }
            else
            {
                lifeStats.RestoreHp();
                lifeStats.RestoreMp();
            }
        }
    }

    private class FpReduceTask
    {
        private PlayerLifeStats lifeStats;
        private int secondsElapsed = 0;

        internal FpReduceTask(PlayerLifeStats lifeStats)
        {
            this.lifeStats = lifeStats;
        }

        public void Run()
        {
            if (lifeStats.IsDead() || !lifeStats.GetOwner().IsSpawned())
            {
                lifeStats.CancelFpReduce();
                lifeStats = null;
                return;
            }
            else if (secondsElapsed % lifeStats.GetFlightReducePeriod() == 0)
            {
                lifeStats.ReduceFp(null, lifeStats.GetFlightReduceValue(), 0, null);
                lifeStats.SpecialrestoreFp();
                if (lifeStats.GetCurrentFp() <= 0)
                {
                    if (lifeStats.GetOwner().IsFlying())
                    {
                        lifeStats.GetOwner().GetFlyController().EndFly(true);
                    }
                    else
                    {
                        lifeStats.TriggerFpRestore();
                    }
                }
            }
            secondsElapsed++;
        }
    }

    private class FpRestoreTask
    {
        private PlayerLifeStats lifeStats;

        internal FpRestoreTask(PlayerLifeStats lifeStats)
        {
            this.lifeStats = lifeStats;
        }

        public void Run()
        {
            if (lifeStats.IsDead() || lifeStats.IsFlyTimeFullyRestored())
            {
                lifeStats.CancelFpRestore();
                lifeStats = null;
            }
            else
            {
                lifeStats.RestoreFp();
            }
        }
    }
}
