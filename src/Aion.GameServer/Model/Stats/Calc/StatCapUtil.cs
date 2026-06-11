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
    private static readonly Dictionary<StatEnum, StatLimits> limits = new Dictionary<StatEnum, StatLimits>();

    static StatCapUtil()
    {
        foreach (StatEnum stat in Enum.GetValues<StatEnum>())
        {
            limits[stat] = new StatLimits(stat);
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
        if (IsElementalDefenseStat(stat))
        {
            return -GetElementalDefenseCapForCreature(creature);
        }
        return limits[stat].LowerCap;
    }

    public static int GetUpperCap(StatEnum stat, Creature creature)
    {
        bool isSpeedUnrestricted = !(creature is Player player) || player.IsStaff();
        if ((stat == StatEnum.SPEED || stat == StatEnum.FLY_SPEED) && isSpeedUnrestricted)
            return int.MaxValue;
        if (IsElementalDefenseStat(stat))
        {
            return GetElementalDefenseCapForCreature(creature);
        }
        return limits[stat].UpperCap;
    }

    public static int GetElementalDefenseCapForCreature(Creature creature)
    {
        if (creature is Player)
        {
            return 1000 + Math.Max(0, creature.GetLevel() - 50) * 10;
        }
        return GetElementalDefenseBaseValue();
    }

    private static bool IsElementalDefenseStat(StatEnum stat)
    {
        return stat switch
        {
            StatEnum.WATER_RESISTANCE or StatEnum.FIRE_RESISTANCE or StatEnum.EARTH_RESISTANCE
                or StatEnum.WIND_RESISTANCE or StatEnum.DARK_RESISTANCE or StatEnum.LIGHT_RESISTANCE => true,
            _ => false
        };
    }

    public static int GetDifferenceLimit(StatEnum stat)
    {
        return limits[stat].DiffLimit;
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

    private sealed record Cap(int Min, int Max);

    private sealed class StatLimits
    {
        public readonly int LowerCap;
        public readonly int UpperCap;
        public readonly int DiffLimit;

        public StatLimits(StatEnum stat)
        {
            this.LowerCap = LowerCapFor(stat);
            this.UpperCap = UpperCapFor(stat);
            this.DiffLimit = DifferenceLimitFor(stat);
        }

        private static int LowerCapFor(StatEnum stat)
        {
            int value = int.MinValue;
            switch (stat)
            {
                case StatEnum.MAIN_HAND_POWER:
                case StatEnum.MAIN_HAND_ACCURACY:
                case StatEnum.MAIN_HAND_CRITICAL:
                case StatEnum.OFF_HAND_POWER:
                case StatEnum.OFF_HAND_ACCURACY:
                case StatEnum.OFF_HAND_CRITICAL:
                case StatEnum.MAGICAL_RESIST:
                case StatEnum.PHYSICAL_CRITICAL_RESIST:
                case StatEnum.EVASION:
                case StatEnum.PHYSICAL_DEFENSE:
                case StatEnum.PHYSICAL_ACCURACY:
                case StatEnum.MAGICAL_ACCURACY:
                case StatEnum.SPEED:
                case StatEnum.FLY_SPEED:
                case StatEnum.MAXHP:
                case StatEnum.MAXMP:
                    value = 0;
                    break;
            }
            return value;
        }

        private static int UpperCapFor(StatEnum stat)
        {
            int value = int.MaxValue;
            switch (stat)
            {
                case StatEnum.SPEED:
                    value = 12000;
                    break;
                case StatEnum.FLY_SPEED:
                    value = 16000;
                    break;
                case StatEnum.HEAL_BOOST:
                    value = 1000;
                    break;
            }
            return value;
        }

        private static int DifferenceLimitFor(StatEnum stat)
        {
            switch (stat)
            {
                case StatEnum.BLOCK:
                case StatEnum.PHYSICAL_CRITICAL:
                case StatEnum.MAGICAL_CRITICAL:
                    return 500;
                case StatEnum.MAGICAL_RESIST:
                    return 900; // in PvP: 500 (see StatFunctions#calculateMagicalResistRate)
                case StatEnum.EVASION:
                    return 300;
                case StatEnum.PARRY:
                    return 400;
                case StatEnum.BOOST_MAGICAL_SKILL:
                    return 2900;
            }
            return int.MaxValue;
        }
    }
}
