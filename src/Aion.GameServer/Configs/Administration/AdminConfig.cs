using System.Collections.Generic;

namespace Aion.GameServer.Configs.Administration;

/// <summary>
/// Java parity: configs/administration/AdminConfig (ATracer, Neon).
/// Fields keep Java SCREAMING_SNAKE names + @Property default values.
/// Array/list fields are populated by the config framework (default templates noted; left null pre-wiring to avoid embedding private-use unicode in source).
/// </summary>
public static class AdminConfig
{
    /// <summary>
    /// Custom name tags based on access level (entry N = tag for access level N+1).
    /// Key: gameserver.administration.customtags. Default: per-level »JDev«/»Dev«/»JEM«/»EM«/»JGM«/»GM«/»SGM«/»Admin« tags.
    /// </summary>
    public static string[] NAME_TAGS;

    /// <summary>Key: gameserver.administration.unrestricted_itemtrade</summary>
    public static sbyte UNRESTRICTED_ITEMTRADE = 1;

    /// <summary>Key: gameserver.administration.gm_panel</summary>
    public static sbyte GM_PANEL = 2;

    /// <summary>Key: gameserver.administration.gm_skills</summary>
    public static sbyte GM_SKILLS = 8;

    /// <summary>Key: gameserver.administration.flight.free_fly</summary>
    public static sbyte FREE_FLIGHT = 1;

    /// <summary>Key: gameserver.administration.flight.unlimited_time</summary>
    public static sbyte UNLIMITED_FLIGHT_TIME = 1;

    /// <summary>Key: gameserver.administration.auto_res</summary>
    public static sbyte AUTO_RES = 1;

    /// <summary>Key: gameserver.administration.view_player_details</summary>
    public static sbyte VIEW_PLAYER_DETAILS = 5;

    /// <summary>Key: gameserver.administration.instance.enter_all</summary>
    public static sbyte INSTANCE_ENTER_ALL = 2;

    /// <summary>Key: gameserver.administration.instance.open_doors</summary>
    public static sbyte INSTANCE_OPEN_DOORS = 6;

    /// <summary>Key: gameserver.administration.instance.door_info</summary>
    public static sbyte INSTANCE_DOOR_INFO = 9;

    /// <summary>Key: gameserver.administration.house.enter_all</summary>
    public static sbyte HOUSE_ENTER_ALL = 9;

    /// <summary>Key: gameserver.administration.house.show_address</summary>
    public static sbyte HOUSE_SHOW_ADDRESS = 9;

    /// <summary>Key: gameserver.administration.dialog_info</summary>
    public static sbyte DIALOG_INFO = 9;

    /// <summary>Key: gameserver.administration.enchant_info</summary>
    public static sbyte ENCHANT_INFO = 9;

    /// <summary>Key: gameserver.administration.zone_info</summary>
    public static sbyte ZONE_INFO = 9;

    /// <summary>Key: gameserver.administration.audit_info</summary>
    public static sbyte AUDIT_INFO = 9;

    /// <summary>Special command permissions. Key: gameserver.administration.command.quest.advanced_parameters</summary>
    public static sbyte CMD_QUEST_ADV_PARAMS = 9;

    /// <summary>Key: gameserver.administration.login.execute_commands. Default: //invis, //invul, //enemy none, //see</summary>
    public static List<string> LOGIN_EXECUTE_COMMANDS;

    /// <summary>Key: gameserver.administration.login.print_revision</summary>
    public static sbyte REVISION_INFO_ON_LOGIN = 9;

    /// <summary>Key: gameserver.administration.login.announce_levels. Default: *</summary>
    public static List<string> ANNOUNCE_LEVELS;

    /// <summary>Key: gameserver.administration.login.announce_to_all_players</summary>
    public static bool ANNOUNCE_LOGIN_TO_ALL_PLAYERS = true;

    /// <summary>Key: gameserver.administration.logout.announce_to_all_players</summary>
    public static bool ANNOUNCE_LOGOUT_TO_ALL_PLAYERS = true;
}
