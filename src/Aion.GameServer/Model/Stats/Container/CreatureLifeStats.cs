using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>
/// Java parity: model/stats/container/CreatureLifeStats&lt;T extends Creature&gt; (@author ATracer).
/// </summary>
public abstract class CreatureLifeStats
{
    private int currentHp;
    private int cachedMaxHp;
    private int currentMp;
    private int cachedMaxMp;
    private int killingBlow; // for long animation skills that will kill - last damage
    protected readonly Creature owner;
    protected readonly object restoreLock = new object();
    protected ScheduledTask lifeRestoreTask;

    public CreatureLifeStats(Creature owner)
    {
        this.owner = owner;
        currentHp = cachedMaxHp = GetMaxHp();
        currentMp = cachedMaxMp = GetMaxMp();
    }

    // Java parity helper: Math.round(float) = floor(x+0.5) (C# Math.Round is banker's rounding).
    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);

    public Creature GetOwner()
    {
        return owner;
    }

    // Property-form bridges over the getCurrentX() accessors (reworked call sites use lifeStats.CurrentHp, ...).
    public int CurrentHp => GetCurrentHp();
    public int CurrentMp => GetCurrentMp();
    public int CurrentFp => GetCurrentFp();

    public int GetCurrentHp()
    {
        return currentHp;
    }

    internal void ResetFreshNpcStatisticsSelection()
    {
        if (owner is not Npc || owner.IsInWorld() || owner.IsSpawned())
            throw new InvalidOperationException("Only an unpublished NPC can reconstruct its fresh stat seed");
        lock (this)
        {
            currentHp = cachedMaxHp = GetMaxHp();
            currentMp = cachedMaxMp = GetMaxMp();
        }
    }

    public int GetCurrentMp()
    {
        return currentMp;
    }

    public int GetMaxHp()
    {
        return GetOwner().GetGameStats().GetMaxHp().GetCurrent();
    }

    public int GetMaxMp()
    {
        return GetOwner().GetGameStats().GetMaxMp().GetCurrent();
    }

    public bool IsDead()
    {
        return currentHp == 0;
    }

    public bool IsAboutToDie()
    {
        return killingBlow != 0;
    }

    public void SetKillingBlow(int killingBlow)
    {
        this.killingBlow = killingBlow;
    }

    private void UnsetIsAboutToDie()
    {
        this.killingBlow = 0;
    }

    /// <summary>
    /// Called whenever caller wants to absorb the creature's HP. If <paramref name="type"/> is null, no SM_ATTACK_STATUS packet is sent.
    /// Returns the HP that this creature has left. If 0, the creature died.
    /// </summary>
    public int ReduceHp(TYPE type, int value, int skillId, LOG? log, Creature attacker)
    {
        return ReduceHp(type, value, skillId, log, attacker, false);
    }

    public int ReduceHp(TYPE type, int value, int skillId, LOG? log, Creature attacker, bool criticalHit)
    {
        if (attacker == null)
            throw new ArgumentNullException(nameof(attacker));
        if (GetOwner().IsInvulnerable())
        {
            UnsetIsAboutToDie();
            return currentHp;
        }

        int previousHp, newHp;
        lock (this)
        {
            if (IsDead())
                return 0;

            previousHp = currentHp;
            int minHp = type == TYPE.USED_HP ? 1 : 0; // a skill cost drains its caster down to 1 hp, it never kills him
            currentHp = newHp = Math.Clamp(currentHp - value, Math.Min(minHp, currentHp), currentHp);
            if (IsDead())
            {
                currentMp = 0;
                UnsetIsAboutToDie();
            }
        }

        if (newHp != previousHp || skillId != 0)
            SendAttackStatusPacketUpdate(type, previousHp - newHp, skillId, log, criticalHit);
        if (newHp != previousHp)
            OnHpChanged(type, previousHp, newHp, attacker);
        return newHp;
    }

    /// <summary>
    /// Called whenever caller wants to absorb the creature's MP. If <paramref name="type"/> is null, no SM_ATTACK_STATUS packet is sent.
    /// Returns the MP that this creature has left.
    /// </summary>
    public int ReduceMp(TYPE type, int value, int skillId, LOG log)
    {
        int previousMp, newMp;
        lock (this)
        {
            if (IsDead())
                return 0;

            previousMp = currentMp;
            currentMp = newMp = Math.Clamp(currentMp - value, 0, currentMp);
        }

        if (newMp != previousMp || skillId != 0)
            SendAttackStatusPacketUpdate(type, previousMp - newMp, skillId, log);
        if (newMp != previousMp)
            OnMpChanged(type, previousMp, newMp);
        return newMp;
    }

    protected void SendAttackStatusPacketUpdate(TYPE type, int value, int skillId, LOG? log)
    {
        SendAttackStatusPacketUpdate(type, value, skillId, log, false);
    }

    protected void SendAttackStatusPacketUpdate(TYPE type, int value, int skillId, LOG? log, bool criticalHit)
    {
        if (type != null)
            PacketSendUtility.BroadcastToSightedPlayers(owner, new SM_ATTACK_STATUS(owner, type, skillId, value, log!.Value, criticalHit), true);
    }

    /// <summary>Called whenever caller wants to restore the creature's HP. Returns currentHp.</summary>
    public int IncreaseHp(TYPE type, int value)
    {
        return IncreaseHp(type, value, GetOwner(), 0, LOG.REGULAR);
    }

    public int IncreaseHp(TYPE type, int value, Creature effector)
    {
        return IncreaseHp(type, value, effector, 0, LOG.REGULAR);
    }

    public int IncreaseHp(TYPE type, int value, Effect effect, LOG log)
    {
        return IncreaseHp(type, value, effect.GetEffector(), effect.GetSkillId(), log);
    }

    private int IncreaseHp(TYPE type, int value, Creature effector, int skillId, LOG log)
    {
        if (value < 0) // some skills reduce hp via a negative heal (e.g. 3732 Spirit Absorption)
            return ReduceHp(type, -value, skillId, log, effector);

        if (GetOwner().GetEffectController().IsAbnormalSet(AbnormalState.DISEASE))
            return currentHp;

        int previousHp, newHp;
        lock (this)
        {
            if (IsDead())
                return 0;

            previousHp = currentHp;
            currentHp = newHp = Math.Min(currentHp + value, GetMaxHp());
            if (killingBlow != 0 && newHp > killingBlow)
                UnsetIsAboutToDie();
        }

        if (newHp != previousHp || skillId != 0)
            SendAttackStatusPacketUpdate(type, newHp - previousHp, skillId, log);
        if (newHp != previousHp)
            OnHpChanged(type, previousHp, newHp, effector == null ? GetOwner() : effector);
        return newHp;
    }

    /// <summary>Called whenever caller wants to restore the creature's MP. Returns currentMp.</summary>
    public int IncreaseMp(int value)
    {
        return IncreaseMp(null, value, 0, null);
    }

    public int IncreaseMp(TYPE type, int value, int skillId, LOG? log)
    {
        int previousMp, newMp;
        lock (this)
        {
            if (IsDead())
                return 0;

            previousMp = currentMp;
            currentMp = newMp = Math.Max(currentMp, Math.Min(currentMp + value, GetMaxMp()));
        }

        if (newMp != previousMp || skillId != 0)
            SendAttackStatusPacketUpdate(type, newMp - previousMp, skillId, log);
        if (newMp != previousMp)
            OnMpChanged(type, previousMp, newMp);
        return currentMp;
    }

    /// <summary>Restores HP with value set as HP_RESTORE_TICK.</summary>
    public void RestoreHp()
    {
        IncreaseHp(TYPE.NATURAL_HP, GetOwner().GetGameStats().GetHpRegenRate().GetCurrent());
    }

    /// <summary>Restores MP with value set as MP_RESTORE_TICK.</summary>
    public void RestoreMp()
    {
        IncreaseMp(TYPE.NATURAL_MP, GetOwner().GetGameStats().GetMpRegenRate().GetCurrent(), 0, LOG.REGULAR);
    }

    /// <summary>Will trigger restore task if not already running.</summary>
    public virtual void TriggerRestoreTask()
    {
        lock (restoreLock)
        {
            if (lifeRestoreTask == null && !IsDead())
            {
                lifeRestoreTask = Aion.GameServer.Services.LifeStatsRestoreService.GetInstance().ScheduleRestoreTask(this);
            }
        }
    }

    /// <summary>Cancel currently running restore task.</summary>
    public void CancelRestoreTask()
    {
        lock (restoreLock)
        {
            if (lifeRestoreTask != null)
            {
                lifeRestoreTask.Cancel();
                lifeRestoreTask = null;
            }
        }
    }

    public bool IsFullyRestoredHpMp()
    {
        return GetMaxHp() == currentHp && GetMaxMp() == currentMp;
    }

    public bool IsFullyRestoredHp()
    {
        return GetMaxHp() == currentHp;
    }

    public bool IsFullyRestoredMp()
    {
        return GetMaxMp() == currentMp;
    }

    /// <summary>
    /// Synchronize current HP and MP with updated MAXHP and MAXMP stats. Should be called only on creature load to game or player level up.
    /// </summary>
    public virtual void SynchronizeWithMaxStats()
    {
        lock (this)
        {
            cachedMaxHp = GetMaxHp();
            cachedMaxMp = GetMaxMp();
        }
        if (currentHp != cachedMaxHp)
            SetCurrentHp(cachedMaxHp);
        if (currentMp != cachedMaxMp)
            SetCurrentMp(cachedMaxMp);
    }

    /// <summary>
    /// Synchronize current HP and MP with MAXHP and MAXMP when max stats were decreased below current level.
    /// </summary>
    public virtual void UpdateCurrentStats()
    {
        int maxHp = GetMaxHp();
        if (maxHp < currentHp)
            currentHp = maxHp;

        int maxMp = GetMaxMp();
        if (maxMp < currentMp)
            currentMp = maxMp;
    }

    /// <summary>HP percentage 0 - 100 (minimum 1% while alive).</summary>
    public int GetHpPercentage()
    {
        if (currentHp == 0)
            return 0;
        return Math.Max(1, (int)(100f * currentHp / GetMaxHp()));
    }

    /// <summary>MP percentage 0 - 100.</summary>
    public int GetMpPercentage()
    {
        return (int)(100f * currentMp / GetMaxMp());
    }

    protected virtual void OnHpChanged(TYPE type, int previousHp, int newHp, Creature effector)
    {
        if (newHp == 0)
            GetOwner().GetController().OnDie(effector);
        GetOwner().GetObserveController().NotifyHPChangeObservers(newHp);
    }

    protected virtual void OnMpChanged(TYPE type, int previousMp, int newMp)
    {
    }

    public virtual int GetMaxFp()
    {
        return 0;
    }

    public virtual int GetCurrentFp()
    {
        return 0;
    }

    /// <summary>Cancel all tasks when player logs out.</summary>
    public virtual void CancelAllTasks()
    {
        CancelRestoreTask();
    }

    /// <summary>Fully restore owner's HP and remove dead state of lifestats.</summary>
    public void SetCurrentHpPercent(int hpPercent)
    {
        SetCurrentHp((int)((long)GetMaxHp() * hpPercent / 100));
    }

    /// <summary>Sets the current HP without notifying observers.</summary>
    public void SetCurrentHp(int hp)
    {
        SetCurrentHp(hp, owner);
    }

    public void SetCurrentHp(int hp, Creature effector)
    {
        int previousHp, newHp;
        lock (this)
        {
            previousHp = currentHp;
            int maxHp = GetMaxHp();
            currentHp = newHp = Math.Clamp(hp, 0, maxHp);
            if (previousHp == 0 || newHp == 0)
            {
                cachedMaxHp = maxHp;
                cachedMaxMp = GetMaxMp();
            }
            if (killingBlow != 0 && (newHp == 0 || newHp > killingBlow))
                UnsetIsAboutToDie();
        }
        if (newHp != previousHp)
        {
            // broadcast current hp percentage to others
            PacketSendUtility.BroadcastToSightedPlayers(owner, new SM_ATTACK_STATUS(owner, TYPE.HP, 0, 0, LOG.REGULAR));
            OnHpChanged(TYPE.HP, previousHp, newHp, effector);
        }
    }

    public void SetCurrentMp(int value)
    {
        int previousMp, newMp;
        lock (this)
        {
            if (IsDead())
                return;
            previousMp = currentMp;
            currentMp = newMp = Math.Clamp(value, 0, GetMaxMp());
        }
        if (newMp != previousMp)
        {
            PacketSendUtility.BroadcastToSightedPlayers(owner, new SM_ATTACK_STATUS(owner, TYPE.HEAL_MP, 0, 0, LOG.MPHEAL));
            OnMpChanged(TYPE.HEAL_MP, previousMp, newMp);
        }
    }

    /// <summary>Fully restore owner's MP.</summary>
    public void SetCurrentMpPercent(int mpPercent)
    {
        SetCurrentMp((int)((long)GetMaxMp() * mpPercent / 100));
    }

    public void OnStatsChange(Effect effect)
    {
        if (IsDead())
            return;
        CheckMaxHPChanged(effect == null ? owner : effect.GetEffector());
        CheckMaxMPChanged();
    }

    private void CheckMaxHPChanged(Creature effector)
    {
        int newHp;
        lock (this)
        {
            newHp = currentHp;
            int currentMaxHp = GetMaxHp();
            if (cachedMaxHp != currentMaxHp)
            {
                newHp = Math.Max(1, JRound((long)currentHp * currentMaxHp / (float)cachedMaxHp));
                cachedMaxHp = currentMaxHp;
            }
        }
        if (newHp != currentHp)
            SetCurrentHp(newHp, effector);
    }

    private void CheckMaxMPChanged()
    {
        int newMp;
        lock (this)
        {
            newMp = currentMp;
            int currentMaxMp = GetMaxMp();
            if (cachedMaxMp != currentMaxMp)
            {
                newMp = Math.Max(1, JRound((long)currentMp * currentMaxMp / (float)cachedMaxMp));
                cachedMaxMp = currentMaxMp;
            }
        }
        if (newMp != currentMp)
            SetCurrentMp(newMp);
    }
}

/// <summary>
/// Java parity: generic typing of <see cref="CreatureLifeStats"/> (Java <c>CreatureLifeStats&lt;T extends Creature&gt;</c>).
/// Non-generic base + generic shim so <c>Creature.GetLifeStats()</c> can return the non-generic base (no C# wildcard
/// generics). Subclasses extend <c>CreatureLifeStats&lt;Player&gt;</c> etc. and see <c>owner</c>/<c>GetOwner()</c> typed as T.
/// </summary>
public abstract class CreatureLifeStats<T> : CreatureLifeStats where T : Creature
{
    public CreatureLifeStats(T owner) : base(owner)
    {
    }

    protected new T owner => (T)base.owner;

    public new T GetOwner() => (T)base.owner;
}
