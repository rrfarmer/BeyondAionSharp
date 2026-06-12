namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/WorldConfig — world-level configuration constants.
/// TODO-backlog: load from game server properties file via config framework when ported.
/// All fields carry the Java default values from @Property annotations.
/// </summary>
public static class WorldConfig
{
    // Java-name aliases over the PascalCase config fields (call sites use the Java SCREAMING_SNAKE names).
    public static int WORLD_REGION_SIZE { get => WorldRegionSize; set => WorldRegionSize = value; }
    public static bool WORLD_EMULATE_FASTTRACK { get => WorldEmulateFasttrack; set => WorldEmulateFasttrack = value; }
    public static string ZONE_HANDLER_DIRECTORY { get => ZoneHandlerDirectory; set => ZoneHandlerDirectory = value; }

    /// <summary>Java parity: WORLD_REGION_SIZE — default 128. Property key: gameserver.world.region.size</summary>
    public static int WorldRegionSize { get; set; } = 128;

    /// <summary>Java parity: WORLD_MAX_TWINS_USUAL — default 1. Property key: gameserver.world.max.twincount.usual</summary>
    public static int WorldMaxTwinsUsual { get; set; } = 1;

    /// <summary>Java parity: WORLD_MAX_TWINS_BEGINNER — default -1. Property key: gameserver.world.max.twincount.beginner</summary>
    public static int WorldMaxTwinsBeginner { get; set; } = -1;

    /// <summary>Java parity: WORLD_EMULATE_FASTTRACK — default true. Property key: gameserver.world.emulate.fasttrack</summary>
    public static bool WorldEmulateFasttrack { get; set; } = true;

    /// <summary>Java parity: ZONE_HANDLER_DIRECTORY — default "./data/handlers/zone". Property key: gameserver.world.zone_handler_directory</summary>
    public static string ZoneHandlerDirectory { get; set; } = "./data/handlers/zone";
}
