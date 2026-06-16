namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/SiegeConfig (Sarynth, xTz, Source). SCREAMING_SNAKE field names + @Property defaults.
/// CronExpression fields map to Quartz.CronExpression (red until Quartz package referenced at Tier 4).
/// </summary>
public static class SiegeConfig
{
    /// <summary>Siege Enabled. Key: gameserver.siege.enable</summary>
    public static bool SIEGE_ENABLED = true;

    /// <summary>Balaur Assaults Enabled. Key: gameserver.siege.assault.enable</summary>
    public static bool BALAUR_AUTO_ASSAULT = false;

    /// <summary>Key: gameserver.siege.assault.rate</summary>
    public static float BALAUR_ASSAULT_RATE = 1;

    /// <summary>Berserker Sunayaka spawn time. Key: gameserver.moltenus.time. Default cron: 0 0 22 ? * SUN</summary>
    public static Quartz.CronExpression MOLTENUS_SPAWN_SCHEDULE = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 22 ? * SUN");

    /// <summary>Key: gameserver.siege.health.multiplier.fortress</summary>
    public static float FORTRESS_PROTECTOR_HEALTH_MULTIPLIER = 1;

    /// <summary>Key: gameserver.siege.health.multiplier.artifact</summary>
    public static float ARTIFACT_PROTECTOR_HEALTH_MULTIPLIER = 1;

    /// <summary>Key: gameserver.siege.health.multiplier.base</summary>
    public static float BASE_PROTECTOR_HEALTH_MULTIPLIER = 1;

    /// <summary>Key: gameserver.siege.difficulty.multiplier</summary>
    public static float SIEGE_DIFFICULTY_MULTIPLIER = 1;

    /// <summary>Key: gameserver.siege.panesterra.maxplayers</summary>
    public static int PANESTERRA_MAX_PLAYERS_PER_TEAM = 100;

    /// <summary>Key: gameserver.siege.panesterra.ahserion.maxplayers</summary>
    public static int AHSERION_MAX_PLAYERS_PER_TEAM = 100;

    /// <summary>Key: gameserver.siege.panesterra.ahserion.time. Default cron: 0 50 18 ? * SUN</summary>
    public static Quartz.CronExpression AHSERION_START_SCHEDULE = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 50 18 ? * SUN");

    /// <summary>Key: gameserver.siege.legion.gp.cap_per_member</summary>
    public static int LEGION_GP_CAP_PER_MEMBER = 200;

    /// <summary>Key: gameserver.siege.door.repair.heal.percent</summary>
    public static double DOOR_REPAIR_HEAL_PERCENT = 0.01;

    /// <summary>Key: gameserver.siege.reward.balaur.victory</summary>
    public static bool SIEGE_REWARD_BALAUR_VICTORY = false;

    /// <summary>Key: gameserver.siege.ignore_staff_on_location_clear</summary>
    public static bool IGNORE_STAFF_ON_LOCATION_CLEAR = false;
}
