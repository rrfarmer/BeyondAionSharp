using System;

namespace Aion.GameServer.Utils.Stats;

/// <summary>Java parity: utils/stats/XPRewardEnum (ATracer). Java enum w/ per-instance fields → enum + extensions.</summary>
public enum XPRewardEnum
{
    MINUS_11,
    MINUS_10,
    MINUS_9,
    MINUS_8,
    MINUS_7,
    MINUS_6,
    MINUS_5,
    MINUS_4,
    MINUS_3,
    MINUS_2,
    MINUS_1,
    ZERO,
    PLUS_1,
    PLUS_2,
    PLUS_3,
    PLUS_4
}

public static class XPRewardEnumExtensions
{
    private static int LevelDifference(XPRewardEnum e) => e switch
    {
        XPRewardEnum.MINUS_11 => -11,
        XPRewardEnum.MINUS_10 => -10,
        XPRewardEnum.MINUS_9 => -9,
        XPRewardEnum.MINUS_8 => -8,
        XPRewardEnum.MINUS_7 => -7,
        XPRewardEnum.MINUS_6 => -6,
        XPRewardEnum.MINUS_5 => -5,
        XPRewardEnum.MINUS_4 => -4,
        XPRewardEnum.MINUS_3 => -3,
        XPRewardEnum.MINUS_2 => -2,
        XPRewardEnum.MINUS_1 => -1,
        XPRewardEnum.ZERO => 0,
        XPRewardEnum.PLUS_1 => 1,
        XPRewardEnum.PLUS_2 => 2,
        XPRewardEnum.PLUS_3 => 3,
        XPRewardEnum.PLUS_4 => 4,
        _ => 0,
    };

    public static int RewardPercent(this XPRewardEnum e) => e switch
    {
        XPRewardEnum.MINUS_11 => 0,
        XPRewardEnum.MINUS_10 => 1,
        XPRewardEnum.MINUS_9 => 10,
        XPRewardEnum.MINUS_8 => 20,
        XPRewardEnum.MINUS_7 => 30,
        XPRewardEnum.MINUS_6 => 40,
        XPRewardEnum.MINUS_5 => 50,
        XPRewardEnum.MINUS_4 => 60,
        XPRewardEnum.MINUS_3 => 90,
        XPRewardEnum.MINUS_2 => 100,
        XPRewardEnum.MINUS_1 => 100,
        XPRewardEnum.ZERO => 100,
        XPRewardEnum.PLUS_1 => 105,
        XPRewardEnum.PLUS_2 => 110,
        XPRewardEnum.PLUS_3 => 115,
        XPRewardEnum.PLUS_4 => 120,
        _ => 0,
    };

    /// <param name="levelDifference">between two objects</param>
    /// <returns>XP reward percentage</returns>
    public static int XpRewardFrom(int levelDifference)
    {
        if (levelDifference < LevelDifference(XPRewardEnum.MINUS_11))
        {
            return RewardPercent(XPRewardEnum.MINUS_11);
        }
        if (levelDifference > LevelDifference(XPRewardEnum.PLUS_4))
        {
            return RewardPercent(XPRewardEnum.PLUS_4);
        }

        foreach (XPRewardEnum xpReward in Enum.GetValues<XPRewardEnum>())
        {
            if (LevelDifference(xpReward) == levelDifference)
            {
                return RewardPercent(xpReward);
            }
        }

        throw new InvalidOperationException("XP reward for such level difference was not found");
    }
}
