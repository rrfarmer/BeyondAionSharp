using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/PeriodicSaveConfig (ATracer). [Property] keys/defaults bound at boot from config/*.properties.</summary>
public static class PeriodicSaveConfig
{
    /// <summary>Time in seconds for saving player data. Property key: gameserver.periodicsave.player.general</summary>
    [Property(key: "gameserver.periodicsave.player.general", defaultValue: "900")]
    public static int PLAYER_GENERAL = 900;

    /// <summary>Time in seconds for saving player items and item stones. Property key: gameserver.periodicsave.player.items</summary>
    [Property(key: "gameserver.periodicsave.player.items", defaultValue: "900")]
    public static int PLAYER_ITEMS = 900;

    /// <summary>Time in seconds for saving legion wh items and item stones. Property key: gameserver.periodicsave.legion.items</summary>
    [Property(key: "gameserver.periodicsave.legion.items", defaultValue: "1200")]
    public static int LEGION_ITEMS = 1200;

    /// <summary>Time in seconds for updating and saving pet mood data. Property key: gameserver.periodicsave.player.pets</summary>
    [Property(key: "gameserver.periodicsave.player.pets", defaultValue: "10")]
    public static int PLAYER_PETS = 10;
}
