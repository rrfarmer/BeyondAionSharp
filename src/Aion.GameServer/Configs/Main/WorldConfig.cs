using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/WorldConfig (ATracer) — world-level configuration constants. [Property] keys/defaults
/// bound at boot by <see cref="ConfigurableProcessor"/> from config/main/world.properties (operator overrides
/// honored). Java java.io.File ZONE_HANDLER_DIRECTORY → string path (StringTransformer).
/// <para>
/// The SCREAMING_SNAKE Java-named members carry the [Property] binding; the PascalCase backing members are the
/// storage that gameplay also reads through. Annotating only one settable member per key avoids double-binding.
/// </para>
/// </summary>
public static class WorldConfig
{
    // Java-name members carry the [Property] keys; they forward to the PascalCase backing storage.
    /// <summary>Java parity: WORLD_REGION_SIZE — default 128. Key: gameserver.world.region.size</summary>
    [Property(key: "gameserver.world.region.size", defaultValue: "128")]
    public static int WORLD_REGION_SIZE { get => WorldRegionSize; set => WorldRegionSize = value; }

    /// <summary>Java parity: WORLD_MAX_TWINS_USUAL — default 1. Key: gameserver.world.max.twincount.usual</summary>
    [Property(key: "gameserver.world.max.twincount.usual", defaultValue: "1")]
    public static int WORLD_MAX_TWINS_USUAL { get => WorldMaxTwinsUsual; set => WorldMaxTwinsUsual = value; }

    /// <summary>Java parity: WORLD_MAX_TWINS_BEGINNER — default -1. Key: gameserver.world.max.twincount.beginner</summary>
    [Property(key: "gameserver.world.max.twincount.beginner", defaultValue: "-1")]
    public static int WORLD_MAX_TWINS_BEGINNER { get => WorldMaxTwinsBeginner; set => WorldMaxTwinsBeginner = value; }

    /// <summary>Java parity: WORLD_EMULATE_FASTTRACK — default true. Key: gameserver.world.emulate.fasttrack</summary>
    [Property(key: "gameserver.world.emulate.fasttrack", defaultValue: "true")]
    public static bool WORLD_EMULATE_FASTTRACK { get => WorldEmulateFasttrack; set => WorldEmulateFasttrack = value; }

    /// <summary>Java parity: ZONE_HANDLER_DIRECTORY — default "./data/handlers/zone". Key: gameserver.world.zone_handler_directory</summary>
    [Property(key: "gameserver.world.zone_handler_directory", defaultValue: "./data/handlers/zone")]
    public static string ZONE_HANDLER_DIRECTORY { get => ZoneHandlerDirectory; set => ZoneHandlerDirectory = value; }

    // PascalCase backing storage (gameplay call sites also read these).
    public static int WorldRegionSize { get; set; } = 128;
    public static int WorldMaxTwinsUsual { get; set; } = 1;
    public static int WorldMaxTwinsBeginner { get; set; } = -1;
    public static bool WorldEmulateFasttrack { get; set; } = true;
    public static string ZoneHandlerDirectory { get; set; } = "./data/handlers/zone";
}
