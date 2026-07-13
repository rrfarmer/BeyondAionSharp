using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Stats.Calc;

/// <summary>
/// Java parity: model/stats/calc/StatCapUtil. Stat lower/upper/difference caps and PvP/PvE ratio limits.
/// </summary>
public class StatCapUtil
{
    private static readonly Dictionary<StatEnum, StatCapRule> limits = new();

    static StatCapUtil()
    {
        RegisterDefaults();
    }

    private static void RegisterDefaults()
    {
        Register(StatEnum.MAXHP, creature => creature is Player ? 100 : 1, UnlimitedUpper);
        Register(StatEnum.MAXMP, creature => creature is Player ? 1 : 0, UnlimitedUpper);
        Register(StatEnum.SPEED, 0, creature => creature is Player player && !player.IsStaff() ? 12000 : int.MaxValue);
        Register(StatEnum.FLY_SPEED, 0, creature => creature is Player player && !player.IsStaff() ? 16000 : int.MaxValue);
        Register(StatEnum.HEAL_BOOST, -1000, 1000);
        Register(StatEnum.EVASION, 0, UnlimitedUpper, 300);
        Register(StatEnum.PARRY, 0, UnlimitedUpper, 400);
        Register(StatEnum.BLOCK, 0, UnlimitedUpper, 500);
        Register(StatEnum.PHYSICAL_CRITICAL, 0, UnlimitedUpper, 500);
        Register(StatEnum.MAGICAL_CRITICAL, 0, UnlimitedUpper, 500);
        Register(StatEnum.MAGICAL_RESIST, 0, UnlimitedUpper, 900);
        Register(StatEnum.BOOST_MAGICAL_SKILL, 0, UnlimitedUpper, 2900);

        foreach (StatEnum stat in new[]
                 {
                     StatEnum.PHYSICAL_CRITICAL_RESIST,
                     StatEnum.MAGICAL_CRITICAL_RESIST,
                     StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE,
                     StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE
                 })
        {
            Register(stat, 0, 700);
        }

        foreach (StatEnum stat in new[]
                 {
                     StatEnum.POWER, StatEnum.AGILITY, StatEnum.ACCURACY,
                     StatEnum.HEALTH, StatEnum.KNOWLEDGE, StatEnum.WILL
                 })
        {
            Register(stat, 80, 999);
        }

        foreach (StatEnum stat in new[]
                 {
                     StatEnum.MAIN_HAND_POWER, StatEnum.MAIN_HAND_ACCURACY, StatEnum.MAIN_HAND_CRITICAL,
                     StatEnum.OFF_HAND_POWER, StatEnum.OFF_HAND_ACCURACY, StatEnum.OFF_HAND_CRITICAL,
                     StatEnum.PHYSICAL_DEFENSE, StatEnum.PHYSICAL_ACCURACY, StatEnum.MAGICAL_ACCURACY
                 })
        {
            Register(stat, 0, UnlimitedUpper);
        }

        foreach (StatEnum stat in new[]
                 {
                     StatEnum.WATER_RESISTANCE, StatEnum.FIRE_RESISTANCE, StatEnum.EARTH_RESISTANCE,
                     StatEnum.WIND_RESISTANCE, StatEnum.DARK_RESISTANCE, StatEnum.LIGHT_RESISTANCE
                 })
        {
            Register(stat, creature => -GetElementalDefenseCapForCreature(creature), GetElementalDefenseCapForCreature);
        }
    }

    public static int GetElementalDefenseBaseValue()
    {
        return 1300;
    }

    public static void CalculateBaseValue(Stat2 stat, Creature creature)
    {
        int lowerCap = GetLowerCap(stat.GetStat(), creature);
        int upperCap = GetUpperCap(stat.GetStat(), creature);

        if (stat.GetStat() == StatEnum.ATTACK_SPEED)
        {
            int @base = stat.GetBase() / 2;
            if (stat.GetBonus() > 0 && @base < stat.GetBonus())
                stat.SetBonus(@base);
            else if (stat.GetBonus() < 0 && @base < -stat.GetBonus())
                stat.SetBonus(-@base);
        }

        Calculate(stat, lowerCap, upperCap);
    }

