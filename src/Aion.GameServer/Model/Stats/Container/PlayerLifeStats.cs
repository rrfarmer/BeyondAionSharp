using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/PlayerLifeStats (@author ATracer, sphinx).</summary>
public class PlayerLifeStats : CreatureLifeStats<Player>
{
    private readonly object fpLock = new object();
    private int flightReducePeriod = 2;
    private int flightReduceValue = 1;
    private int currentFp;
    private ScheduledTask flyRestoreTask;
    private ScheduledTask flyReduceTask;

    public PlayerLifeStats(Player owner)
        : base(owner, owner.GetGameStats().GetMaxHp().GetCurrent(), owner.GetGameStats().GetMaxMp().GetCurrent())
    {
        this.currentFp = owner.GetGameStats().GetFlyTime().GetCurrent();
    }

    protected override void OnHpChanged(int previousHp, int newHp, Creature effector)
    {
        if (IsFullyRestoredHp()) // FIXME: Temp Fix: Reset aggro list when hp is full
            owner.GetAggroList().Clear();
        if (owner.IsSpawned())
        {
            SendHpPacketUpdate();
            SendGroupPacketUpdate();
            if (previousHp == 0 || newHp < previousHp)
                TriggerRestoreTask();
            if (previousHp == 0)
                TriggerFpRestore();
        }
        base.OnHpChanged(previousHp, newHp, effector);
    }

    protected override void OnMpChanged(int previousMp, int newMp)
    {
        base.OnMpChanged(previousMp, newMp);
        if (owner.IsSpawned())
        {
            SendMpPacketUpdate();
            SendGroupPacketUpdate();
            if (newMp < previousMp)
                TriggerRestoreTask();
        }
    }

    private void SendGroupPacketUpdate()
    {
        if (owner.IsInTeam())
            Aion.GameServer.Taskmanager.Tasks.TeamStatUpdater.GetInstance().Add(owner);
    }

    public override void SynchronizeWithMaxStats()
    {
        if (IsDead())
            return;

        base.SynchronizeWithMaxStats();
        currentFp = GetMaxFp();

        if (owner.IsSpawned())
        {
            SendHpPacketUpdate();
            SendMpPacketUpdate();
            SendFpPacketUpdate();
        }
    }

    public override void UpdateCurrentStats()
    {
        base.UpdateCurrentStats();

        if (!IsFullyRestoredHpMp())
            TriggerRestoreTask();

        if (GetMaxFp() < currentFp)
            currentFp = GetMaxFp();

        if (owner.GetFlyState() == 0 && !owner.IsInSprintMode())
            TriggerFpRestore();
    }

    private void SendHpPacketUpdate()
    {
        PacketSendUtility.SendPacket(owner, new SM_STATUPDATE_HP(GetCurrentHp(), GetMaxHp()));
    }

    private void SendMpPacketUpdate()
    {
        PacketSendUtility.SendPacket(owner, new SM_STATUPDATE_MP(GetCurrentMp(), GetMaxMp()));
    }

    public override int GetCurrentFp()
    {
        return this.currentFp;
    }

    public override int GetMaxFp()
    {
        return owner.GetGameStats().GetFlyTime().GetCurrent();
    }

    /// <summary>FP percentage 0 - 100.</summary>
    public int GetFpPercentage()
    {
        return 100 * currentFp / GetMaxFp();
    }

    /// <summary>Called whenever caller wants to restore the creature's FP.</summary>
    public int IncreaseFp(TYPE type, int value, int skillId, LOG log)
    {
        lock (fpLock)
        {
            if (IsDead())
            {
                return 0;
            }
            int newFp = this.currentFp + value;
            if (newFp > GetMaxFp())
            {
                newFp = GetMaxFp();
                value = GetMaxFp() - this.currentFp;
            }
            if (currentFp != newFp)
            {
                this.currentFp = newFp;
                OnIncreaseFp(type, value, skillId, log);
            }
        }

        return currentFp;
    }

