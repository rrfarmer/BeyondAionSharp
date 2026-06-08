using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.SkillEngine.Model;
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
public abstract class CreatureGameStats<T> where T : Creature
{
    private const int ATTACK_MAX_COUNTER = int.MaxValue;

    protected readonly T owner;
    private readonly ConcurrentDictionary<StatEnum, List<IStatFunction>> stats = new ConcurrentDictionary<StatEnum, List<IStatFunction>>();

    private int attackCounter = 0;
    private int cachedMaxHp, cachedMaxMp, cachedSpeed;

    protected CreatureGameStats(T owner)
    {
        this.owner = owner;
    }

    // Java parity helper: Math.round(float) = floor(x+0.5) (C# Math.Round is banker's rounding).
    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);

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

    public void AddEffectOnly(IStatOwner statOwner, List<IStatFunction> functions)
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
    }

    public void AddEffect(IStatOwner statOwner, List<IStatFunction> functions)
    {
        AddEffectOnly(statOwner, functions);
        OnStatsChange(statOwner is Effect effect ? effect : null);
    }

    public void EndEffect(IStatOwner statOwner)
    {
        bool statsChanged = false;
        foreach (List<IStatFunction> functions in stats.Values)
        {
            lock (functions)
            {
                statsChanged |= functions.RemoveAll(statFunction => statOwner.Equals(statFunction.GetOwner())) > 0;
            }
        }
        if (statsChanged && !owner.IsDead())
            OnStatsChange(null);
    }

    public float GetPositiveStat(StatEnum statEnum, float baseValue)
    {
        Stat2 stat = GetStat(statEnum, baseValue);
        float value = stat.GetCurrent();
        return value > 0 ? value : 0;
    }

    public int GetPositiveReverseStat(StatEnum statEnum, int baseValue)
    {
        Stat2 stat = GetReverseStat(statEnum, baseValue);
        int value = stat.GetCurrent();
        return value > 0 ? value : 0;
    }

    public virtual Stat2 GetStat(StatEnum statEnum, float baseValue, params CalculationType[] calculationTypes)
    {
        Stat2 stat = new AdditionStat(statEnum, baseValue, owner);
        return GetStat(statEnum, stat, calculationTypes);
    }

    public Stat2 GetStat(StatEnum statEnum, float baseValue, float bonusRate, params CalculationType[] calculationTypes)
    {
        Stat2 stat = new AdditionStat(statEnum, baseValue, owner, bonusRate);
        return GetStat(statEnum, stat, calculationTypes);
    }

    public Stat2 GetReverseStat(StatEnum statEnum, float baseValue)
    {
        Stat2 stat = new ReverseStat(statEnum, baseValue, owner);
        return GetStat(statEnum, stat);
    }

    public Stat2 GetReverseStat(StatEnum statEnum, float baseValue, float bonusRate)
    {
        Stat2 stat = new ReverseStat(statEnum, baseValue, owner, bonusRate);
        return GetStat(statEnum, stat);
    }

    public virtual Stat2 GetStat(StatEnum statEnum, Stat2 stat, params CalculationType[] calculationTypes)
    {
        List<IStatFunction> functions = GetStatsSorted(statEnum);
        if (functions != null)
        {
            foreach (IStatFunction func in functions)
            {
                if (func.Validate(stat))
                {
                    if ((statEnum == StatEnum.PHYSICAL_ATTACK || statEnum == StatEnum.MAGICAL_ATTACK) && func.GetOwner() is Aion.GameServer.Model.Enchants.EnchantEffect ef)
                    {
                        if (ef.GetItemSlot() == Aion.GameServer.Model.Items.ItemSlot.MAIN_HAND && Array.IndexOf(calculationTypes, CalculationType.MAIN_HAND) >= 0
                            || ef.GetItemSlot() == Aion.GameServer.Model.Items.ItemSlot.SUB_HAND && Array.IndexOf(calculationTypes, CalculationType.OFF_HAND) >= 0)
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
        }
        return stat;
    }

    public Stat2 GetItemStatBoost(StatEnum statEnum, Stat2 stat)
    {
        List<IStatFunction> functions = GetStatsSorted(statEnum);
        if (functions != null)
        {
            foreach (IStatFunction func in functions)
            {
                if (func.IsBonus() && func.Validate(stat) && (func.GetOwner() is Item || func.GetOwner() is Aion.GameServer.Model.Items.ManaStone
                    || func.GetOwner() is Aion.GameServer.Model.Templates.ItemSet.ItemSetTemplate || func.GetOwner() is Aion.GameServer.Model.Items.RandomBonusEffect))
                {
                    func.Apply(stat);
                }
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

    public Stat2 GetParry()
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

    public Stat2 GetMainHandPAttack(params CalculationType[] calculationTypes)
    {
        return GetStat(StatEnum.PHYSICAL_ATTACK, GetStatsTemplate().GetAttack(), calculationTypes);
    }

    public Stat2 GetMainHandPCritical()
    {
        return GetStat(StatEnum.PHYSICAL_CRITICAL, GetStatsTemplate().GetPcrit());
    }

    public Stat2 GetMainHandPAccuracy()
    {
        return GetStat(StatEnum.PHYSICAL_ACCURACY, GetStatsTemplate().GetAccuracy());
    }

    public virtual Stat2 GetMainHandMAttack(params CalculationType[] calculationTypes)
    {
        return GetStat(StatEnum.MAGICAL_ATTACK, GetStatsTemplate().GetMagicalAttack(), calculationTypes);
    }

    public Stat2 GetMCritical()
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

    public abstract Stat2 GetAttackSpeed();

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

    /// <summary>Send packet about stats info.</summary>
    public virtual void UpdateStatInfo()
    {
    }

    /// <summary>Send packet about speed info.</summary>
    public virtual void UpdateSpeedInfo()
    {
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.ChangeSpeed));
    }

    protected bool CheckSpeedStats()
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
            return null;
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
        CheckMaxHPChanged(effect);
        CheckMaxMPChanged(effect);
    }

    private void CheckMaxHPChanged(Effect effect)
    {
        lock (this)
        {
            int oldMaxHp = cachedMaxHp != 0 ? cachedMaxHp : GetMaxHp().GetBase();
            int currentMaxHp = cachedMaxHp = GetMaxHp().GetCurrent();
            if (oldMaxHp != currentMaxHp)
            {
                float percent = 1f * currentMaxHp / oldMaxHp;
                int newHp = Math.Min(JRound(owner.GetLifeStats().GetCurrentHp() * percent), currentMaxHp);
                Creature effector = effect == null ? owner : effect.GetEffector();
                owner.GetLifeStats().SetCurrentHp(newHp, effector);
            }
        }
    }

    private void CheckMaxMPChanged(Effect effect)
    {
        lock (this)
        {
            int oldMaxMp = cachedMaxMp != 0 ? cachedMaxMp : GetMaxMp().GetBase();
            int currentMaxMp = cachedMaxMp = GetMaxMp().GetCurrent();
            if (oldMaxMp != currentMaxMp)
            {
                float percent = 1f * currentMaxMp / oldMaxMp;
                owner.GetLifeStats().SetCurrentMp(Math.Min(JRound(owner.GetLifeStats().GetCurrentMp() * percent), currentMaxMp));
            }
        }
    }
}
