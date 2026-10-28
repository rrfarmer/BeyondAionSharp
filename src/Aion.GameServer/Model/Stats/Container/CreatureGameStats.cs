using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>
/// Java parity: model/stats/container/CreatureGameStats&lt;T extends Creature&gt; (@author xavier, Neon).
/// </summary>
/// <remarks>
/// Generics kept faithful (<c>T : Creature</c>, typed <c>owner</c>). Java <c>Creature.getGameStats()</c> returns the
/// wildcard <c>CreatureGameStats&lt;?&gt;</c>; C# has no wildcard generics, so the non-generic return type for
/// <c>Creature.GetGameStats()</c> will be settled when Creature is ported (likely via a non-generic base/interface).
/// </remarks>
public abstract class CreatureGameStats
{
    private const int ATTACK_MAX_COUNTER = int.MaxValue;

    protected readonly Creature owner;
    private readonly ConcurrentDictionary<StatEnum, List<IStatFunction>> stats = new ConcurrentDictionary<StatEnum, List<IStatFunction>>();

    // Low 31 bits count active collection writers, bit 31 is a short resource
    // commit reservation, and high 32 bits count completed writes. A single
    // atomic read can detect both an active writer
    // and an add/remove pair during one retail curse stat observation. This
    // does not cover mutations inside a function or reserve the observed state.
    private long retailStatMutationStamp;
    internal sealed class RetailStatMutationOwner
    {
        internal long Completed;
        internal readonly object Gate=new();
        internal HashSet<IStatFunction>? Expected;
    }
    private System.Runtime.CompilerServices.ConditionalWeakTable<Effect,RetailStatMutationOwner>? retailStatOwners;
    private const long RetailStatReservation=0x80000000L;
    private void BeginRetailStatMutation()
    {
        var spin=new System.Threading.SpinWait();
        while(true)
        {
            long stamp=System.Threading.Volatile.Read(ref retailStatMutationStamp);
            if((stamp & RetailStatReservation)!=0){spin.SpinOnce();continue;}
            if((stamp & 0x7fffffffL)==0x7fffffffL)throw new InvalidOperationException("Too many concurrent stat writers");
            if(System.Threading.Interlocked.CompareExchange(ref retailStatMutationStamp,unchecked(stamp+1),stamp)==stamp)return;
        }
    }
    private void EndRetailStatMutation(IStatOwner? statOwner,bool preparedCurseMutation=false)
    {
        try
        {
            var owners=System.Threading.Volatile.Read(ref retailStatOwners);
            if(preparedCurseMutation &&statOwner is Effect effect &&owners is not null &&owners.TryGetValue(effect,out var tracked))
                System.Threading.Interlocked.Increment(ref tracked.Completed);
        }
        finally { System.Threading.Interlocked.Add(ref retailStatMutationStamp, 0xffffffffL); }
    }
    internal RetailStatMutationOwner TrackRetailStatOwner(Effect effect)
    {
        var owners=System.Threading.Volatile.Read(ref retailStatOwners);
        if(owners is null)
        {
            var created=new System.Runtime.CompilerServices.ConditionalWeakTable<Effect,RetailStatMutationOwner>();
            owners=System.Threading.Interlocked.CompareExchange(ref retailStatOwners,created,null)??created;
        }
        return owners.GetValue(effect,static _=>new());
    }
    internal bool TryCaptureRetailStatMutation(out long stamp)
    {
        stamp=System.Threading.Volatile.Read(ref retailStatMutationStamp);
        return (stamp & 0xffffffffL)==0;
    }
    internal bool RetailStatMutationUnchanged(long stamp)
        => (stamp & 0xffffffffL)==0 &&System.Threading.Volatile.Read(ref retailStatMutationStamp)==stamp;
    // Call only for the short resource write, after max-stat calculation and
    // before callbacks. Never wait for existing writers while holding HP.
    internal bool TryReserveRetailStatCommit(long stamp)
        => (stamp & 0xffffffffL)==0 &&System.Threading.Interlocked.CompareExchange(
            ref retailStatMutationStamp,stamp | RetailStatReservation,stamp)==stamp;
    internal void ReleaseRetailStatCommit(long stamp)
    {
        if(System.Threading.Interlocked.CompareExchange(ref retailStatMutationStamp,stamp,stamp | RetailStatReservation)
            !=(stamp | RetailStatReservation))throw new InvalidOperationException("Retail stat reservation changed");
    }
    internal bool TryRetailExternalStatMutations(long stamp,RetailStatMutationOwner tracked,out uint count)
    {
        long owned=System.Threading.Volatile.Read(ref tracked.Completed);
        count=unchecked((uint)(stamp >> 32)-(uint)owned);
        return RetailStatMutationUnchanged(stamp);
    }

