using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/PricesConfig (Sarynth). [Property] keys/defaults bound at boot.</summary>
public static class PricesConfig
{
    /// <summary>Controls the "Prices:" value in influence tab. Property key: gameserver.prices.default.prices</summary>
    [Property(key: "gameserver.prices.default.prices", defaultValue: "100")]
    public static int DEFAULT_PRICES = 100;

    /// <summary>Hidden modifier for all prices. Property key: gameserver.prices.default.modifier</summary>
    [Property(key: "gameserver.prices.default.modifier", defaultValue: "100")]
    public static int DEFAULT_MODIFIER = 100;

    /// <summary>Taxes: value = 100 + tax %. Property key: gameserver.prices.default.taxes</summary>
    [Property(key: "gameserver.prices.default.taxes", defaultValue: "100")]
    public static int DEFAULT_TAXES = 100;

    /// <summary>Property key: gameserver.prices.vendor.buymod</summary>
    [Property(key: "gameserver.prices.vendor.buymod", defaultValue: "100")]
    public static int VENDOR_BUY_MODIFIER = 100;

    /// <summary>Property key: gameserver.prices.vendor.sellmod</summary>
    [Property(key: "gameserver.prices.vendor.sellmod", defaultValue: "20")]
    public static int VENDOR_SELL_MODIFIER = 20;
}
