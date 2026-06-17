using System.Collections.Generic;
using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/CustomConfig.
/// Fields keep Java SCREAMING_SNAKE names + @Property default values, bound from properties by ConfigurableProcessor.
/// CronExpression fields map to Quartz.NET's Quartz.CronExpression via the CronExpressionTransformer (registered by Config).
/// </summary>
public static class CustomConfig
{
    /// <summary>Enables challenge tasks. Key: gameserver.challenge.tasks.enabled</summary>
    [Property(key: "gameserver.challenge.tasks.enabled", defaultValue: "false")]
    public static bool CHALLENGE_TASKS_ENABLED = false;

    /// <summary>Announce when a player successfully enchants an item to +15 or +20. Key: gameserver.enchant.announce.enable</summary>
    [Property(key: "gameserver.enchant.announce.enable", defaultValue: "true")]
    public static bool ENABLE_ENCHANT_ANNOUNCE = true;

    /// <summary>Enable speaking between factions. Key: gameserver.chat.factions.enable</summary>
    [Property(key: "gameserver.chat.factions.enable", defaultValue: "false")]
    public static bool SPEAKING_BETWEEN_FACTIONS = false;

    /// <summary>Minimum level to use whisper. Key: gameserver.chat.whisper.level</summary>
    [Property(key: "gameserver.chat.whisper.level", defaultValue: "10")]
    public static int LEVEL_TO_WHISPER = 10;

    /// <summary>Days after which a broker item is unregistered (client cannot display more than 255 days). Key: gameserver.broker.registration_expiration_days</summary>
    [Property(key: "gameserver.broker.registration_expiration_days", defaultValue: "8")]
    public static int BROKER_REGISTRATION_EXPIRATION_DAYS = 8;

    /// <summary>Factions search mode. Key: gameserver.search.factions.mode</summary>
    [Property(key: "gameserver.search.factions.mode", defaultValue: "false")]
    public static bool FACTIONS_SEARCH_MODE = false;

    /// <summary>list gm when search players. Key: gameserver.search.gm.list</summary>
    [Property(key: "gameserver.search.gm.list", defaultValue: "false")]
    public static bool SEARCH_GM_LIST = false;

    /// <summary>Minimum level to use search. Key: gameserver.search.player.level</summary>
    [Property(key: "gameserver.search.player.level", defaultValue: "10")]
    public static int LEVEL_TO_SEARCH = 10;

    /// <summary>Allow opposite factions to bind in enemy territories. Key: gameserver.cross.faction.binding</summary>
    [Property(key: "gameserver.cross.faction.binding", defaultValue: "false")]
    public static bool ENABLE_CROSS_FACTION_BINDING = false;

    /// <summary>Enable second class change without quest. Key: gameserver.simple.secondclass.enable</summary>
    [Property(key: "gameserver.simple.secondclass.enable", defaultValue: "false")]
    public static bool ENABLE_SIMPLE_2NDCLASS = false;

    /// <summary>Disable chain trigger rate (chain skill with 100% success). Key: gameserver.skill.chain.disable_triggerrate</summary>
    [Property(key: "gameserver.skill.chain.disable_triggerrate", defaultValue: "false")]
    public static bool SKILL_CHAIN_DISABLE_TRIGGERRATE = false;

    /// <summary>Base Fly Time. Key: gameserver.base.flytime</summary>
    [Property(key: "gameserver.base.flytime", defaultValue: "60")]
    public static int BASE_FLYTIME = 60;

    /// <summary>Key: gameserver.friendlist.gm_restrict</summary>
    [Property(key: "gameserver.friendlist.gm_restrict", defaultValue: "false")]
    public static bool FRIENDLIST_GM_RESTRICT = false;

    /// <summary>Friendlist size. Key: gameserver.friendlist.size</summary>
    [Property(key: "gameserver.friendlist.size", defaultValue: "90")]
    public static int FRIENDLIST_SIZE = 90;

    /// <summary>Basic Quest limit size. Key: gameserver.basic.questsize.limit</summary>
    [Property(key: "gameserver.basic.questsize.limit", defaultValue: "40")]
    public static int BASIC_QUEST_SIZE_LIMIT = 40;

    /// <summary>Total number of allowed cube expansions. Key: gameserver.cube.expansion_limit</summary>
    [Property(key: "gameserver.cube.expansion_limit", defaultValue: "11")]
    public static int CUBE_EXPANSION_LIMIT = 11;

