using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/GSConfig. SCREAMING_SNAKE field names + [Property] keys/defaults bound at boot by
/// <see cref="ConfigurableProcessor"/> from the loaded config/*.properties (operator overrides honored).
/// Java java.time.ZoneId → System.TimeZoneInfo (via ZoneIdTransformer); java.io.File → string path.
/// </summary>
public static class GSConfig
{
    /// <summary>
    /// Server country code (client checks against its cc start parameter): 1=NA, 2=EU, 7=RU, 99=Region free
    /// (allows any client but limits character names to 10 chars). Key: gameserver.country.code
    /// </summary>
    [Property(key: "gameserver.country.code", defaultValue: "99")]
    public static int SERVER_COUNTRY_CODE = 99;

    /// <summary>Players Max Level. Key: gameserver.players.max.level</summary>
    [Property(key: "gameserver.players.max.level", defaultValue: "65")]
    public static int PLAYER_MAX_LEVEL = 65;

    /// <summary>
    /// Time Zone. Key: gameserver.timezone — required (no default, DO_NOT_OVERWRITE sentinel). Java ZoneId →
    /// TimeZoneInfo via ZoneIdTransformer (empty value → local time zone).
    /// </summary>
    [Property(key: "gameserver.timezone")]
    public static System.TimeZoneInfo TIME_ZONE_ID = System.TimeZoneInfo.Local;

    /// <summary>Enable connection with CS (ChatServer). Key: gameserver.chatserver.enable</summary>
    [Property(key: "gameserver.chatserver.enable", defaultValue: "false")]
    public static bool ENABLE_CHAT_SERVER;

    /// <summary>Min. required level to write in CS channels. Key: gameserver.chatserver.min_level</summary>
    [Property(key: "gameserver.chatserver.min_level", defaultValue: "10")]
    public static byte CHAT_SERVER_MIN_LEVEL = 10;

    /// <summary>Key: gameserver.character.creation.mode</summary>
    [Property(key: "gameserver.character.creation.mode", defaultValue: "0")]
    public static int CHARACTER_CREATION_MODE = 0;

    /// <summary>Key: gameserver.character.limit.count</summary>
    [Property(key: "gameserver.character.limit.count", defaultValue: "8")]
    public static int CHARACTER_LIMIT_COUNT = 8;

    /// <summary>Key: gameserver.character.faction.limitation.mode</summary>
    [Property(key: "gameserver.character.faction.limitation.mode", defaultValue: "0")]
    public static int CHARACTER_FACTION_LIMITATION_MODE = 0;

    /// <summary>Key: gameserver.ratio.limitation.enable</summary>
    [Property(key: "gameserver.ratio.limitation.enable", defaultValue: "false")]
    public static bool ENABLE_RATIO_LIMITATION;

    /// <summary>Key: gameserver.ratio.min.value</summary>
    [Property(key: "gameserver.ratio.min.value", defaultValue: "60")]
    public static int RATIO_MIN_VALUE = 60;

    /// <summary>Key: gameserver.ratio.min.required.level</summary>
    [Property(key: "gameserver.ratio.min.required.level", defaultValue: "10")]
    public static int RATIO_MIN_REQUIRED_LEVEL = 10;

    /// <summary>Key: gameserver.ratio.min.characters_count</summary>
    [Property(key: "gameserver.ratio.min.characters_count", defaultValue: "50")]
    public static int RATIO_MIN_CHARACTERS_COUNT = 50;

    /// <summary>Key: gameserver.ratio.high_player_count.disabling</summary>
    [Property(key: "gameserver.ratio.high_player_count.disabling", defaultValue: "500")]
    public static int RATIO_HIGH_PLAYER_COUNT_DISABLING = 500;

    /// <summary>Key: gameserver.character.reentry.time</summary>
    [Property(key: "gameserver.character.reentry.time", defaultValue: "20")]
    public static int CHARACTER_REENTRY_TIME = 20;

    /// <summary>Minimum ms between two skill casts (the game client enforces wait times accordingly). Key: gameserver.min_skill_cast_interval_millis</summary>
    [Property(key: "gameserver.min_skill_cast_interval_millis", defaultValue: "350")]
    public static int MIN_SKILL_CAST_INTERVAL_MILLIS = 350;

    /// <summary>Key: gameserver.item_wrap_limit</summary>
    [Property(key: "gameserver.item_wrap_limit", defaultValue: "0")]
    public static int ITEM_WRAP_LIMIT = 0;

    /// <summary>Key: gameserver.web_rewards.enable</summary>
    [Property(key: "gameserver.web_rewards.enable", defaultValue: "false")]
    public static bool ENABLE_WEB_REWARDS;

    /// <summary>Key: gameserver.analysis.quest_handlers</summary>
    [Property(key: "gameserver.analysis.quest_handlers", defaultValue: "true")]
    public static bool ANALYZE_QUESTHANDLERS = true;

    /// <summary>Location of quest *.java handlers. Key: gameserver.quest.handler_directory (Java type: File → string path).</summary>
    [Property(key: "gameserver.quest.handler_directory", defaultValue: "./data/handlers/quest")]
    public static string QUEST_HANDLER_DIRECTORY = "./data/handlers/quest";
}
