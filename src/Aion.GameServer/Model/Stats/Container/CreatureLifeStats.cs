using Aion.GameServer.Controllers.Attack.RetailNpc;
using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
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

    /// <summary>Some plain-NPC factories assign their owner after stat-container
    /// construction. Retire only the new statistics selection and reconstruct its
    /// fresh HP seed before world publication; no observer, task or heal is emitted.</summary>
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
        return ReduceHpCore(type,value,skillId,log,attacker,criticalHit);
    }

    // Only an actual selected periodic registry lookup can admit a missing source.
    // Legacy public ReduceHp still rejects null. Both use the same HP commit/events.
    internal int ReduceRetailPeriodicHp(SkyTempleNpcPeriodicEffects.TickDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        return ReduceHpCore(TYPE.DAMAGE,0,0,null,null!,false,dispatch);
    }

    internal int ReduceRetailSkillHp(SkyTempleNpcSkillTiming.ImpactDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        return ReduceHpCore(TYPE.REGULAR,0,0,null,null!,false,null,dispatch);
    }

    internal int ReduceStatisticsPeriodicHp(RetailNpcStatPeriodicCombat.TickDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        return ReduceHpCore(TYPE.DAMAGE,0,0,null,null!,false,statPeriodic:dispatch);
    }

    internal int ReducePreparedNpcDrainInitialHp(RetailNpcStatSpellDrain.NpcInitialDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        return ReduceHpCore(TYPE.REGULAR,dispatch.Damage,dispatch.Effect.GetSkillId(),LOG.REGULAR,
            dispatch.Caster,false,statInitial:dispatch);
    }

    internal int ReduceRetailPlayerDrainHp(RetailNpcStatSpellDrain.PlayerInitialDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        bool provoked=dispatch.Effect.GetSkillTemplate().GetActivationAttribute()==ActivationAttribute.PROVOKED;
        return ReduceHpCore(provoked?TYPE.DAMAGE:TYPE.REGULAR,dispatch.Damage,dispatch.Effect.GetSkillId(),
            provoked?LOG.PROCATKINSTANT:LOG.REGULAR,dispatch.Caster,false,playerDrain:dispatch);
    }

    private int ReduceHpCore(TYPE type,int value,int skillId,LOG? log,Creature attacker,bool criticalHit,
        SkyTempleNpcPeriodicEffects.TickDispatch? periodic=null,SkyTempleNpcSkillTiming.ImpactDispatch? impact=null,
        RetailNpcStatPeriodicCombat.TickDispatch? statPeriodic=null,
        RetailNpcStatSpellDrain.NpcInitialDispatch? statInitial=null,
        RetailNpcStatSpellDrain.PlayerInitialDispatch? playerDrain=null)
    {
        if (periodic is null && impact is null && statPeriodic is null && statInitial is null && playerDrain is null && GetOwner().IsInvulnerable())
        {
            UnsetIsAboutToDie();
            return currentHp;
        }

        int previousHp, newHp;
        lock (this)
        {
            statPeriodic?.EnterHp(GetOwner());
            using var drainProtection=statPeriodic?.EnterNpcDrainProtection(GetOwner()) ?? statInitial?.EnterHp(GetOwner())
                ?? playerDrain?.EnterHp(GetOwner());
            if(playerDrain is not null && GetOwner().IsInvulnerable())
                throw new Aion.GameServer.Controllers.ObserveController.PlayerDrainProtectionChangedException();
            if(statInitial is not null && GetOwner().IsInvulnerable())
                throw new RetailNpcStatSpellDrain.NpcInitialRetiredException("Unreviewed NPC drain initial invulnerability");
            if(statPeriodic is not null)
            {
                value=statPeriodic.Damage;skillId=statPeriodic.Effect.GetSkillId();
                log=statPeriodic.Log;attacker=statPeriodic.Caster!;criticalHit=statPeriodic.Critical;
                if(GetOwner().IsInvulnerable())
                {
                    statPeriodic.RecordHpLoss(GetOwner(),0);
                    UnsetIsAboutToDie();return currentHp;
                }
            }
            if (impact is not null)
            {
                var current=impact.EnterHp(GetOwner());
                value=current.Damage;skillId=current.Effect.GetSkillId();log=LOG.REGULAR;attacker=current.Source;
                if(GetOwner().IsInvulnerable()){UnsetIsAboutToDie();return currentHp;}
            }
            if (periodic is not null)
            {
                // Final lifetime validation and the HP commit share the retirement lock.
                // The periodic owner already holds its own gate; notifications stay outside
                // this HP lock so death/end callbacks cannot invert HP -> periodic gate.
                var current=periodic.EnterHp(GetOwner());
                value=current.Damage;skillId=current.Effect.GetSkillId();log=current.Log;
                attacker=current.Source!;criticalHit=current.Critical;
                if (GetOwner().IsInvulnerable())
                {
                    UnsetIsAboutToDie();
                    return currentHp;
                }
            }
            if (IsDead())
            {
                statPeriodic?.RecordHpLoss(GetOwner(),0);
                return 0;
            }

            previousHp = currentHp;
            int minHp = type == TYPE.USED_HP ? 1 : 0; // a skill cost drains its caster down to 1 hp, it never kills him
            currentHp = newHp = Math.Clamp(currentHp - value, Math.Min(minHp, currentHp), currentHp);
            statPeriodic?.RecordHpLoss(GetOwner(),previousHp-newHp);
            statInitial?.MarkCommitted(GetOwner());
            playerDrain?.MarkCommitted(GetOwner());
            if (IsDead())
            {
                if(this is NpcLifeStats npcLife)npcLife.RetireRetailDrainLife();
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

    // The selected NPC heartbeat supplies a lifetime check under the same HP lock.
    // Legacy callers retain their original arithmetic and notification path.
    internal int IncreaseRetailNpcHp(int value, Func<bool> currentLifetime)
    {
        ArgumentNullException.ThrowIfNull(currentLifetime);
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        return IncreaseHp(TYPE.NATURAL_HP, value, null!, 0, LOG.REGULAR, currentLifetime);
    }

    // Drain eligibility and the HP write share the same owner lock as life
    // retirement. Notifications still run after releasing that lock.
    internal bool IncreaseRetailNpcDrainHp(int value,Effect effect,Func<bool> currentLifetime)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(currentLifetime);
        if(owner is not Npc)throw new InvalidOperationException("Retail NPC drain heal requires an NPC owner");
        if(value<=0)throw new ArgumentOutOfRangeException(nameof(value));
        IncreaseHpCore(TYPE.HP,value,effect.GetEffector(),effect.GetSkillId(),LOG.SPELLATKDRAIN,
            currentLifetime,out bool admitted,rejectDrainOverflow:true);
        return admitted;
    }

    // DelayedDrain calls the retail NPC Heal2 owner, which requires current
    // HP > 0. The legacy host IsDead only checks equality with zero.
    internal void IncreaseRetailNpcInstantDrainHp(int value,Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if(owner is not Npc)throw new InvalidOperationException("Retail NPC drain heal requires an NPC owner");
        if(value<=0)throw new ArgumentOutOfRangeException(nameof(value));
        IncreaseHpCore(TYPE.HP,value,effect.GetEffector(),effect.GetSkillId(),LOG.SPELLATKDRAININSTANT,
            null,out _,requirePositiveHp:true);
    }

    // The prepared NPC-pair fixture retains the captured HP owner, just as
    // the native timer retains its caster. Unrepresented signed overflow is
    // rejected under the HP lock; this does not model every native life state.
    internal void IncreasePreparedNpcInstantDrainHp(int value,Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if(owner is not Npc ||!ReferenceEquals(effect.GetEffector(),owner))
            throw new InvalidOperationException("Prepared NPC drain heal owner differs");
        if(value<=0)throw new ArgumentOutOfRangeException(nameof(value));
        IncreaseHpCore(TYPE.HP,value,effect.GetEffector(),effect.GetSkillId(),LOG.SPELLATKDRAININSTANT,
            ()=>ReferenceEquals(owner.GetLifeStats(),this) ? true : throw new InvalidOperationException("Prepared NPC drain heal life owner changed"),
            out _,rejectDrainOverflow:true,requirePositiveHp:true);
    }

    private int IncreaseHp(TYPE type, int value, Creature effector, int skillId, LOG log, Func<bool>? currentLifetime = null)
        => IncreaseHpCore(type,value,effector,skillId,log,currentLifetime,out _);

    private int IncreaseHpCore(TYPE type,int value,Creature effector,int skillId,LOG log,Func<bool>? currentLifetime,out bool admitted,
        bool rejectDrainOverflow=false,bool requirePositiveHp=false)
    {
        admitted=false;
        if (value < 0) // some skills reduce hp via a negative heal (e.g. 3732 Spirit Absorption)
            return ReduceHp(type, -value, skillId, log, effector);

        if (GetOwner().GetEffectController().IsAbnormalSet(AbnormalState.DISEASE))
            return currentHp;

        int previousHp, newHp;
        lock (this)
        {
            if (IsDead() || (requirePositiveHp && currentHp<0))
                return 0;
            if (currentLifetime is not null && !currentLifetime()) return currentHp;
            // Native Heal2 adds signed 32-bit HP before its signed max check.
            // Its overflow result is not yet representable by host life state.
            if (rejectDrainOverflow && (long)currentHp+value>int.MaxValue)
                throw new InvalidOperationException("Unreviewed NPC drain heal HP overflow");
            admitted=true;

            previousHp = currentHp;
            currentHp = newHp = currentLifetime is null
                ? Math.Min(currentHp + value, GetMaxHp())
                : (int)Math.Clamp((long)currentHp + value, 0L, GetMaxHp());
            if (killingBlow != 0 && newHp > killingBlow)
                UnsetIsAboutToDie();
        }

        if (currentLifetime is not null && type==TYPE.NATURAL_HP && newHp != previousHp)
            SkyTempleNpcDiagnostics.Regenerated((Npc)owner, previousHp, newHp, value);
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
            if(previousHp!=0 && newHp==0 && this is NpcLifeStats npcLife)npcLife.RetireRetailDrainLife();
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

    // Restricted to the explicitly prepared curse owner. Both current-resource
    // updates and cached maxima commit under the life lock; callbacks remain
    // outside it. The supplied maxima and lifecycle guard still need complete
    // retail equipment/effect admission before any production caller exists.
    internal bool ApplyPreparedRetailCurseResources(Effect effect,int oldHpMax,int newHpMax,
        int oldMpMax,int newMpMax,Func<bool> currentLifetime,Action? onCommitted=null,
        Func<bool>? tryReserveStats=null,Action? releaseStats=null)
        => UpdatePreparedRetailCurseResources(effect,oldHpMax,newHpMax,oldMpMax,newMpMax,currentLifetime,onCommitted,tryReserveStats,releaseStats);

    // On removal, other stat owners may have changed both maxima since start.
    // Reconcile from the cached current maxima to the actually observed maxima
    // after removing this effect's own functions; no frozen plan is assumed.
    internal bool ReconcilePreparedRetailCurseRemoval(Effect effect,Func<bool> currentLifetime,Action? onCommitted=null)
        => UpdatePreparedRetailCurseResources(effect,null,null,null,null,currentLifetime,onCommitted,null,null);

    private bool UpdatePreparedRetailCurseResources(Effect effect,int? expectedOldHpMax,int? expectedNewHpMax,
        int? expectedOldMpMax,int? expectedNewMpMax,Func<bool> currentLifetime,Action? onCommitted,
        Func<bool>? tryReserveStats,Action? releaseStats)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(currentLifetime);
        if((tryReserveStats is null)!=(releaseStats is null))throw new ArgumentException("Incomplete stat reservation");
        if(owner is not Aion.GameServer.Model.GameObjects.Players.Player || !ReferenceEquals(effect.GetEffected(),owner)
            ||!RetailNpcStatSpellDrain.HasPreparedCurse(effect))
            throw new InvalidOperationException("Prepared curse resource owner differs");
        int oldHp,newHp,oldMp,newMp,oldHpMax,newHpMax,oldMpMax,newMpMax;
        lock(this)
        {
            if(IsDead() || !currentLifetime())return false;
            oldHpMax=cachedMaxHp;oldMpMax=cachedMaxMp;newHpMax=GetMaxHp();newMpMax=GetMaxMp();
            if(expectedOldHpMax is { } eh && oldHpMax!=eh || expectedNewHpMax is { } nh && newHpMax!=nh
                ||expectedOldMpMax is { } em && oldMpMax!=em || expectedNewMpMax is { } nm && newMpMax!=nm)
                throw new InvalidOperationException("Prepared curse resource maxima changed");
            oldHp=currentHp;oldMp=currentMp;
            newHp=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,oldHpMax,newHpMax,oldHp);
            newMp=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Mp,oldMpMax,newMpMax,oldMp);
            if(tryReserveStats is not null &&!tryReserveStats())return false;
            try
            {
                currentHp=newHp;currentMp=newMp;cachedMaxHp=newHpMax;cachedMaxMp=newMpMax;
                if(killingBlow!=0 && (newHp==0 || newHp>killingBlow))UnsetIsAboutToDie();
            }
            finally { releaseStats?.Invoke(); }
            onCommitted?.Invoke(); // Internal marker only; notifications remain outside this lock.
        }
        if(oldHp!=newHp)
        {
            PacketSendUtility.BroadcastToSightedPlayers(owner,new SM_ATTACK_STATUS(owner,TYPE.HP,0,0,LOG.REGULAR));
            OnHpChanged(TYPE.HP,oldHp,newHp,effect.GetEffector());
        }
        if(oldMp!=newMp)
        {
            PacketSendUtility.BroadcastToSightedPlayers(owner,new SM_ATTACK_STATUS(owner,TYPE.HEAL_MP,0,0,LOG.MPHEAL));
            OnMpChanged(TYPE.HEAL_MP,oldMp,newMp);
        }
        if(this is PlayerLifeStats playerLife)
            playerLife.NotifyPreparedRetailCurseMaxima(oldHp==newHp && oldHpMax!=newHpMax,
                oldMp==newMp && oldMpMax!=newMpMax);
        return true;
    }

    private void CheckMaxHPChanged(Creature effector)
    {
        int previousHp, newHp;
        bool retailHandled = false, retailCommitted = false;
        lock (this)
        {
            previousHp = newHp = currentHp;
            var maximum = owner.GetGameStats().GetMaxHp();
            int currentMaxHp = maximum.GetCurrent();
            if (cachedMaxHp != currentMaxHp)
            {
                if (owner is Npc && maximum.HasUnchangedExactIntegerCurrent())
                {
                    retailHandled = true;
                    newHp = RetailNpcCurrentHpMath.Adjust(cachedMaxHp, currentMaxHp, currentHp);
                    if (newHp != previousHp)
                    {
                        // Native NPC recalculation writes its current HP directly;
                        // float rounding can briefly leave it above its new maximum.
                        currentHp = newHp;
                        if (killingBlow != 0 && (newHp == 0 || newHp > killingBlow))
                            UnsetIsAboutToDie();
                        retailCommitted = true;
                    }
                }
                else
                    newHp = Math.Max(1, JRound((long)currentHp * currentMaxHp / (float)cachedMaxHp));
                cachedMaxHp = currentMaxHp;
            }
        }
        if (retailCommitted)
        {
            PacketSendUtility.BroadcastToSightedPlayers(owner, new SM_ATTACK_STATUS(owner, TYPE.HP, 0, 0, LOG.REGULAR));
            OnHpChanged(TYPE.HP, previousHp, newHp, effector);
        }
        else if (!retailHandled && newHp != currentHp)
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
