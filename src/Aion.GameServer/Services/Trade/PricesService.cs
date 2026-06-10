using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Siege;

namespace Aion.GameServer.Services.Trade;

/// <summary>Java parity: services/trade/PricesService (Sarynth, wakizashi). Effective price/tax calculation honoring abyss influence: getGlobalPrices/getTaxes (influence-scaled), getGlobalPricesModifier/vendor buy/sell modifiers, getPriceForService/getBuyPrice (ordered round-down multiply chains matching client), getSellReward. Math.round(float)->(int)Floor(x+0.5f); switch-on-Race + IllegalArgument->Argument; double /100D round-down casts preserved exactly. Influence/PricesConfig red-tolerated.</summary>
public class PricesService
{
    /// <summary>
    /// Used in SM_PRICES. Returns buyingPrice.
    /// </summary>
    public static int GetGlobalPrices(Race playerRace)
    {
        int defaultPrices = PricesConfig.DEFAULT_PRICES;
        float influenceValue = GetPriceInfluenceRate(playerRace);
        if (influenceValue == 0.5f)
        {
            return defaultPrices;
        }
        else if (influenceValue > 0.5f)
        {
            float diff = influenceValue - 0.5f;
            return (int)Math.Floor(defaultPrices - ((diff / 2) * 100) + 0.5f);
        }
        else
        {
            float diff = 0.5f - influenceValue;
            return (int)Math.Floor(defaultPrices + ((diff / 2) * 100) + 0.5f);
        }
    }

    /// <summary>
    /// Used in SM_PRICES
    /// </summary>
    public static int GetGlobalPricesModifier()
    {
        return PricesConfig.DEFAULT_MODIFIER;
    }

    /// <summary>
    /// Used in SM_PRICES
    /// </summary>
    public static int GetTaxes(Race playerRace)
    {
        int defaultTax = PricesConfig.DEFAULT_TAXES;
        float influenceValue = GetPriceInfluenceRate(playerRace);
        if (influenceValue >= 0.5f)
        {
            return defaultTax;
        }
        float diff = 0.5f - influenceValue;
        return (int)Math.Floor(defaultTax + ((diff / 4) * 100) + 0.5f);
    }

    private static float GetPriceInfluenceRate(Race playerRace)
    {
        switch (playerRace)
        {
            case Race.ASMODIANS:
                return Influence.GetInstance().GetAsmodianInfluenceRate();
            case Race.ELYOS:
                return Influence.GetInstance().GetElyosInfluenceRate();
        }
        throw new ArgumentException(playerRace + " is no valid player race.");
    }

    /// <summary>
    /// Used in SM_TRADELIST. Returns buyPriceModifier.
    /// </summary>
    public static int GetVendorBuyModifier()
    {
        return PricesConfig.VENDOR_BUY_MODIFIER;
    }

    /// <summary>
    /// Used in SM_SELL_ITEM. The default sellModifier, but some npcs and merchant pets use their own values.
    /// </summary>
    public static int GetVendorSellModifier()
    {
        return PricesConfig.VENDOR_SELL_MODIFIER;
    }

    /// <summary>
    /// The calculated price after taxes and global modifiers.
    /// </summary>
    public static long GetPriceForService(long basePrice, Race playerRace)
    {
        // Tricky. Requires multiplication by Prices, Modifier, Taxes
        // In order, and round down each time to match client calculation.
        return (long)((long)((long)(basePrice * GetGlobalPrices(playerRace) / 100D) * GetGlobalPricesModifier() / 100D) * GetTaxes(playerRace) / 100D);
    }

    /// <summary>
    /// The calculated price after taxes, vendor and global modifiers.
    /// </summary>
    public static long GetBuyPrice(long requiredKinah, Race playerRace)
    {
        // Requires double precision for 2mil+ kinah items
        return (long)((long)((long)((long)(requiredKinah * GetVendorBuyModifier() / 100D) * GetGlobalPrices(playerRace) / 100D)
            * GetGlobalPricesModifier() / 100D) * GetTaxes(playerRace) / 100D);
    }

    /// <summary>
    /// The calculated Kinah reward after applying sellModifier (default 20 = 20% of the original value, see GetVendorSellModifier()).
    /// </summary>
    public static long GetSellReward(long kinahValue, int sellModifier)
    {
        return (long)(kinahValue * sellModifier / 100D);
    }
}