    /// <summary>Called whenever caller wants to reduce the creature's FP. Returns current flight points.</summary>
    public int ReduceFp(TYPE type, int value, int skillId, LOG? log)
    {
        lock (fpLock)
        {
            int newFp = this.currentFp - value;

            if (newFp < 0)
            {
                newFp = 0;
                value = this.currentFp;
            }

            this.currentFp = newFp;
        }

        OnReduceFp(type, value, skillId, log);

        return currentFp;
    }

    public int SetCurrentFp(int value)
    {
        lock (fpLock)
        {
            int newFp = value;

            if (newFp < 0)
                newFp = 0;

            this.currentFp = newFp;
        }

        OnReduceFp(null, value, 0, null);

        return currentFp;
    }

    protected void OnIncreaseFp(TYPE type, int value, int skillId, LOG? log)
    {
        if (value > 0)
        {
            SendAttackStatusPacketUpdate(type, value, skillId, log);
            SendFpPacketUpdate();
        }
    }

    protected void OnReduceFp(TYPE type, int value, int skillId, LOG? log)
    {
        SendAttackStatusPacketUpdate(type, value, skillId, log);
        SendFpPacketUpdate();
    }

    public void SendFpPacketUpdate()
    {
        PacketSendUtility.SendPacket(owner, new SM_FLY_TIME(currentFp, GetMaxFp()));
    }

    /// <summary>This method should be used only on FlyTimeRestoreService.</summary>
    public void RestoreFp()
    {
        // how much fly time restoring per 6 second.
        IncreaseFp(TYPE.NATURAL_FP, 3, 0, LOG.REGULAR);
    }

    public void SpecialrestoreFp()
    {
        if (owner.GetGameStats().GetStat(StatEnum.REGEN_FP, 0).GetCurrent() != 0)
            IncreaseFp(TYPE.NATURAL_FP, owner.GetGameStats().GetStat(StatEnum.REGEN_FP, 0).GetCurrent() / 3, 0, LOG.REGULAR);
    }

    public void TriggerFpRestore()
    {
        lock (restoreLock)
        {
            CancelFpReduce();
            if (flyRestoreTask == null && !IsDead() && !IsFlyTimeFullyRestored())
            {
                flyRestoreTask = Aion.GameServer.Services.LifeStatsRestoreService.GetInstance().ScheduleFpRestoreTask(this);
            }
        }
    }

    public void CancelFpRestore()
    {
        lock (restoreLock)
        {
            if (flyRestoreTask != null && !flyRestoreTask.Completion.IsCanceled)
            {
                flyRestoreTask.Cancel();
                flyRestoreTask = null;
            }
        }
    }

    public void TriggerFpReduce()
    {
        if (owner.HasAccess(AdminConfig.UNLIMITED_FLIGHT_TIME) || IsDead())
            return;
        lock (restoreLock)
        {
            if (owner.IsInSprintMode())
            {
                flightReduceValue = owner.ride.GetCostFp().Value;
                flightReducePeriod = 1;
            }
            else if (owner.IsFlying())
            {
                bool isInFlyArea = owner.IsInsideZoneType(ZoneType.FLY) && !owner.IsInsideZoneType(ZoneType.NO_FLY);
                flightReduceValue = isInFlyArea ? 1 : 2;
                flightReducePeriod = isInFlyArea && owner.IsInGlidingState() ? 2 : 1;
            }
            else
            {
                return;
            }
            CancelFpRestore();
            if (flyReduceTask == null && !IsDead())
                flyReduceTask = Aion.GameServer.Services.LifeStatsRestoreService.GetInstance().ScheduleFpReduceTask(this);
        }
    }

    public void CancelFpReduce()
    {
        lock (restoreLock)
        {
            if (flyReduceTask != null && !flyReduceTask.Completion.IsCanceled)
            {
                flyReduceTask.Cancel();
                flyReduceTask = null;
            }
        }
    }

    public bool IsFlyTimeFullyRestored()
    {
        return GetMaxFp() == currentFp;
    }

    public override void CancelAllTasks()
    {
        base.CancelAllTasks();
        CancelFpReduce();
        CancelFpRestore();
    }

    public int GetFlightReducePeriod()
    {
        return flightReducePeriod;
    }

    public int GetFlightReduceValue()
    {
        return flightReduceValue;
    }
}
