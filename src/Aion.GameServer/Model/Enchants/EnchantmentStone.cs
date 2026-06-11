using System;
using System.Collections.Generic;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/EnchantmentStone (Neon). Enum w/(baseLevel,baseQuality)→enum + EnchantmentStoneExtensions; getByItemId switch+range→C# switch/range; IllegalArgumentException→ArgumentException. ItemQuality red-tolerated.</summary>
public enum EnchantmentStone
{
    ALPHA,
    BETA,
    GAMMA,
    DELTA,
    EPSILON,
    OMEGA
}

public static class EnchantmentStoneExtensions
{
    private readonly struct StoneData
    {
        public readonly int BaseLevel;
        public readonly ItemQuality BaseQuality;

        public StoneData(int baseLevel, ItemQuality baseQuality)
        {
            BaseLevel = baseLevel;
            BaseQuality = baseQuality;
        }
    }

    private static readonly Dictionary<EnchantmentStone, StoneData> data = new()
    {
        [EnchantmentStone.ALPHA] = new StoneData(20, ItemQuality.RARE),
        [EnchantmentStone.BETA] = new StoneData(40, ItemQuality.LEGEND),
        [EnchantmentStone.GAMMA] = new StoneData(55, ItemQuality.UNIQUE),
        [EnchantmentStone.DELTA] = new StoneData(60, ItemQuality.EPIC),
        [EnchantmentStone.EPSILON] = new StoneData(65, ItemQuality.MYTHIC),
        [EnchantmentStone.OMEGA] = new StoneData(65, ItemQuality.MYTHIC),
    };

    public static int GetBaseLevel(this EnchantmentStone self)
    {
        return data[self].BaseLevel;
    }

    public static ItemQuality GetBaseQuality(this EnchantmentStone self)
    {
        return data[self].BaseQuality;
    }

    public static EnchantmentStone GetByItemId(int itemId)
    {
        switch (itemId)
        {
            case 166000191:
                return EnchantmentStone.ALPHA;
            case 166000192:
                return EnchantmentStone.BETA;
            case 166000193:
                return EnchantmentStone.GAMMA;
            case 166000194:
                return EnchantmentStone.DELTA;
            case 166000195:
                return EnchantmentStone.EPSILON;
            case 166020000:
            case 166020001:
            case 166020002:
            case 166020003:
                return EnchantmentStone.OMEGA;
            default:
                if (itemId >= 166000001 && itemId <= 166000190) // L1 - L190 (old stones)
                {
                    if (itemId > 166000100) // 101+
                    {
                        return EnchantmentStone.EPSILON;
                    }
                    else if (itemId > 166000060) // 61-100
                    {
                        return EnchantmentStone.DELTA;
                    }
                    else if (itemId > 166000050) // 51-60
                    {
                        return EnchantmentStone.GAMMA;
                    }
                    else if (itemId > 166000030) // 31-50
                    {
                        return EnchantmentStone.BETA;
                    }
                    else // 1-30
                    {
                        return EnchantmentStone.ALPHA;
                    }
                }
                throw new ArgumentException("No matching enchantment stone found for item ID " + itemId);
        }
    }
}