    private int attackCounter = 0;
    private int cachedSpeed;

    protected CreatureGameStats(Creature owner)
    {
        this.owner = owner;
    }

    public int GetAttackCounter()
    {
        return attackCounter;
    }

    protected void SetAttackCounter(int attackCounter)
    {
        if (attackCounter <= 0)
        {
            this.attackCounter = 1;
        }
        else
        {
            this.attackCounter = attackCounter;
        }
    }

    public void IncreaseAttackCounter()
    {
        if (attackCounter == ATTACK_MAX_COUNTER)
        {
            this.attackCounter = 1;
        }
        else
        {
            this.attackCounter++;
        }
    }

    // Java parity: List<? extends IStatFunction> -> C# covariant IEnumerable (body only iterates).
    public void AddEffectOnly(IStatOwner statOwner, IEnumerable<IStatFunction> functions)
        => AddEffectOnlyCore(statOwner,functions,false);

    private void AddEffectOnlyCore(IStatOwner statOwner,IEnumerable<IStatFunction> functions,bool preparedCurseMutation)
    {
        bool completed=false;
        BeginRetailStatMutation();
        try
        {
            foreach (IStatFunction function in functions)
            {
                IStatFunction functionToAdd = Equals(statOwner, function.GetOwner()) ? function : new StatFunctionProxy(statOwner, function);
                stats.AddOrUpdate(functionToAdd.GetName(),
                    k =>
                    {
                        List<IStatFunction> statFunctions = new List<IStatFunction>();
                        statFunctions.Add(functionToAdd);
                        return statFunctions;
                    },
                    (k, statFunctions) =>
                    {
                        lock (statFunctions)
                        {
                            statFunctions.Add(functionToAdd);
                            statFunctions.Sort();
                        }
                        return statFunctions;
                    });
            }
            completed=true;
        }
        finally { EndRetailStatMutation(statOwner,preparedCurseMutation &&completed); }
    }

    // Only the prepared curse owner may discount its exact HP/MP pair. Generic
    // additions attributed to the same effect still invalidate its witness.
    internal void AddPreparedRetailCurseEffectOnly(Effect effect,IReadOnlySet<IStatFunction> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if(!ReferenceEquals(effect.GetEffected(),owner)
            ||!Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatSpellDrain.HasPreparedCurse(effect)
            ||selected.Count!=2 ||selected.Any(f=>f.GetType()!=typeof(StatAddFunction) ||!f.IsBonus() ||f.HasConditions())
            ||selected.Count(f=>f.GetName()==StatEnum.MAXHP)!=1 ||selected.Count(f=>f.GetName()==StatEnum.MAXMP)!=1)
            throw new InvalidOperationException("Prepared retail curse stat pair differs");
        bool approved=false;
        var owners=System.Threading.Volatile.Read(ref retailStatOwners);
        if(owners is not null &&owners.TryGetValue(effect,out var tracked))
        {
            lock(tracked.Gate)
            {
                if(tracked.Expected is not null)throw new InvalidOperationException("Prepared retail curse stat pair already bound");
                tracked.Expected=new HashSet<IStatFunction>(selected,ReferenceEqualityComparer.Instance);
                approved=true;
            }
        }
        AddEffectOnlyCore(effect,selected,approved);
    }

