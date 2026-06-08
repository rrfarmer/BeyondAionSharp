using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Model.Stats.Calc;

/// <summary>
/// Computes an NPC's base stat from level, rating, and rank.
/// Java parity: model/stats/calc/NpcStatCalculation.
/// </summary>
public static class NpcStatCalculation
{
    // Java parity: calculateStat(StatEnum, NpcRating, NpcRank, byte)
    public static int CalculateStat(StatEnum stat, NpcRating rating, NpcRank rank, byte level)
    {
        float baseValue = GetBaseValue(stat, level);
        float ratingMod = GetRatingModifier(stat, rating);
        float rankMod = GetRankModifier(stat, rank);
        // Java Math.round(float) == (int)floor(f + 0.5f)
        return (int)Math.Floor(baseValue * ratingMod * rankMod + 0.5f);
    }

    private static float GetBaseValue(StatEnum stat, byte level) => stat switch
    {
        // https://www.wolframalpha.com/input/?i=-0.0007x%5E3+%2B+0.1x%5E2+%2B+5.3x
        StatEnum.PHYSICAL_ATTACK => -0.0007f * (float)Math.Pow(level, 3) + 0.1f * (float)Math.Pow(level, 2) + 5.3f * level,
        StatEnum.MAGICAL_DEFEND => level * 5f,
        StatEnum.MAGICAL_ATTACK => level * 20f,
        StatEnum.PHYSICAL_DEFENSE => level * 17f,
        StatEnum.MAGICAL_ACCURACY => level * 25f,
        // fit (1,20),(15,270),(30,585),(50,1075),(60,1350),(65,1495)
        StatEnum.MAGICAL_RESIST => 0.1f * (float)Math.Pow(level, 2) + 16.5f * level,
        StatEnum.PHYSICAL_ACCURACY => level * 37f,
        StatEnum.PARRY => level * 40f,
        StatEnum.PHYSICAL_CRITICAL_RESIST => (level - 50) * 2.5f,
        StatEnum.MAGICAL_CRITICAL_RESIST => (level - 50) * 1.1f,
        StatEnum.STUNLIKE_RESISTANCE => 100f,
        _ => throw new ArgumentException("Stat calculation for " + stat + " is not implemented"),
    };

    private static float GetRatingModifier(StatEnum stat, NpcRating rating) => rating switch
    {
        NpcRating.Junk or NpcRating.Normal => stat switch
        {
            StatEnum.MAGICAL_ATTACK => 0.4f,
            StatEnum.STUNLIKE_RESISTANCE => 0f,
            _ => 1f,
        },
        NpcRating.Elite => stat switch
        {
            StatEnum.PHYSICAL_ATTACK => 1.7f,
            StatEnum.MAGICAL_ATTACK => 0.5f,
            StatEnum.MAGICAL_DEFEND or StatEnum.PHYSICAL_DEFENSE => 1.25f,
            StatEnum.MAGICAL_RESIST => 1.05f,
            StatEnum.PHYSICAL_ACCURACY or StatEnum.MAGICAL_ACCURACY => 1.03f,
            StatEnum.PARRY => 1.025f,
            StatEnum.PHYSICAL_CRITICAL_RESIST => 9f,
            StatEnum.MAGICAL_CRITICAL_RESIST => 8.5f,
            StatEnum.STUNLIKE_RESISTANCE => 5f,
            _ => 1f,
        },
        NpcRating.Hero => stat switch
        {
            StatEnum.PHYSICAL_ATTACK => 2.4f,
            StatEnum.MAGICAL_ATTACK => 0.6f,
            StatEnum.PHYSICAL_ACCURACY or StatEnum.MAGICAL_ACCURACY => 1.075f,
            StatEnum.MAGICAL_RESIST => 1.2f,
            StatEnum.MAGICAL_DEFEND or StatEnum.PHYSICAL_DEFENSE => 1.5f,
            StatEnum.PARRY => 1.07f,
            StatEnum.PHYSICAL_CRITICAL_RESIST or StatEnum.MAGICAL_CRITICAL_RESIST => 13.5f,
            StatEnum.STUNLIKE_RESISTANCE => 20f,
            _ => 1f,
        },
        NpcRating.Legendary => stat switch
        {
            StatEnum.PHYSICAL_ATTACK => 2.6f,
            StatEnum.PHYSICAL_DEFENSE or StatEnum.MAGICAL_DEFEND => 1.75f,
            StatEnum.MAGICAL_RESIST => 1.35f,
            StatEnum.MAGICAL_ACCURACY => 1.47f,
            StatEnum.MAGICAL_ATTACK or StatEnum.PARRY or StatEnum.PHYSICAL_ACCURACY => 1.1f,
            StatEnum.PHYSICAL_CRITICAL_RESIST or StatEnum.MAGICAL_CRITICAL_RESIST => 13.5f,
            StatEnum.STUNLIKE_RESISTANCE => 20f,
            _ => 1f,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(rating)),
    };