    /// <summary>Npc Cube Expands limit size. Key: gameserver.npcexpands.limit</summary>
    [Property(key: "gameserver.npcexpands.limit", defaultValue: "5")]
    public static int NPC_CUBE_EXPANDS_SIZE_LIMIT = 5;

    /// <summary>Enable Kinah cap. Key: gameserver.enable.kinah.cap</summary>
    [Property(key: "gameserver.enable.kinah.cap", defaultValue: "false")]
    public static bool ENABLE_KINAH_CAP = false;

    /// <summary>Kinah cap value. Key: gameserver.kinah.cap.value</summary>
    [Property(key: "gameserver.kinah.cap.value", defaultValue: "999999999")]
    public static long KINAH_CAP_VALUE = 999999999L;

    /// <summary>Enable AP cap. Key: gameserver.enable.ap.cap</summary>
    [Property(key: "gameserver.enable.ap.cap", defaultValue: "false")]
    public static bool ENABLE_AP_CAP = false;

    /// <summary>AP cap value. Key: gameserver.ap.cap.value</summary>
    [Property(key: "gameserver.ap.cap.value", defaultValue: "1000000")]
    public static long AP_CAP_VALUE = 1000000L;

    /// <summary>Enable no AP in mentored group. Key: gameserver.noap.mentor.group</summary>
    [Property(key: "gameserver.noap.mentor.group", defaultValue: "false")]
    public static bool MENTOR_GROUP_AP = false;

    /// <summary>.faction cfg. Key: gameserver.faction.price</summary>
    [Property(key: "gameserver.faction.price", defaultValue: "10000")]
    public static int FACTION_USE_PRICE = 10000;

    /// <summary>Key: gameserver.faction.cmdchannel</summary>
    [Property(key: "gameserver.faction.cmdchannel", defaultValue: "true")]
    public static bool FACTION_CMD_CHANNEL = true;

    /// <summary>Key: gameserver.faction.chatchannels</summary>
    [Property(key: "gameserver.faction.chatchannels", defaultValue: "false")]
    public static bool FACTION_CHAT_CHANNEL = false;

    /// <summary>Time in ms in which players are limited for killing one player. Key: gameserver.pvp.dayduration</summary>
    [Property(key: "gameserver.pvp.dayduration", defaultValue: "86400000")]
    public static long PVP_DAY_DURATION = 86400000L;

    /// <summary>Allowed Kills in configured time for full AP. Key: gameserver.pvp.maxkills</summary>
    [Property(key: "gameserver.pvp.maxkills", defaultValue: "5")]
    public static int MAX_DAILY_PVP_KILLS = 5;

    /// <summary>Add a reward to player for pvp kills. Key: gameserver.kill.reward.enable</summary>
    [Property(key: "gameserver.kill.reward.enable", defaultValue: "false")]
    public static bool ENABLE_KILL_REWARD = false;

    /// <summary>Keep buffs when killed in Sanctum's/Pandaemonium's Coliseum. Key: gameserver.coliseum.keep_buffs</summary>
    [Property(key: "gameserver.coliseum.keep_buffs", defaultValue: "false")]
    public static bool KEEP_BUFFS_IN_COLISEUM = false;

    /// <summary>Enable one kisk restriction. Key: gameserver.kisk.restriction.enable</summary>
    [Property(key: "gameserver.kisk.restriction.enable", defaultValue: "true")]
    public static bool ENABLE_KISK_RESTRICTION = true;

    /// <summary>Key: gameserver.rift.enable</summary>
    [Property(key: "gameserver.rift.enable", defaultValue: "true")]
    public static bool RIFT_ENABLED = true;

    /// <summary>Key: gameserver.rift.duration</summary>
    [Property(key: "gameserver.rift.duration", defaultValue: "1")]
    public static int RIFT_DURATION = 1;

    /// <summary>Key: gameserver.vortex.enable</summary>
    [Property(key: "gameserver.vortex.enable", defaultValue: "true")]
    public static bool VORTEX_ENABLED = true;