    public void AddEffect(IStatOwner statOwner, IEnumerable<IStatFunction> functions)
    {
        AddEffectOnly(statOwner, functions);
        OnStatsChange(statOwner is Effect effect ? effect : null);
    }

    // A statistics-only NPC can retire into an owned/scaled context. Replace
    // only the selected numeric functions, retaining every other effect owner
    // and this effect's unrelated components, registration and expiry.
    internal void ReplaceOwnedFunctions(IStatOwner statOwner,IReadOnlySet<IStatFunction> previous,IEnumerable<IStatFunction> replacement)
    {
        BeginRetailStatMutation();
        try
        {
            foreach(var functions in stats.Values)
                lock(functions)functions.RemoveAll(f=>statOwner.Equals(f.GetOwner())
                    &&previous.Contains(f is Aion.GameServer.Model.Stats.Calc.Functions.StatFunctionProxy proxy?proxy.GetProxiedFunction():f));
            AddEffect(statOwner,replacement);
        }
        finally { EndRetailStatMutation(statOwner); }
    }

    public void EndEffect(IStatOwner statOwner)
    {
        bool statsChanged = RemoveEffectFunctions(statOwner);
        if (statsChanged && !owner.IsDead())
            OnStatsChange(null);
    }

    // The prepared retail curse owns a paired HP/MP update after removing its
    // own modifiers. Existing callers still use EndEffect and its normal update.
    internal bool EndPreparedRetailCurseEffectOnly(Effect effect,IReadOnlySet<IStatFunction> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if(!ReferenceEquals(effect.GetEffected(),owner)
            ||!Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatSpellDrain.HasPreparedCurse(effect))
            throw new InvalidOperationException("Prepared retail curse stat owner differs");
        bool approved=false,completed=false;
        var owners=System.Threading.Volatile.Read(ref retailStatOwners);
        if(owners is not null &&owners.TryGetValue(effect,out var tracked))
            lock(tracked.Gate)approved=tracked.Expected is { } expected &&expected.SetEquals(selected);
        BeginRetailStatMutation();
        try
        {
            bool changed=false;
            foreach(var functions in stats.Values)
                lock(functions)changed|=functions.RemoveAll(f=>effect.Equals(f.GetOwner())
                    &&selected.Contains(f is StatFunctionProxy proxy?proxy.GetProxiedFunction():f))>0;
            completed=true;return changed;
        }
        finally { EndRetailStatMutation(effect,approved &&completed); }
    }

    private bool RemoveEffectFunctions(IStatOwner statOwner)
    {
        BeginRetailStatMutation();
        try
        {
            bool statsChanged = false;
            foreach (List<IStatFunction> functions in stats.Values)
            {
                lock (functions)
                {
                    statsChanged |= functions.RemoveAll(statFunction => statOwner.Equals(statFunction.GetOwner())) > 0;
                }
            }
            return statsChanged;
        }
        finally { EndRetailStatMutation(statOwner); }
    }

    public float GetPositiveStat(StatEnum statEnum, float baseValue)
    {
        Stat2 stat = GetStat(statEnum, baseValue);
        return Math.Max(0, stat.GetCurrent());
    }

    public int GetPositiveReverseStat(StatEnum statEnum, int baseValue)
    {
        Stat2 stat = GetReverseStat(statEnum, baseValue);
        return Math.Max(0, stat.GetCurrent());
    }

    // Java parity: public final getStat(StatEnum, float)
    public Stat2 GetStat(StatEnum statEnum, float baseValue)
    {
        return GetStat(statEnum, baseValue, NoCalculationTypes);
    }

    protected virtual Stat2 GetStat(StatEnum statEnum, float baseValue, ISet<CalculationType> calculationTypes)
    {
        Stat2 stat = new AdditionStat(statEnum, baseValue, owner);
        return ApplyStatFunctions(statEnum, stat, calculationTypes);
    }

    public Stat2 GetReverseStat(StatEnum statEnum, float baseValue)
    {
        Stat2 stat = new ReverseStat(statEnum, baseValue, owner);
        return ApplyStatFunctions(statEnum, stat, NoCalculationTypes);
    }

