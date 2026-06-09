using System;

namespace Aion.GameServer.Utils.Stats;

/// <summary>Java parity: utils/stats/XPLossEnum (ATracer, Jangan). Java enum w/ per-instance fields → enum + extensions; Math.round→(long)Math.Floor(x+0.5).</summary>
public enum XPLossEnum
{
    LEVEL_6,
    LEVEL_30,
    LEVEL_40,
    LEVEL_50,
    LEVEL_55,
    LEVEL_60,
    LEVEL_65
}

public static class XPLossEnumExtensions
{
    public static int GetLevel(this XPLossEnum e) => e switch
    {
        XPLossEnum.LEVEL_6 => 6,
        XPLossEnum.LEVEL_30 => 30,
        XPLossEnum.LEVEL_40 => 40,
        XPLossEnum.LEVEL_50 => 50,
        XPLossEnum.LEVEL_55 => 55,
        XPLossEnum.LEVEL_60 => 60,
        XPLossEnum.LEVEL_65 => 65,
        _ => 0,
    };

    public static double GetParam(this XPLossEnum e) => e switch
    {
        XPLossEnum.LEVEL_6 => 1.0,
        XPLossEnum.LEVEL_30 => 1.0,
        XPLossEnum.LEVEL_40 => 0.35,
        XPLossEnum.LEVEL_50 => 0.25,
        XPLossEnum.LEVEL_55 => 0.25,
        XPLossEnum.LEVEL_60 => 0.25,
        XPLossEnum.LEVEL_65 => 0.25,
        _ => 0.0,
    };

    public static long GetExpLoss(int level, long expNeed)
    {
        if (level < 6)
            return 0;

        foreach (XPLossEnum xpLossEnum in Enum.GetValues<XPLossEnum>())
        {
            if (level <= xpLossEnum.GetLevel())
                return (long)Math.Floor(expNeed / 100 * xpLossEnum.GetParam() + 0.5);
        }
        return 0;
    }
}
