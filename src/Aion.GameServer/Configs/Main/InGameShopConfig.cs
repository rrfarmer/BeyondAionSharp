using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/InGameShopConfig (xTz). [Property] keys/defaults bound at boot.</summary>
public static class InGameShopConfig
{
    /// <summary>Enable in game shop. Key: gameserver.ingameshop.enable</summary>
    [Property(key: "gameserver.ingameshop.enable", defaultValue: "false")]
    public static bool ENABLE_IN_GAME_SHOP = false;

    /// <summary>Enable gift system between factions. Key: gameserver.ingameshop.gift</summary>
    [Property(key: "gameserver.ingameshop.gift", defaultValue: "false")]
    public static bool ENABLE_GIFT_OTHER_RACE = false;

    /// <summary>Property key: gameserver.ingameshop.allow.gift</summary>
    [Property(key: "gameserver.ingameshop.allow.gift", defaultValue: "true")]
    public static bool ALLOW_GIFTS = true;
}
