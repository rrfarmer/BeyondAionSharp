using System;

namespace Aion.GameServer.Utils.Stats;

/// <summary>Java parity: utils/stats/DropRewardEnum. Java enum w/ per-instance fields → enum + extensions (levelDifference/dropRewardPercent via switch).</summary>
public enum DropRewardEnum
{
    MINUS_10,
    MINUS_9,
    MINUS_8,
    MINUS_7,
    MINUS_6,
    MINUS_5
}

public static class DropRewardEnumExtensions
{
    private static int LevelDifference(DropRewardEnum e) => e switch
    {
        DropRewardEnum.MINUS_10 => -10,
        DropRewardEnum.MINUS_9 => -9,
        DropRewardEnum.MINUS_8 => -8,
        DropRewardEnum.MINUS_7 => -7,
        DropRewardEnum.MINUS_6 => -6,
        DropRewardEnum.MINUS_5 => -5,
        _ => 0,
    };

    public static int RewardPercent(this DropRewardEnum e) => e switch
    {
        DropRewardEnum.MINUS_10 => 0,
        DropRewardEnum.MINUS_9 => 40,
        DropRewardEnum.MINUS_8 => 60,
        DropRewardEnum.MINUS_7 => 70,
        DropRewardEnum.MINUS_6 => 80,
        DropRewardEnum.MINUS_5 => 100,
        _ => 0,
    };

    /// <param name="levelDifference">between two objects</param>
    /// <returns>Drop reward percentage</returns>
    public static int DropRewardFrom(int levelDifference)
    {
        if (levelDifference <= LevelDifference(DropRewardEnum.MINUS_10))
            return RewardPercent(DropRewardEnum.MINUS_10);
        else if (levelDifference >= LevelDifference(DropRewardEnum.MINUS_5))
            return RewardPercent(DropRewardEnum.MINUS_5);

        foreach (DropRewardEnum dropReward in Enum.GetValues<DropRewardEnum>())
        {
            if (LevelDifference(dropReward) == levelDifference)
            {
                return RewardPercent(dropReward);
            }
        }

        throw new InvalidOperationException("Drop reward for such level difference was not found");
    }
}
