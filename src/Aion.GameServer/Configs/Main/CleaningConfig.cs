namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/CleaningConfig.</summary>
public static class CleaningConfig
{
    /// <summary>Property key: gameserver.cleaning.enable</summary>
    public static bool CLEANING_ENABLE = false;

    /// <summary>Property key: gameserver.cleaning.min_account_inactivity</summary>
    public static int MIN_ACCOUNT_INACTIVITY_DAYS = 365;

    /// <summary>Property key: gameserver.cleaning.max_level</summary>
    public static int MAX_DELETABLE_CHAR_LEVEL = 25;
}
