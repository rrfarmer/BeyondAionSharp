namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/GSConfig. SCREAMING_SNAKE field names + @Property defaults.
/// Java java.time.ZoneId → System.TimeZoneInfo; java.io.File → string path.
/// </summary>
public static class GSConfig
{
    /// <summary>
    /// Server country code (client checks against its cc start parameter): 1=NA, 2=EU, 7=RU, 99=Region free
    /// (allows any client but limits character names to 10 chars). Key: gameserver.country.code
    /// </summary>
    public static int SERVER_COUNTRY_CODE = 99;

    /// <summary>Players Max Level. Key: gameserver.players.max.level</summary>
    public static int PLAYER_MAX_LEVEL = 65;

    /// <summary>Time Zone. Key: gameserver.timezone (framework-loaded, no default).</summary>
    public static System.TimeZoneInfo TIME_ZONE_ID;

    /// <summary>Enable connection with CS (ChatServer). Key: gameserver.chatserver.enable</summary>
    public static bool ENABLE_CHAT_SERVER = false;

    /// <summary>Min. required level to write in CS channels. Key: gameserver.chatserver.min_level</summary>
    public static byte CHAT_SERVER_MIN_LEVEL = 10;

    /// <summary>Key: gameserver.character.creation.mode</summary>
    public static int CHARACTER_CREATION_MODE = 0;

    /// <summary>Key: gameserver.character.limit.count</summary>
    public static int CHARACTER_LIMIT_COUNT = 8;

    /// <summary>Key: gameserver.character.faction.limitation.mode</summary>
    public static int CHARACTER_FACTION_LIMITATION_MODE = 0;

    /// <summary>Key: gameserver.ratio.limitation.enable</summary>
    public static bool ENABLE_RATIO_LIMITATION = false;

    /// <summary>Key: gameserver.ratio.min.value</summary>
    public static int RATIO_MIN_VALUE = 60;

    /// <summary>Key: gameserver.ratio.min.required.level</summary>
    public static int RATIO_MIN_REQUIRED_LEVEL = 10;

    /// <summary>Key: gameserver.ratio.min.characters_count</summary>
    public static int RATIO_MIN_CHARACTERS_COUNT = 50;

    /// <summary>Key: gameserver.ratio.high_player_count.disabling</summary>
    public static int RATIO_HIGH_PLAYER_COUNT_DISABLING = 500;

    /// <summary>Key: gameserver.character.reentry.time</summary>
    public static int CHARACTER_REENTRY_TIME = 20;

    /// <summary>Minimum ms between two skill casts (the game client enforces wait times accordingly). Key: gameserver.min_skill_cast_interval_millis</summary>
    public static int MIN_SKILL_CAST_INTERVAL_MILLIS = 350;

    /// <summary>Key: gameserver.item_wrap_limit</summary>
    public static int ITEM_WRAP_LIMIT = 0;

    /// <summary>Key: gameserver.web_rewards.enable</summary>
    public static bool ENABLE_WEB_REWARDS = false;

    /// <summary>Key: gameserver.analysis.quest_handlers</summary>
    public static bool ANALYZE_QUESTHANDLERS = true;

    /// <summary>Location of quest *.java handlers. Key: gameserver.quest.handler_directory (Java type: File).</summary>
    public static string QUEST_HANDLER_DIRECTORY = "./data/handlers/quest";
}