    public virtual Stat2 ApplyStatFunctions(StatEnum statEnum, Stat2 stat, ISet<CalculationType> calculationTypes)
    {
        foreach (IStatFunction func in GetStatsSorted(statEnum))
        {
            if (func.Validate(stat))
            {
                if ((statEnum == StatEnum.PHYSICAL_ATTACK || statEnum == StatEnum.MAGICAL_ATTACK) && func.GetOwner() is Aion.GameServer.Model.Enchants.EnchantEffect ef)
                {
                    if (ef.GetItemSlot() == Aion.GameServer.Model.Items.ItemSlot.MAIN_HAND && calculationTypes.Contains(CalculationType.MAIN_HAND)
                        || ef.GetItemSlot() == Aion.GameServer.Model.Items.ItemSlot.SUB_HAND && calculationTypes.Contains(CalculationType.OFF_HAND))
                    {
                        func.Apply(stat, calculationTypes);
                    }
                }
                else
                {
                    func.Apply(stat, calculationTypes);
                }
            }
        }
        StatCapUtil.CalculateBaseValue(stat, owner);
        return stat;
    }

    public Stat2 GetItemStatBoost(StatEnum statEnum, Stat2 stat)
    {
        foreach (IStatFunction func in GetStatsSorted(statEnum))
        {
            if (func.IsBonus() && func.Validate(stat) && (func.GetOwner() is Item || func.GetOwner() is Aion.GameServer.Model.Items.ManaStone
                || func.GetOwner() is Aion.GameServer.Model.Templates.Itemset.ItemSetTemplate || func.GetOwner() is Aion.GameServer.Model.Items.RandomBonusEffect))
            {
                func.Apply(stat, NoCalculationTypes);
            }
        }
        return stat;
    }

    public abstract StatsTemplate GetStatsTemplate();

    public Stat2 GetPower()
    {
        return GetStat(StatEnum.POWER, GetStatsTemplate().GetPower());
    }

    public Stat2 GetHealth()
    {
        return GetStat(StatEnum.HEALTH, GetStatsTemplate().GetHealth());
    }

    public Stat2 GetAccuracy()
    {
        return GetStat(StatEnum.ACCURACY, GetStatsTemplate().GetBaseAccuracy());
    }

    public Stat2 GetAgility()
    {
        return GetStat(StatEnum.AGILITY, GetStatsTemplate().GetAgility());
    }

    public Stat2 GetKnowledge()
    {
        return GetStat(StatEnum.KNOWLEDGE, GetStatsTemplate().GetKnowledge());
    }

    public Stat2 GetWill()
    {
        return GetStat(StatEnum.WILL, GetStatsTemplate().GetWill());
    }

    public Stat2 GetMaxHp()
    {
        return GetStat(StatEnum.MAXHP, GetStatsTemplate().GetMaxHp());
    }

    public Stat2 GetMaxMp()
    {
        return GetStat(StatEnum.MAXMP, GetStatsTemplate().GetMaxMp());
    }

    public Stat2 GetPDef()
    {
        return GetStat(StatEnum.PHYSICAL_DEFENSE, GetStatsTemplate().GetPdef());
    }

    public Stat2 GetMDef()
    {
        return GetStat(StatEnum.MAGICAL_DEFEND, GetStatsTemplate().GetMdef());
    }

    public Stat2 GetEvasion()
    {
        return GetStat(StatEnum.EVASION, GetStatsTemplate().GetEvasion());
    }

    public virtual Stat2 GetParry()
    {
        return GetStat(StatEnum.PARRY, GetStatsTemplate().GetParry());
    }

    public Stat2 GetBlock()
    {
        return GetStat(StatEnum.BLOCK, GetStatsTemplate().GetBlock());
    }

    public Stat2 GetMResist()
    {
        return GetStat(StatEnum.MAGICAL_RESIST, GetStatsTemplate().GetMresist());
    }

    public Stat2 GetPCR()
    {
        return GetStat(StatEnum.PHYSICAL_CRITICAL_RESIST, GetStatsTemplate().GetStrikeResist());
    }

