using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Lifecycle;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/NpcLifeStats.</summary>
public class NpcLifeStats : CreatureLifeStats<Npc>
{
    private long retailLifetime;
    private bool retailHeartbeatRunning;
    private uint lastRetailHeartbeat;
    // Host-side witness for prepared NPC drain healing. The native byte is
    // initialized/set and cleared by Npc::Die; its full transition equivalence
    // is not yet proven, so this does not admit a production drain program.
    private bool retailDrainLifeAlive=true;
    private long retailDrainLifeGeneration=1;
    public NpcLifeStats(Npc owner)
        : base(owner)
    {
    }

    internal long CaptureRetailDrainLife()
    {
        lock(this)return retailDrainLifeGeneration;
    }

    internal bool IsRetailDrainLifeEligible(long generation)
    {
        lock(this)return retailDrainLifeAlive && retailDrainLifeGeneration==generation
            && GetCurrentHp()>0 && owner.IsSpawned();
    }

    internal void RetireRetailDrainLife()
    {
        lock(this)
        {
            if(!retailDrainLifeAlive)return;
            retailDrainLifeAlive=false;
            retailDrainLifeGeneration=unchecked(retailDrainLifeGeneration+1);
        }
    }

    internal void BeginRetailDrainLife()
    {
        lock(this)
        {
            retailDrainLifeGeneration=unchecked(retailDrainLifeGeneration+1);
            retailDrainLifeAlive=true;
        }
    }

    public override void TriggerRestoreTask()
    {
        if (SkyTempleNpcProfileCatalog.TryGet((Npc)owner, out _))
        {
            StartRetailHeartbeat();
            return;
        }
        lock (restoreLock)
        {
            if (lifeRestoreTask == null && !IsDead())
            {
                this.lifeRestoreTask = Aion.GameServer.Services.LifeStatsRestoreService.GetInstance().ScheduleHpRestoreTask(this);
            }
        }
    }

    /// <summary>The existing restore handle becomes the selected NPC's single timer0
    /// chain. Fight/full HP suppress restoration, not the heartbeat clock.</summary>
    internal void StartRetailHeartbeat()
    {
        if (!SkyTempleNpcProfileCatalog.TryGet((Npc)owner, out _)) return;
        lock (restoreLock)
        {
            if (retailHeartbeatRunning || IsDead() || !owner.IsInWorld()) return;
            retailHeartbeatRunning = true;
            lastRetailHeartbeat = 0; // NPC constructors initialize +850 to zero.
            long lifetime = Interlocked.Increment(ref retailLifetime);
            lifeRestoreTask?.Cancel();
            ScheduleRetailHeartbeat(lifetime);
        }
    }

    internal void StopRetailHeartbeat()
    {
        lock (restoreLock)
        {
            if (!retailHeartbeatRunning) return;
            // Serialize retirement against the HP commit's final lifetime check.
            // Notifications run outside that lock; no HP callback takes restoreLock.
            lock (this)
            {
                retailHeartbeatRunning = false;
                Interlocked.Increment(ref retailLifetime);
            }
            CancelRestoreTask();
        }
    }

    public override void CancelAllTasks()
    {
        StopRetailHeartbeat();
        base.CancelAllTasks();
    }

    private bool IsCurrentRetailLifetime(long lifetime) =>
        Volatile.Read(ref retailHeartbeatRunning) && Interlocked.Read(ref retailLifetime) == lifetime && owner.IsInWorld();

    private void ScheduleRetailHeartbeat(long lifetime)
    {
        // This host runs active NPCs instead of the native split-server sleep
        // optimization. Its active timer0 branch is 2000ms + inclusive[0,200].
        int delay = RetailNpcRegenerationPlan.TimerZeroRearmDelay(false, Random.Shared.Next(0, 201));
        lifeRestoreTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (ct.IsCancellationRequested || !IsCurrentRetailLifetime(lifetime)) return ValueTask.CompletedTask;
            uint now = SystemClock.RetailTick32();
            bool due;
            lock (restoreLock)
            {
                if (!IsCurrentRetailLifetime(lifetime)) return ValueTask.CompletedTask;
                due = RetailNpcRegenerationPlan.HasHeartbeatOpportunity(now, lastRetailHeartbeat);
                if (due) lastRetailHeartbeat = now;
            }
            try
            {
                if (due) RestoreRetailHeartbeat(lifetime);
            }
            finally
            {
                // A post-commit observer can throw. The existing scheduler logs
                // the fault; rearm once without replaying that committed heal.
                lock (restoreLock)
                {
                    if (IsCurrentRetailLifetime(lifetime) && !IsDead()) ScheduleRetailHeartbeat(lifetime);
                    else if (Interlocked.Read(ref retailLifetime) == lifetime)
                    {
                        retailHeartbeatRunning = false;
                        lifeRestoreTask = null;
                    }
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delay));
    }

    private void RestoreRetailHeartbeat(long lifetime)
    {
        Npc npc = (Npc)owner;
        // FIGHT plus its retained encounter timestamp represents the attack
        // state even during a temporary fear/confuse motion. RETURNING is not
        // a blanket prohibition in the native heartbeat.
        bool attacking = npc.GetAi().GetState() == AIState.FIGHT || npc.GetGameStats().GetFightStartingTime() != 0;
        uint flags = npc.GetEffectController().IsAbnormalSet(AbnormalState.DISEASE) ? 0x80u : 0u;
        int order = npc.IsInState(CreatureState.RESTING) ? 2 : 0;
        var request = RetailNpcRegenerationPlan.PlanNormalHpRequest(!IsDead(), attacking, flags, order,
            GetCurrentHp(), GetMaxHp(), npc.GetGameStats().GetHpRegenRate().GetCurrent());
        if (request is null) return;
        // IncreaseHp owns locking, no-resurrection and all actual HP observers.
        // Check the spawn lifetime again inside that existing HP commit lock.
        IncreaseRetailNpcHp(request.Value.OriginalDelta, () => IsCurrentRetailLifetime(lifetime));
    }
}
