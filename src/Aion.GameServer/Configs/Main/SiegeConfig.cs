using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/SiegeConfig (Sarynth, xTz, Source). SCREAMING_SNAKE field names + @Property defaults.
/// CronExpression fields map to Quartz.CronExpression, bound via the CronExpressionTransformer (registered by Config).
/// </summary>
public static class SiegeConfig
{
    /// <summary>Siege Enabled. Key: gameserver.siege.enable</summary>
    [Property(key: "gameserver.siege.enable", defaultValue: "true")]
    public static bool SIEGE_ENABLED = true;

    /// <summary>Balaur Assaults Enabled. Key: gameserver.siege.assault.enable</summary>
    [Property(key: "gameserver.siege.assault.enable", defaultValue: "false")]
    public static bool BALAUR_AUTO_ASSAULT = false;

    /// <summary>Key: gameserver.siege.assault.rate</summary>
    [Property(key: "gameserver.siege.assault.rate", defaultValue: "1")]
    public static float BALAUR_ASSAULT_RATE = 1;

    /// <summary>Berserker Sunayaka spawn time. Key: gameserver.moltenus.time. Default cron: 0 0 22 ? * SUN</summary>
    [Property(key: "gameserver.moltenus.time", defaultValue: "0 0 22 ? * SUN")]
    public static Quartz.CronExpression MOLTENUS_SPAWN_SCHEDULE = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 22 ? * SUN");

    /// <summary>Key: gameserver.siege.health.multiplier.fortress</summary>
    [Property(key: "gameserver.siege.health.multiplier.fortress", defaultValue: "1")]
    public static float FORTRESS_PROTECTOR_HEALTH_MULTIPLIER = 1;

    /// <summary>Key: gameserver.siege.health.multiplier.artifact</summary>
    [Property(key: "gameserver.siege.health.multiplier.artifact", defaultValue: "1")]
    public static float ARTIFACT_PROTECTOR_HEALTH_MULTIPLIER = 1;

    /// <summary>Key: gameserver.siege.health.multiplier.base</summary>
    [Property(key: "gameserver.siege.health.multiplier.base", defaultValue: "1")]
    public static float BASE_PROTECTOR_HEALTH_MULTIPLIER = 1;

    /// <summary>Key: gameserver.siege.difficulty.multiplier</summary>
    [Property(key: "gameserver.siege.difficulty.multiplier", defaultValue: "1")]
    public static float SIEGE_DIFFICULTY_MULTIPLIER = 1;

    /// <summary>Key: gameserver.siege.panesterra.maxplayers</summary>
    [Property(key: "gameserver.siege.panesterra.maxplayers", defaultValue: "100")]
    public static int PANESTERRA_MAX_PLAYERS_PER_TEAM = 100;

    /// <summary>Key: gameserver.siege.panesterra.ahserion.maxplayers</summary>
    [Property(key: "gameserver.siege.panesterra.ahserion.maxplayers", defaultValue: "100")]
    public static int AHSERION_MAX_PLAYERS_PER_TEAM = 100;

    /// <summary>Key: gameserver.siege.panesterra.ahserion.time. Default cron: 0 50 18 ? * SUN</summary>
    [Property(key: "gameserver.siege.panesterra.ahserion.time", defaultValue: "0 50 18 ? * SUN")]
    public static Quartz.CronExpression AHSERION_START_SCHEDULE = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 50 18 ? * SUN");

    /// <summary>Key: gameserver.siege.legion.gp.cap_per_member</summary>
    [Property(key: "gameserver.siege.legion.gp.cap_per_member", defaultValue: "200")]
    public static int LEGION_GP_CAP_PER_MEMBER = 200;

    /// <summary>Key: gameserver.siege.door.repair.heal.percent</summary>
    [Property(key: "gameserver.siege.door.repair.heal.percent", defaultValue: "0.01")]
    public static double DOOR_REPAIR_HEAL_PERCENT = 0.01;

    /// <summary>Key: gameserver.siege.reward.balaur.victory</summary>
    [Property(key: "gameserver.siege.reward.balaur.victory", defaultValue: "false")]
    public static bool SIEGE_REWARD_BALAUR_VICTORY = false;

    /// <summary>Key: gameserver.siege.ignore_staff_on_location_clear</summary>
    [Property(key: "gameserver.siege.ignore_staff_on_location_clear", defaultValue: "false")]
    public static bool IGNORE_STAFF_ON_LOCATION_CLEAR = false;
}
