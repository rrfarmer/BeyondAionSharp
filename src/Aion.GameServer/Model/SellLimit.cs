using System;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model;

/// <summary>
/// Java parity: model/SellLimit. Java enum with per-instance min/max level + limit → enum + extension accessors;
/// static getSellLimit(Player) walks values. NoSuchElementException → InvalidOperationException (closest analog).
/// </summary>
public enum SellLimit
{
    LIMIT_1_30,
    LIMIT_31_40,
    LIMIT_41_55,
    LIMIT_56_60,
    LIMIT_61_65
}

public static class SellLimitExtensions
{
    public static int GetPlayerMinLevel(this SellLimit s) => s switch
    {
        SellLimit.LIMIT_1_30 => 1,
        SellLimit.LIMIT_31_40 => 31,
        SellLimit.LIMIT_41_55 => 41,
        SellLimit.LIMIT_56_60 => 56,
        SellLimit.LIMIT_61_65 => 61,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static int GetPlayerMaxLevel(this SellLimit s) => s switch
    {
        SellLimit.LIMIT_1_30 => 30,
        SellLimit.LIMIT_31_40 => 40,
        SellLimit.LIMIT_41_55 => 55,
        SellLimit.LIMIT_56_60 => 60,
        SellLimit.LIMIT_61_65 => 65,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static long GetLimit(this SellLimit s) => s switch
    {
        SellLimit.LIMIT_1_30 => 5300047,
        SellLimit.LIMIT_31_40 => 7100047,
        SellLimit.LIMIT_41_55 => 12050047,
        SellLimit.LIMIT_56_60 => 14600047,
        SellLimit.LIMIT_61_65 => 17150047,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static long GetSellLimit(Player player)
    {
        int playerLevel = player.GetAccount().GetMaxPlayerLevel();
        foreach (SellLimit sellLimit in Enum.GetValues(typeof(SellLimit)))
        {
            if (sellLimit.GetPlayerMinLevel() <= playerLevel && sellLimit.GetPlayerMaxLevel() >= playerLevel)
            {
                return Rates.SELL_LIMIT.CalcResult(player, sellLimit.GetLimit());
            }
        }
        throw new InvalidOperationException("Sell limit for player level: " + playerLevel + " was not found");
    }
}
