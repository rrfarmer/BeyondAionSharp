namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/AIConfig (ATracer).
/// Fields keep Java SCREAMING_SNAKE names + @Property default values (loaded from properties when the config framework is wired).
/// </summary>
public static class AIConfig
{
    /// <summary>Debug (for developers). Property key: gameserver.ai.move.debug</summary>
    public static bool MOVE_DEBUG = true;

    /// <summary>Property key: gameserver.ai.event.debug</summary>
    public static bool EVENT_DEBUG = false;

    /// <summary>Property key: gameserver.ai.oncreate.debug</summary>
    public static bool ONCREATE_DEBUG = false;

    /// <summary>Enable NPC movement. Property key: gameserver.npcmovement.enable</summary>
    public static bool ACTIVE_NPC_MOVEMENT = true;

    /// <summary>Minimum movement delay. Property key: gameserver.npcmovement.delay.minimum</summary>
    public static int MINIMIMUM_DELAY = 3;

    /// <summary>Maximum movement delay. Property key: gameserver.npcmovement.delay.maximum</summary>
    public static int MAXIMUM_DELAY = 15;

    /// <summary>Npc Shouts activator. Property key: gameserver.npcshouts.enable</summary>
    public static bool SHOUTS_ENABLE = false;

    /// <summary>Location of AI *.java handlers. Property key: gameserver.ai.handler_directory (Java type: File).</summary>
    public static string HANDLER_DIRECTORY = "./data/handlers/ai";
}