    /// <summary>Key: gameserver.vortex.brusthonin.schedule. Default cron: 0 0 16 ? * SAT.
    /// Java @Property defaultValue "0 0 16 ? * SAT" (config/main/custom.properties identical) transformed by
    /// CronExpressionTransformer (CronExpressions.getOrCreate). C# Config.Load is a deferred no-op, so this field
    /// carries the @Property default directly (same fix shape as SiegeConfig.MOLTENUS/AHSERION). No invented value.</summary>
    [Property(key: "gameserver.vortex.brusthonin.schedule", defaultValue: "0 0 16 ? * SAT")]
    public static Quartz.CronExpression VORTEX_BRUSTHONIN_SCHEDULE =
        Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 16 ? * SAT");

    /// <summary>Key: gameserver.vortex.theobomos.schedule. Default cron: 0 0 16 ? * SUN.
    /// Java @Property defaultValue "0 0 16 ? * SUN" (config/main/custom.properties identical) transformed by
    /// CronExpressionTransformer (CronExpressions.getOrCreate). C# Config.Load is a deferred no-op, so this field
    /// carries the @Property default directly. No invented value.</summary>
    [Property(key: "gameserver.vortex.theobomos.schedule", defaultValue: "0 0 16 ? * SUN")]
    public static Quartz.CronExpression VORTEX_THEOBOMOS_SCHEDULE =
        Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 16 ? * SUN");

    /// <summary>Key: gameserver.vortex.duration</summary>
    [Property(key: "gameserver.vortex.duration", defaultValue: "1")]
    public static int VORTEX_DURATION = 1;

    /// <summary>Key: gameserver.cp.enable</summary>
    [Property(key: "gameserver.cp.enable", defaultValue: "true")]
    public static bool CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED = true;

    /// <summary>Key: gameserver.cp.worlds</summary>
    [Property(key: "gameserver.cp.worlds", defaultValue: "210020000,210040000,210050000,210070000,220020000,220040000,220070000,220080000")]
    public static ISet<int> CONQUEROR_AND_PROTECTOR_WORLDS = new HashSet<int>
    {
        210020000, 210040000, 210050000, 210070000, 220020000, 220040000, 220070000, 220080000
    };

    /// <summary>Key: gameserver.cp.level.diff</summary>
    [Property(key: "gameserver.cp.level.diff", defaultValue: "5")]
    public static int CONQUEROR_AND_PROTECTOR_LEVEL_DIFF = 5;

    /// <summary>Key: gameserver.cp.kills.decrease_interval_minutes</summary>
    [Property(key: "gameserver.cp.kills.decrease_interval_minutes", defaultValue: "10")]
    public static int CONQUEROR_AND_PROTECTOR_KILLS_DECREASE_INTERVAL = 10;

    /// <summary>Key: gameserver.cp.kills.decrease_count</summary>
    [Property(key: "gameserver.cp.kills.decrease_count", defaultValue: "1")]
    public static int CONQUEROR_AND_PROTECTOR_KILLS_DECREASE_COUNT = 1;

    /// <summary>Key: gameserver.cp.kills.rank1</summary>
    [Property(key: "gameserver.cp.kills.rank1", defaultValue: "1")]
    public static int CONQUEROR_AND_PROTECTOR_KILLS_RANK1 = 1;

    /// <summary>Key: gameserver.cp.kills.rank2</summary>
    [Property(key: "gameserver.cp.kills.rank2", defaultValue: "10")]
    public static int CONQUEROR_AND_PROTECTOR_KILLS_RANK2 = 10;

    /// <summary>Key: gameserver.cp.kills.rank3</summary>
    [Property(key: "gameserver.cp.kills.rank3", defaultValue: "20")]
    public static int CONQUEROR_AND_PROTECTOR_KILLS_RANK3 = 20;

    /// <summary>Limits Config. Key: gameserver.limits.enable</summary>
    [Property(key: "gameserver.limits.enable", defaultValue: "true")]
    public static bool LIMITS_ENABLED = true;

    /// <summary>Key: gameserver.limits.enable_dynamic_cap</summary>
    [Property(key: "gameserver.limits.enable_dynamic_cap", defaultValue: "false")]
    public static bool LIMITS_ENABLE_DYNAMIC_CAP = false;

    /// <summary>Key: gameserver.limits.update. Default cron "0 0 0 ? * *" — initialized from the Java @Property
    /// defaultValue via CronExpressions.GetOrCreate (no invented value) so PlayerLimitService.ScheduleUpdate
    /// doesn't pass a null CronExpression to CronService.Schedule (the null-cron failure mode).</summary>
    [Property(key: "gameserver.limits.update", defaultValue: "0 0 0 ? * *")]
    public static Quartz.CronExpression LIMITS_UPDATE = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 0 ? * *");