    public Stat2 GetMCR()
    {
        return GetStat(StatEnum.MAGICAL_CRITICAL_RESIST, GetStatsTemplate().GetSpellResist());
    }

    // Java parity: public final getMainHandPAttack(CalculationType...)
    public Stat2 GetMainHandPAttack(params CalculationType[] calculationTypes)
    {
        return GetMainHandPAttack(ToSet(calculationTypes));
    }

    public virtual Stat2 GetMainHandPAttack(ISet<CalculationType> calculationTypes)
    {
        return GetStat(StatEnum.PHYSICAL_ATTACK, GetStatsTemplate().GetAttack(), calculationTypes);
    }

    public virtual Stat2 GetMainHandPCritical()
    {
        return GetStat(StatEnum.PHYSICAL_CRITICAL, GetStatsTemplate().GetPcrit());
    }

    public virtual Stat2 GetMainHandPAccuracy()
    {
        return GetStat(StatEnum.PHYSICAL_ACCURACY, GetStatsTemplate().GetAccuracy());
    }

    // Java parity: public final getMainHandMAttack(CalculationType...)
    public Stat2 GetMainHandMAttack(params CalculationType[] calculationTypes)
    {
        return GetMainHandMAttack(ToSet(calculationTypes));
    }

    public virtual Stat2 GetMainHandMAttack(ISet<CalculationType> calculationTypes)
    {
        return GetStat(StatEnum.MAGICAL_ATTACK, GetStatsTemplate().GetMagicalAttack(), calculationTypes);
    }

    public virtual Stat2 GetMCritical()
    {
        return GetStat(StatEnum.MAGICAL_CRITICAL, GetStatsTemplate().GetMcrit());
    }

    public virtual Stat2 GetMAccuracy()
    {
        return GetStat(StatEnum.MAGICAL_ACCURACY, GetStatsTemplate().GetMacc());
    }

    public virtual Stat2 GetMBoost()
    {
        return GetStat(StatEnum.BOOST_MAGICAL_SKILL, GetStatsTemplate().GetMagicBoost());
    }

    public Stat2 GetMBResist()
    {
        return GetStat(StatEnum.MAGIC_SKILL_BOOST_RESIST, GetStatsTemplate().GetMsup());
    }

    public Stat2 GetAbnormalResistance()
    {
        return GetStat(StatEnum.ABNORMAL_RESISTANCE_ALL, GetStatsTemplate().GetAbnormalResistance());
    }

    public Stat2 GetResistance(StatEnum statEnum)
    {
        int baseValue = statEnum switch
        {
            StatEnum.OPENAERIAL_RESISTANCE or StatEnum.PARALYZE_RESISTANCE or StatEnum.SPIN_RESISTANCE
                or StatEnum.STAGGER_RESISTANCE or StatEnum.STUMBLE_RESISTANCE or StatEnum.STUN_RESISTANCE => GetStatsTemplate().GetStunLikeResistance(),
            _ => 0
        };
        return GetStat(statEnum, baseValue);
    }

    public virtual Stat2 GetAttackSpeed()
    {
        return GetStat(StatEnum.ATTACK_SPEED, GetBaseAttackSpeed());
    }

    public abstract int GetBaseAttackSpeed();

    public float GetAttackSpeedRate()
    {
        return (float)GetAttackSpeed().GetCurrent() / GetBaseAttackSpeed();
    }

    public abstract Stat2 GetMovementSpeed();

    public abstract Stat2 GetAttackRange();

    public abstract Stat2 GetHpRegenRate();

    public abstract Stat2 GetMpRegenRate();

    public int GetElementalDefenseFor(SkillElement element)
    {
        switch (element)
        {
            case SkillElement.EARTH:
                return GetStat(StatEnum.EARTH_RESISTANCE, 0).GetCurrent();
            case SkillElement.FIRE:
                return GetStat(StatEnum.FIRE_RESISTANCE, 0).GetCurrent();
            case SkillElement.WATER:
                return GetStat(StatEnum.WATER_RESISTANCE, 0).GetCurrent();
            case SkillElement.WIND:
                return GetStat(StatEnum.WIND_RESISTANCE, 0).GetCurrent();
            case SkillElement.LIGHT:
                return GetStat(StatEnum.ELEMENTAL_RESISTANCE_LIGHT, 0).GetCurrent();
            case SkillElement.DARK:
                return GetStat(StatEnum.ELEMENTAL_RESISTANCE_DARK, 0).GetCurrent();
            default:
                return 0;
        }
    }

