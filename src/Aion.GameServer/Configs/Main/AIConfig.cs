using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/AIConfig (ATracer). SCREAMING_SNAKE field names + [Property] keys/defaults bound at boot
/// by <see cref="ConfigurableProcessor"/> from the loaded config/*.properties. Java java.io.File → string path.
/// </summary>
public static class AIConfig
{
    /// <summary>Debug (for developers). Property key: gameserver.ai.move.debug</summary>
    [Property(key: "gameserver.ai.move.debug", defaultValue: "true")]
    public static bool MOVE_DEBUG = true;

    /// <summary>Property key: gameserver.ai.event.debug</summary>
    [Property(key: "gameserver.ai.event.debug", defaultValue: "false")]
    public static bool EVENT_DEBUG = false;

    /// <summary>Property key: gameserver.ai.oncreate.debug</summary>
    [Property(key: "gameserver.ai.oncreate.debug", defaultValue: "false")]
    public static bool ONCREATE_DEBUG = false;

    /// <summary>Enable NPC movement. Property key: gameserver.npcmovement.enable</summary>
    [Property(key: "gameserver.npcmovement.enable", defaultValue: "true")]
    public static bool ACTIVE_NPC_MOVEMENT = true;

    /// <summary>Minimum movement delay. Property key: gameserver.npcmovement.delay.minimum</summary>
    [Property(key: "gameserver.npcmovement.delay.minimum", defaultValue: "3")]
    public static int MINIMIMUM_DELAY = 3;

    /// <summary>Maximum movement delay. Property key: gameserver.npcmovement.delay.maximum</summary>
    [Property(key: "gameserver.npcmovement.delay.maximum", defaultValue: "15")]
    public static int MAXIMUM_DELAY = 15;

    /// <summary>Npc Shouts activator. Property key: gameserver.npcshouts.enable</summary>
    [Property(key: "gameserver.npcshouts.enable", defaultValue: "false")]
    public static bool SHOUTS_ENABLE = false;

    /// <summary>Location of AI *.java handlers. Property key: gameserver.ai.handler_directory (Java type: File → string path).</summary>
    [Property(key: "gameserver.ai.handler_directory", defaultValue: "./data/handlers/ai")]
    public static string HANDLER_DIRECTORY = "./data/handlers/ai";
}