    /// <summary>Key: gameserver.abyssxform.afterlogout</summary>
    [Property(key: "gameserver.abyssxform.afterlogout", defaultValue: "false")]
    public static bool ABYSSXFORM_LOGOUT = false;

    /// <summary>Key: gameserver.ride.restriction.enable</summary>
    [Property(key: "gameserver.ride.restriction.enable", defaultValue: "true")]
    public static bool ENABLE_RIDE_RESTRICTION = true;

    /// <summary>Enables sell apitems. Key: gameserver.selling.apitems.enabled</summary>
    [Property(key: "gameserver.selling.apitems.enabled", defaultValue: "true")]
    public static bool SELLING_APITEMS_ENABLED = true;

    /// <summary>Key: character.deletion.time.minutes</summary>
    [Property(key: "character.deletion.time.minutes", defaultValue: "5")]
    public static int CHARACTER_DELETION_TIME_MINUTES = 5;

    /// <summary>Don't consume potions when already at full HP/MP. Key: gameserver.items.ignore_potions_at_full_health</summary>
    [Property(key: "gameserver.items.ignore_potions_at_full_health", defaultValue: "false")]
    public static bool IGNORE_POTIONS_AT_FULL_HEALTH = false;

    /// <summary>Custom Reward Packages. Key: gameserver.custom.starter_kit.enable</summary>
    [Property(key: "gameserver.custom.starter_kit.enable", defaultValue: "false")]
    public static bool ENABLE_STARTER_KIT = false;

    /// <summary>Key: gameserver.pvpmap.enable</summary>
    [Property(key: "gameserver.pvpmap.enable", defaultValue: "false")]
    public static bool PVP_MAP_ENABLED = false;

    /// <summary>Key: gameserver.pvpmap.apmultiplier</summary>
    [Property(key: "gameserver.pvpmap.apmultiplier", defaultValue: "2")]
    public static float PVP_MAP_AP_MULTIPLIER = 2;

    /// <summary>Key: gameserver.pvpmap.pve.apmultiplier</summary>
    [Property(key: "gameserver.pvpmap.pve.apmultiplier", defaultValue: "1")]
    public static float PVP_MAP_PVE_AP_MULTIPLIER = 1;

    /// <summary>Key: gameserver.pvpmap.random_boss.rate</summary>
    [Property(key: "gameserver.pvpmap.random_boss.rate", defaultValue: "40")]
    public static int PVP_MAP_RANDOM_BOSS_BASE_RATE = 40;

    /// <summary>Key: gameserver.pvpmap.random_boss.time. Java @Property defaultValue "0 30 14,18,21 ? * *"
    /// (CustomConfig.java:264) parsed into a CronExpression via CronExpressions.GetOrCreate — same faithful inline-default
    /// pattern as AutoGroupConfig's CronExpression[] fields. Left null this NREs CronService.Schedule (cronExpression
    /// .CronExpressionString) when PvpMapHandler.StartRandomBossTask schedules off it during PvpMapService.Init().</summary>
    [Property(key: "gameserver.pvpmap.random_boss.time", defaultValue: "0 30 14,18,21 ? * *")]
    public static Quartz.CronExpression PVP_MAP_RANDOM_BOSS_SCHEDULE = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 30 14,18,21 ? * *");

    /// <summary>Key: gameserver.rates.godstone.activation.rate</summary>
    [Property(key: "gameserver.rates.godstone.activation.rate", defaultValue: "1.0")]
    public static float GODSTONE_ACTIVATION_RATE = 1.0f;

    /// <summary>Key: gameserver.rates.godstone.evaluation.cooldown_millis</summary>
    [Property(key: "gameserver.rates.godstone.evaluation.cooldown_millis", defaultValue: "750")]
    public static int GODSTONE_EVALUATION_COOLDOWN_MILLIS = 750;

    /// <summary>Count summon-applied abnormal effects for cumulative resist. Key: gameserver.pvp.cumulative_resist.count_summon_effects</summary>
    [Property(key: "gameserver.pvp.cumulative_resist.count_summon_effects", defaultValue: "false")]
    public static bool COUNT_SUMMON_EFFECTS_FOR_CUMULATIVE_RESIST = false;
}
