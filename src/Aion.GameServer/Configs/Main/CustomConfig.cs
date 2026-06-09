using System.Collections.Generic;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/CustomConfig.
/// Fields keep Java SCREAMING_SNAKE names + @Property default values (loaded from properties when the config framework is wired).
/// CronExpression fields map to Quartz.NET's Quartz.CronExpression (red until the Quartz package is referenced at Tier 4).
/// </summary>
public static class CustomConfig
{
    /// <summary>Enables challenge tasks. Key: gameserver.challenge.tasks.enabled</summary>
    public static bool CHALLENGE_TASKS_ENABLED = false;

    /// <summary>Announce when a player successfully enchants an item to +15 or +20. Key: gameserver.enchant.announce.enable</summary>
    public static bool ENABLE_ENCHANT_ANNOUNCE = true;

    /// <summary>Enable speaking between factions. Key: gameserver.chat.factions.enable</summary>
    public static bool SPEAKING_BETWEEN_FACTIONS = false;

    /// <summary>Minimum level to use whisper. Key: gameserver.chat.whisper.level</summary>
    public static int LEVEL_TO_WHISPER = 10;

    /// <summary>Days after which a broker item is unregistered (client cannot display more than 255 days). Key: gameserver.broker.registration_expiration_days</summary>
    public static int BROKER_REGISTRATION_EXPIRATION_DAYS = 8;

    /// <summary>Factions search mode. Key: gameserver.search.factions.mode</summary>
    public static bool FACTIONS_SEARCH_MODE = false;

    /// <summary>list gm when search players. Key: gameserver.search.gm.list</summary>
    public static bool SEARCH_GM_LIST = false;

    /// <summary>Minimum level to use search. Key: gameserver.search.player.level</summary>
    public static int LEVEL_TO_SEARCH = 10;

    /// <summary>Allow opposite factions to bind in enemy territories. Key: gameserver.cross.faction.binding</summary>
    public static bool ENABLE_CROSS_FACTION_BINDING = false;

    /// <summary>Enable second class change without quest. Key: gameserver.simple.secondclass.enable</summary>
    public static bool ENABLE_SIMPLE_2NDCLASS = false;

    /// <summary>Disable chain trigger rate (chain skill with 100% success). Key: gameserver.skill.chain.disable_triggerrate</summary>
    public static bool SKILL_CHAIN_DISABLE_TRIGGERRATE = false;

    /// <summary>Base Fly Time. Key: gameserver.base.flytime</summary>
    public static int BASE_FLYTIME = 60;

    /// <summary>Key: gameserver.friendlist.gm_restrict</summary>
    public static bool FRIENDLIST_GM_RESTRICT = false;

    /// <summary>Friendlist size. Key: gameserver.friendlist.size</summary>
    public static int FRIENDLIST_SIZE = 90;

    /// <summary>Basic Quest limit size. Key: gameserver.basic.questsize.limit</summary>
    public static int BASIC_QUEST_SIZE_LIMIT = 40;

    /// <summary>Total number of allowed cube expansions. Key: gameserver.cube.expansion_limit</summary>
    public static int CUBE_EXPANSION_LIMIT = 11;

    /// <summary>Npc Cube Expands limit size. Key: gameserver.npcexpands.limit</summary>
    public static int NPC_CUBE_EXPANDS_SIZE_LIMIT = 5;

    /// <summary>Enable Kinah cap. Key: gameserver.enable.kinah.cap</summary>
    public static bool ENABLE_KINAH_CAP = false;

    /// <summary>Kinah cap value. Key: gameserver.kinah.cap.value</summary>
    public static long KINAH_CAP_VALUE = 999999999L;

    /// <summary>Enable AP cap. Key: gameserver.enable.ap.cap</summary>
    public static bool ENABLE_AP_CAP = false;

    /// <summary>AP cap value. Key: gameserver.ap.cap.value</summary>
    public static long AP_CAP_VALUE = 1000000L;

    /// <summary>Enable no AP in mentored group. Key: gameserver.noap.mentor.group</summary>
    public static bool MENTOR_GROUP_AP = false;

    /// <summary>.faction cfg. Key: gameserver.faction.price</summary>
    public static int FACTION_USE_PRICE = 10000;

    /// <summary>Key: gameserver.faction.cmdchannel</summary>
    public static bool FACTION_CMD_CHANNEL = true;

    /// <summary>Key: gameserver.faction.chatchannels</summary>
    public static bool FACTION_CHAT_CHANNEL = false;

    /// <summary>Time in ms in which players are limited for killing one player. Key: gameserver.pvp.dayduration</summary>
    public static long PVP_DAY_DURATION = 86400000L;

    /// <summary>Allowed Kills in configured time for full AP. Key: gameserver.pvp.maxkills</summary>
    public static int MAX_DAILY_PVP_KILLS = 5;

    /// <summary>Add a reward to player for pvp kills. Key: gameserver.kill.reward.enable</summary>
    public static bool ENABLE_KILL_REWARD = false;

    /// <summary>Keep buffs when killed in Sanctum's/Pandaemonium's Coliseum. Key: gameserver.coliseum.keep_buffs</summary>
    public static bool KEEP_BUFFS_IN_COLISEUM = false;

