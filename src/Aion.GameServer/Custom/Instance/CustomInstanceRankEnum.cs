using System;

namespace Aion.GameServer.Custom.Instance;

/// <summary>Java parity: custom/instance/CustomInstanceRankEnum (Jo). Java enum w/ per-instance minRank/minReward/rankIcon(char PUA glyphs) -> enum + extensions.</summary>
public enum CustomInstanceRankEnum
{
    IRON,
    BRONZE,
    SILVER,
    GOLD,
    PLATINUM,
    MITHRIL,
    CERANIUM,
    ANCIENT,
    ANCIENT_PLUS
}

public static class CustomInstanceRankEnumExtensions
{
    public static int GetMinRank(this CustomInstanceRankEnum e) => e switch
    {
        CustomInstanceRankEnum.IRON => 0,
        CustomInstanceRankEnum.BRONZE => 1,
        CustomInstanceRankEnum.SILVER => 3,
        CustomInstanceRankEnum.GOLD => 5,
        CustomInstanceRankEnum.PLATINUM => 8,
        CustomInstanceRankEnum.MITHRIL => 12,
        CustomInstanceRankEnum.CERANIUM => 16,
        CustomInstanceRankEnum.ANCIENT => 20,
        CustomInstanceRankEnum.ANCIENT_PLUS => 24,
        _ => 0,
    };

    public static int GetMinReward(this CustomInstanceRankEnum e) => e switch
    {
        CustomInstanceRankEnum.IRON => 4,
        CustomInstanceRankEnum.BRONZE => 10,
        CustomInstanceRankEnum.SILVER => 16,
        CustomInstanceRankEnum.GOLD => 22,
        CustomInstanceRankEnum.PLATINUM => 28,
        CustomInstanceRankEnum.MITHRIL => 34,
        CustomInstanceRankEnum.CERANIUM => 40,
        CustomInstanceRankEnum.ANCIENT => 46,
        CustomInstanceRankEnum.ANCIENT_PLUS => 50,
        _ => 0,
    };

    public static char GetRankIcon(this CustomInstanceRankEnum e) => e switch
    {
        CustomInstanceRankEnum.IRON => '\uE02B',
        CustomInstanceRankEnum.BRONZE => '\uE02C',
        CustomInstanceRankEnum.SILVER => '\uE02D',
        CustomInstanceRankEnum.GOLD => '\uE02E',
        CustomInstanceRankEnum.PLATINUM => '\uE02F',
        CustomInstanceRankEnum.MITHRIL => '\uE030',
        CustomInstanceRankEnum.CERANIUM => '\uE0A8',
        CustomInstanceRankEnum.ANCIENT => '\uE0AA',
        CustomInstanceRankEnum.ANCIENT_PLUS => '\uE0AA',
        _ => '\u0000',
    };

    public static string GetRankDescription(int value)
    {
        CustomInstanceRankEnum rank = GetByRank(value);
        return rank.GetRankIcon() + " " + (rank == CustomInstanceRankEnum.ANCIENT_PLUS ? "ANCIENT +" + (value + 1 - rank.GetMinRank()) : rank.ToString());
    }

    public static CustomInstanceRankEnum GetByRank(int rank)
    {
        CustomInstanceRankEnum[] values = Enum.GetValues<CustomInstanceRankEnum>();
        for (int i = values.Length - 1; i >= 0; i--)
        {
            if (values[i].GetMinRank() <= rank)
                return values[i];
        }
        throw new ArgumentException(rank + " is no valid rank");
    }
}