    private static float GetRankModifier(StatEnum stat, NpcRank rank) => rank switch
    {
        NpcRank.NOVICE => stat switch
        {
            StatEnum.STUNLIKE_RESISTANCE => 0.2f,
            _ => 1f,
        },
        NpcRank.DISCIPLINED => stat switch
        {
            StatEnum.PHYSICAL_ATTACK or StatEnum.PHYSICAL_CRITICAL_RESIST => 1.2f,
            StatEnum.MAGICAL_RESIST => 1.02f,
            StatEnum.MAGICAL_DEFEND or StatEnum.PHYSICAL_DEFENSE => 1.1f,
            StatEnum.MAGICAL_ATTACK => 1.45f,
            StatEnum.PARRY => 1.05f,
            StatEnum.STUNLIKE_RESISTANCE => 0.4f,
            _ => 1f,
        },
        NpcRank.SEASONED => stat switch
        {
            StatEnum.PHYSICAL_ATTACK => 1.6f,
            StatEnum.MAGICAL_DEFEND or StatEnum.PHYSICAL_DEFENSE => 1.2f,
            StatEnum.MAGICAL_RESIST => 1.03f,
            StatEnum.MAGICAL_ATTACK => 1.45f,
            StatEnum.PARRY => 1.1f,
            StatEnum.PHYSICAL_ACCURACY or StatEnum.MAGICAL_ACCURACY => 1.01f,
            StatEnum.PHYSICAL_CRITICAL_RESIST => 1.4f,
            StatEnum.STUNLIKE_RESISTANCE => 0.6f,
            _ => 1f,
        },
        NpcRank.EXPERT => stat switch
        {
            StatEnum.PHYSICAL_ATTACK => 1.65f,
            StatEnum.MAGICAL_RESIST => 1.04f,
            StatEnum.MAGICAL_DEFEND or StatEnum.PHYSICAL_DEFENSE => 1.3f,
            StatEnum.MAGICAL_ATTACK => 1.7f,
            StatEnum.PARRY => 1.1f,
            StatEnum.PHYSICAL_ACCURACY or StatEnum.MAGICAL_ACCURACY => 1.02f,
            StatEnum.PHYSICAL_CRITICAL_RESIST => 1.6f,
            StatEnum.MAGICAL_CRITICAL_RESIST => 1.2f,
            _ => 1f,
        },
        NpcRank.VETERAN => stat switch
        {
            StatEnum.PHYSICAL_ATTACK or StatEnum.MAGICAL_ATTACK => 1.7f,
            StatEnum.MAGICAL_DEFEND or StatEnum.PHYSICAL_DEFENSE or StatEnum.STUNLIKE_RESISTANCE => 1.4f,
            StatEnum.MAGICAL_RESIST => 1.05f,
            StatEnum.PARRY => 1.12f,
            StatEnum.PHYSICAL_ACCURACY or StatEnum.MAGICAL_ACCURACY => 1.03f,
            StatEnum.PHYSICAL_CRITICAL_RESIST => 1.8f,
            StatEnum.MAGICAL_CRITICAL_RESIST => 1.25f,
            _ => 1f,
        },
        NpcRank.MASTER => stat switch
        {
            StatEnum.PHYSICAL_ATTACK => 1.85f,
            StatEnum.MAGICAL_DEFEND or StatEnum.PHYSICAL_DEFENSE => 1.5f,
            StatEnum.MAGICAL_RESIST => 1.06f,
            StatEnum.MAGICAL_ATTACK or StatEnum.STUNLIKE_RESISTANCE => 1.7f,
            StatEnum.PARRY => 1.12f,
            StatEnum.PHYSICAL_ACCURACY or StatEnum.MAGICAL_ACCURACY => 1.04f,
            StatEnum.PHYSICAL_CRITICAL_RESIST => 1.8f,
            StatEnum.MAGICAL_CRITICAL_RESIST => 1.25f,
            _ => 1f,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(rank)),
    };
}