    /// <summary>Enable one kisk restriction. Key: gameserver.kisk.restriction.enable</summary>
    public static bool ENABLE_KISK_RESTRICTION = true;

    /// <summary>Key: gameserver.rift.enable</summary>
    public static bool RIFT_ENABLED = true;

    /// <summary>Key: gameserver.rift.duration</summary>
    public static int RIFT_DURATION = 1;

    /// <summary>Key: gameserver.vortex.enable</summary>
    public static bool VORTEX_ENABLED = true;

    /// <summary>Key: gameserver.vortex.brusthonin.schedule. Default cron: 0 0 16 ? * SAT</summary>
    public static Quartz.CronExpression VORTEX_BRUSTHONIN_SCHEDULE;

    /// <summary>Key: gameserver.vortex.theobomos.schedule. Default cron: 0 0 16 ? * SUN</summary>
    public static Quartz.CronExpression VORTEX_THEOBOMOS_SCHEDULE;

    /// <summary>Key: gameserver.vortex.duration</summary>
    public static int VORTEX_DURATION = 1;

    /// <summary>Key: gameserver.cp.enable</summary>
    public static bool CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED = true;

    /// <summary>Key: gameserver.cp.worlds</summary>
    public static ISet<int> CONQUEROR_AND_PROTECTOR_WORLDS = new HashSet<int>
    {
        210020000, 210040000, 210050000, 210070000, 220020000, 220040000, 220070000, 220080000
    };

    /// <summary>Key: gameserver.cp.level.diff</summary>
    public static int CONQUEROR_AND_PROTECTOR_LEVEL_DIFF = 5;

    /// <summary>Key: gameserver.cp.kills.decrease_interval_minutes</summary>
    public static int CONQUEROR_AND_PROTECTOR_KILLS_DECREASE_INTERVAL = 10;

    /// <summary>Key: gameserver.cp.kills.decrease_count</summary>
    public static int CONQUEROR_AND_PROTECTOR_KILLS_DECREASE_COUNT = 1;

    /// <summary>Key: gameserver.cp.kills.rank1</summary>
    public static int CONQUEROR_AND_PROTECTOR_KILLS_RANK1 = 1;

    /// <summary>Key: gameserver.cp.kills.rank2</summary>
    public static int CONQUEROR_AND_PROTECTOR_KILLS_RANK2 = 10;

    /// <summary>Key: gameserver.cp.kills.rank3</summary>
    public static int CONQUEROR_AND_PROTECTOR_KILLS_RANK3 = 20;

    /// <summary>Limits Config. Key: gameserver.limits.enable</summary>
    public static bool LIMITS_ENABLED = true;

    /// <summary>Key: gameserver.limits.enable_dynamic_cap</summary>
    public static bool LIMITS_ENABLE_DYNAMIC_CAP = false;

    /// <summary>Key: gameserver.limits.update. Default cron: 0 0 0 ? * *</summary>
    public static Quartz.CronExpression LIMITS_UPDATE;

    /// <summary>Key: gameserver.abyssxform.afterlogout</summary>
    public static bool ABYSSXFORM_LOGOUT = false;

    /// <summary>Key: gameserver.ride.restriction.enable</summary>
    public static bool ENABLE_RIDE_RESTRICTION = true;

    /// <summary>Enables sell apitems. Key: gameserver.selling.apitems.enabled</summary>
    public static bool SELLING_APITEMS_ENABLED = true;

    /// <summary>Key: character.deletion.time.minutes</summary>
    public static int CHARACTER_DELETION_TIME_MINUTES = 5;

    /// <summary>Don't consume potions when already at full HP/MP. Key: gameserver.items.ignore_potions_at_full_health</summary>
    public static bool IGNORE_POTIONS_AT_FULL_HEALTH = false;

    /// <summary>Custom Reward Packages. Key: gameserver.custom.starter_kit.enable</summary>
    public static bool ENABLE_STARTER_KIT = false;

    /// <summary>Key: gameserver.pvpmap.enable</summary>
    public static bool PVP_MAP_ENABLED = false;

    /// <summary>Key: gameserver.pvpmap.apmultiplier</summary>
    public static float PVP_MAP_AP_MULTIPLIER = 2;

    /// <summary>Key: gameserver.pvpmap.pve.apmultiplier</summary>
    public static float PVP_MAP_PVE_AP_MULTIPLIER = 1;

    /// <summary>Key: gameserver.pvpmap.random_boss.rate</summary>
    public static int PVP_MAP_RANDOM_BOSS_BASE_RATE = 40;

    /// <summary>Key: gameserver.pvpmap.random_boss.time. Default cron: 0 30 14,18,21 ? * *</summary>
    public static Quartz.CronExpression PVP_MAP_RANDOM_BOSS_SCHEDULE;

    /// <summary>Key: gameserver.rates.godstone.activation.rate</summary>
    public static float GODSTONE_ACTIVATION_RATE = 1.0f;

    /// <summary>Key: gameserver.rates.godstone.evaluation.cooldown_millis</summary>
    public static int GODSTONE_EVALUATION_COOLDOWN_MILLIS = 750;

    /// <summary>Count summon-applied abnormal effects for cumulative resist. Key: gameserver.pvp.cumulative_resist.count_summon_effects</summary>
    public static bool COUNT_SUMMON_EFFECTS_FOR_CUMULATIVE_RESIST = false;
}
