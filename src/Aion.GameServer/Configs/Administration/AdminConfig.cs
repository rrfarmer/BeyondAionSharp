using System.Collections.Generic;
using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Administration;

/// <summary>
/// Java parity: configs/administration/AdminConfig (ATracer, Neon).
/// Fields keep Java SCREAMING_SNAKE names + @Property keys/default values (verbatim, incl. private-use unicode for the
/// custom-tag separators). Array/list fields are bound by the config framework (Array/Collection transformers).
/// </summary>
public static class AdminConfig
{
    /// <summary>
    /// Custom name tags based on access level (entry N = tag for access level N+1).
    /// Key: gameserver.administration.customtags. Default: per-level »JDev«/»Dev«/»JEM«/»EM«/»JGM«/»GM«/»SGM«/»Admin« tags
    /// separated by the private-use glyph .
    /// </summary>
    [Property(key: "gameserver.administration.customtags",
        defaultValue: "%s, »JDev«%s, »Dev«%s, »JEM«%s, »EM«%s, »JGM«%s, »GM«%s, »SGM«%s, »Admin«%s")]
    public static string[] NAME_TAGS;

    /// <summary>Key: gameserver.administration.unrestricted_itemtrade</summary>
    [Property(key: "gameserver.administration.unrestricted_itemtrade", defaultValue: "1")]
    public static sbyte UNRESTRICTED_ITEMTRADE = 1;

    /// <summary>Key: gameserver.administration.gm_panel</summary>
    [Property(key: "gameserver.administration.gm_panel", defaultValue: "2")]
    public static sbyte GM_PANEL = 2;

    /// <summary>Key: gameserver.administration.gm_skills</summary>
    [Property(key: "gameserver.administration.gm_skills", defaultValue: "8")]
    public static sbyte GM_SKILLS = 8;

    /// <summary>Key: gameserver.administration.flight.free_fly</summary>
    [Property(key: "gameserver.administration.flight.free_fly", defaultValue: "1")]
    public static sbyte FREE_FLIGHT = 1;

    /// <summary>Key: gameserver.administration.flight.unlimited_time</summary>
    [Property(key: "gameserver.administration.flight.unlimited_time", defaultValue: "1")]
    public static sbyte UNLIMITED_FLIGHT_TIME = 1;

    /// <summary>Key: gameserver.administration.auto_res</summary>
    [Property(key: "gameserver.administration.auto_res", defaultValue: "1")]
    public static sbyte AUTO_RES = 1;

    /// <summary>Key: gameserver.administration.view_player_details</summary>
    [Property(key: "gameserver.administration.view_player_details", defaultValue: "5")]
    public static sbyte VIEW_PLAYER_DETAILS = 5;

    /// <summary>Key: gameserver.administration.instance.enter_all</summary>
    [Property(key: "gameserver.administration.instance.enter_all", defaultValue: "2")]
    public static sbyte INSTANCE_ENTER_ALL = 2;

    /// <summary>Key: gameserver.administration.instance.open_doors</summary>
    [Property(key: "gameserver.administration.instance.open_doors", defaultValue: "6")]
    public static sbyte INSTANCE_OPEN_DOORS = 6;

    /// <summary>Key: gameserver.administration.instance.door_info</summary>
    [Property(key: "gameserver.administration.instance.door_info", defaultValue: "9")]
    public static sbyte INSTANCE_DOOR_INFO = 9;

    /// <summary>Key: gameserver.administration.house.enter_all</summary>
    [Property(key: "gameserver.administration.house.enter_all", defaultValue: "9")]
    public static sbyte HOUSE_ENTER_ALL = 9;

    /// <summary>Key: gameserver.administration.house.show_address</summary>
    [Property(key: "gameserver.administration.house.show_address", defaultValue: "9")]
    public static sbyte HOUSE_SHOW_ADDRESS = 9;

    /// <summary>Key: gameserver.administration.dialog_info</summary>
    [Property(key: "gameserver.administration.dialog_info", defaultValue: "9")]
    public static sbyte DIALOG_INFO = 9;

    /// <summary>Key: gameserver.administration.enchant_info</summary>
    [Property(key: "gameserver.administration.enchant_info", defaultValue: "9")]
    public static sbyte ENCHANT_INFO = 9;

    /// <summary>Key: gameserver.administration.zone_info</summary>
    [Property(key: "gameserver.administration.zone_info", defaultValue: "9")]
    public static sbyte ZONE_INFO = 9;

    /// <summary>Key: gameserver.administration.audit_info</summary>
    [Property(key: "gameserver.administration.audit_info", defaultValue: "9")]
    public static sbyte AUDIT_INFO = 9;

    /// <summary>Special command permissions. Key: gameserver.administration.command.quest.advanced_parameters</summary>
    [Property(key: "gameserver.administration.command.quest.advanced_parameters", defaultValue: "9")]
    public static sbyte CMD_QUEST_ADV_PARAMS = 9;

    /// <summary>Key: gameserver.administration.login.execute_commands. Default: //invis, //invul, //enemy none, //see</summary>
    [Property(key: "gameserver.administration.login.execute_commands", defaultValue: "//invis, //invul, //enemy none, //see")]
    public static List<string> LOGIN_EXECUTE_COMMANDS;

    /// <summary>Key: gameserver.administration.login.print_revision</summary>
    [Property(key: "gameserver.administration.login.print_revision", defaultValue: "9")]
    public static sbyte REVISION_INFO_ON_LOGIN = 9;

    /// <summary>Key: gameserver.administration.login.announce_levels. Default: *</summary>
    [Property(key: "gameserver.administration.login.announce_levels", defaultValue: "*")]
    public static List<string> ANNOUNCE_LEVELS;

    /// <summary>Key: gameserver.administration.login.announce_to_all_players</summary>
    [Property(key: "gameserver.administration.login.announce_to_all_players", defaultValue: "true")]
    public static bool ANNOUNCE_LOGIN_TO_ALL_PLAYERS = true;

    /// <summary>Key: gameserver.administration.logout.announce_to_all_players</summary>
    [Property(key: "gameserver.administration.logout.announce_to_all_players", defaultValue: "true")]
    public static bool ANNOUNCE_LOGOUT_TO_ALL_PLAYERS = true;
}