    public static int GetLowerCap(StatEnum stat, Creature creature)
    {
        return GetRule(stat).LowerCap(creature);
    }

    public static int GetUpperCap(StatEnum stat, Creature creature)
    {
        return GetRule(stat).UpperCap(creature);
    }

    public static int GetElementalDefenseCapForCreature(Creature creature)
    {
        if (creature is Player)
        {
            return 1000 + Math.Max(0, creature.GetLevel() - 50) * 10;
        }
        return GetElementalDefenseBaseValue();
    }

    public static int GetDifferenceLimit(StatEnum stat)
    {
        return GetRule(stat).DiffLimit;
    }

    private static void Calculate(Stat2 stat2, int lowerCap, int upperCap)
    {
        if (stat2.GetCurrent() > upperCap)
        {
            stat2.SetBonus(upperCap - stat2.GetBase());
        }
        else if (stat2.GetCurrent() < lowerCap)
        {
            stat2.SetBonus(lowerCap - stat2.GetBase());
        }
    }

    public static int ClampStatValue(StatEnum stat, Creature creature, int value)
    {
        int lower = GetLowerCap(stat, creature);
        int upper = GetUpperCap(stat, creature);
        return Math.Clamp(value, lower, upper);
    }

    public static int LimitValueForPvpOrPveStat(CombatMode mode, RatioType type, int value)
    {
        // Note: PvP/PvE ratio caps are symmetric:
        // - attack min is fixed, defense max is fixed
        // - upper/lower bounds depend on combat mode
        Cap cap = mode switch
        {
            CombatMode.PVP => type switch
            {
                RatioType.ATTACK => new Cap(-900, 1000),
                RatioType.DEFENSE => new Cap(-1000, 900),
                _ => new Cap(0, 0)
            },
            CombatMode.PVE => type switch
            {
                RatioType.ATTACK => new Cap(-900, 5000),
                RatioType.DEFENSE => new Cap(-5000, 900),
                _ => new Cap(0, 0)
            },
            _ => new Cap(0, 0)
        };

        return Math.Clamp(value, cap.Min, cap.Max);
    }

    private static void Register(StatEnum stat, int lowerCap, int upperCap)
    {
        Register(stat, _ => lowerCap, _ => upperCap, int.MaxValue);
    }

    private static void Register(StatEnum stat, CapFunction lowerCap, CapFunction upperCap)
    {
        Register(stat, lowerCap, upperCap, int.MaxValue);
    }

    private static void Register(StatEnum stat, int lowerCap, CapFunction upperCap)
    {
        Register(stat, _ => lowerCap, upperCap, int.MaxValue);
    }

    private static void Register(StatEnum stat, int lowerCap, CapFunction upperCap, int diffLimit)
    {
        Register(stat, _ => lowerCap, upperCap, diffLimit);
    }

    private static void Register(StatEnum stat, CapFunction lowerCap, CapFunction upperCap, int diffLimit)
    {
        if (!limits.TryAdd(stat, new StatCapRule(lowerCap, upperCap, diffLimit)))
            throw new ArgumentException($"A limit for {stat} is already registered", nameof(stat));
    }

    private static StatCapRule GetRule(StatEnum stat)
    {
        return limits.GetValueOrDefault(stat, StatCapRule.Unlimited);
    }

    private static int UnlimitedLower(Creature creature) => int.MinValue;

    private static int UnlimitedUpper(Creature creature) => int.MaxValue;

    private sealed record Cap(int Min, int Max);

    private delegate int CapFunction(Creature creature);

    private sealed record StatCapRule(CapFunction LowerCap, CapFunction UpperCap, int DiffLimit)
    {
        public static readonly StatCapRule Unlimited = new(UnlimitedLower, UnlimitedUpper, int.MaxValue);
    }
}
