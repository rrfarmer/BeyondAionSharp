using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/CleaningConfig (nrg). [Property] keys/defaults bound at boot from config/*.properties.</summary>
public static class CleaningConfig
{
    /// <summary>Property key: gameserver.cleaning.enable</summary>
    [Property(key: "gameserver.cleaning.enable", defaultValue: "false")]
    public static bool CLEANING_ENABLE = false;

    /// <summary>Property key: gameserver.cleaning.min_account_inactivity</summary>
    [Property(key: "gameserver.cleaning.min_account_inactivity", defaultValue: "365")]
    public static int MIN_ACCOUNT_INACTIVITY_DAYS = 365;

    /// <summary>Property key: gameserver.cleaning.max_level</summary>
    [Property(key: "gameserver.cleaning.max_level", defaultValue: "25")]
    public static int MAX_DELETABLE_CHAR_LEVEL = 25;
}