    public float GetMovementSpeedFloat()
    {
        return GetMovementSpeed().GetCurrent() / 1000f;
    }

    public void UpdateArmorMasteryStats(List<Item> equipment)
    {
        foreach (List<IStatFunction> statFunctions in stats.Values)
        {
            lock (statFunctions)
            {
                foreach (IStatFunction sf in statFunctions)
                {
                    IStatFunction statFunction = sf;
                    if (statFunction is StatFunctionProxy proxy)
                        statFunction = proxy.GetProxiedFunction();
                    if (statFunction is Aion.GameServer.Model.Stats.Calc.Functions.StatArmorMasteryFunction armorMasteryFunction)
                        armorMasteryFunction.UpdateEquipmentFactor(equipment);
                }
            }
        }
    }

    /// <summary>Send packet about stats info.</summary>
    public virtual void UpdateStatInfo()
    {
    }

    /// <summary>Send packet about speed info.</summary>
    public virtual void UpdateSpeedInfo()
    {
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.CHANGE_SPEED));
    }

    protected virtual bool CheckSpeedStats()
    {
        int currentSpeed = GetMovementSpeed().GetCurrent();
        if (currentSpeed != cachedSpeed)
        {
            UpdateSpeedInfo();
            cachedSpeed = currentSpeed;
            return true;
        }
        return false;
    }

    /// <summary>All stat functions for the given stat sorted by priority.</summary>
    public List<IStatFunction> GetStatsSorted(StatEnum stat)
    {
        if (!stats.TryGetValue(stat, out List<IStatFunction> statFunctions) || statFunctions == null)
            return new List<IStatFunction>();
        lock (statFunctions)
        {
            return new List<IStatFunction>(statFunctions);
        }
    }

    /// <summary>
    /// Perform additional calculations after effects added/removed. Called outside of stats lock.
    /// </summary>
    protected virtual void OnStatsChange(Effect effect)
    {
        owner.GetLifeStats().OnStatsChange(effect);
    }

    // Java parity: Collections.emptySet() (immutable, shared)
    private static readonly ISet<CalculationType> NoCalculationTypes = System.Collections.Immutable.ImmutableHashSet<CalculationType>.Empty;

    protected static ISet<CalculationType> ToSet(CalculationType[] calculationTypes)
    {
        return calculationTypes.Length == 0 ? NoCalculationTypes : new HashSet<CalculationType>(calculationTypes);
    }

    /// <summary>Java parity: copyWith(Set, CalculationType) = EnumSet.of(type) plus all given types.</summary>
    protected static HashSet<CalculationType> CopyWith(ISet<CalculationType> types, CalculationType type)
    {
        HashSet<CalculationType> calculationTypes = new HashSet<CalculationType> { type };
        calculationTypes.UnionWith(types);
        return calculationTypes;
    }
}

/// <summary>
/// Java parity: generic typing of <see cref="CreatureGameStats"/> (Java <c>CreatureGameStats&lt;T extends Creature&gt;</c>).
/// Split into a non-generic base + this generic shim so that <c>Creature.GetGameStats()</c> can return the non-generic
/// base (C# has no wildcard generics for Java's <c>CreatureGameStats&lt;? extends Creature&gt;</c>). Subclasses extend
/// <c>CreatureGameStats&lt;Npc&gt;</c> etc. and see <c>owner</c> typed as T via the shadowing property.
/// </summary>
public abstract class CreatureGameStats<T> : CreatureGameStats where T : Creature
{
    protected CreatureGameStats(T owner) : base(owner)
    {
    }

    protected new T owner => (T)base.owner;
}
