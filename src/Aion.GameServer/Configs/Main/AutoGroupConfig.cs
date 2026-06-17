using Quartz;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/AutoGroupConfig (xTz). @Property defaults as field initializers; CronExpression[] (Quartz) populated by config loader.</summary>
public static class AutoGroupConfig
{
    /// <summary>Key: gameserver.autogroup.enable (default true)</summary>
    public static bool AUTO_GROUP_ENABLE = true;

    /// <summary>Key: gameserver.startTime.enable (default true)</summary>
    public static bool START_TIME_ENABLE = true;

    /// <summary>Key: gameserver.dredgion.registration_period (default 60)</summary>
    public static long DREDGION_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.dredgion.time (default "0 0 0,12,20 ? * *"). Java @Property defaultValue is a single
    /// quote-wrapped cron (ArrayTransformer splits on commas OUTSIDE quotes => 1-element array), so this is a
    /// 1-element CronExpression[] built via CronExpressions.GetOrCreate (no invented value). Without this the
    /// field was null and PeriodicInstanceManager.ScheduleRegistration foreach NRE'd (the null-cron failure mode).</summary>
    public static CronExpression[] DREDGION_TIMES = { Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 0,12,20 ? * *") };

    /// <summary>Key: gameserver.kamar_battlefield.registration_period (default 60)</summary>
    public static long KAMAR_BATTLEFIELD_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.kamar_battlefield.time (default "0 0 0,20 ? * MON,WED,SAT"). 1-element CronExpression[]
    /// from the Java @Property defaultValue (quote-wrapped single cron) via CronExpressions.GetOrCreate.</summary>
    public static CronExpression[] KAMAR_BATTLEFIELD_TIMES = { Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 0,20 ? * MON,WED,SAT") };

    /// <summary>Key: gameserver.engulfed_ophidan_bridge.registration_period (default 60)</summary>
    public static long ENGULFED_OPHIDAN_BRIDGE_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.engulfed_ophidan_bridge.time (default "0 0 12,19 ? * *"). 1-element CronExpression[]
    /// from the Java @Property defaultValue via CronExpressions.GetOrCreate.</summary>
    public static CronExpression[] ENGULFED_OPHIDAN_BRIDGE_TIMES = { Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 12,19 ? * *") };

    /// <summary>Key: gameserver.iron_wall_warfront.registration_period (default 60)</summary>
    public static long IRON_WALL_WARFRONT_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.iron_wall_warfront.time (default "0 0 0,12 ? * SUN"). 1-element CronExpression[]
    /// from the Java @Property defaultValue via CronExpressions.GetOrCreate.</summary>
    public static CronExpression[] IRON_WALL_WARFRONT_TIMES = { Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 0,12 ? * SUN") };

    /// <summary>Key: gameserver.idgel_dome.registration_period (default 60)</summary>
    public static long IDGEL_DOME_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.idgel_dome.time (default "0 0 23 ? * *"). 1-element CronExpression[] from the Java
    /// @Property defaultValue (unquoted single cron) via CronExpressions.GetOrCreate.</summary>
    public static CronExpression[] IDGEL_DOME_TIMES = { Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 23 ? * *") };

    /// <summary>Key: gameserver.autogroup.announce_battleground_registrations (default false)</summary>
    public static bool ANNOUNCE_BATTLEGROUND_REGISTRATIONS = false;
}
