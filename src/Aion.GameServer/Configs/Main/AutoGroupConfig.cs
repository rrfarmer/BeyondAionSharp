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

    /// <summary>Key: gameserver.dredgion.time (default "0 0 0,12,20 ? * *")</summary>
    public static CronExpression[] DREDGION_TIMES;

    /// <summary>Key: gameserver.kamar_battlefield.registration_period (default 60)</summary>
    public static long KAMAR_BATTLEFIELD_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.kamar_battlefield.time (default "0 0 0,20 ? * MON,WED,SAT")</summary>
    public static CronExpression[] KAMAR_BATTLEFIELD_TIMES;

    /// <summary>Key: gameserver.engulfed_ophidan_bridge.registration_period (default 60)</summary>
    public static long ENGULFED_OPHIDAN_BRIDGE_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.engulfed_ophidan_bridge.time (default "0 0 12,19 ? * *")</summary>
    public static CronExpression[] ENGULFED_OPHIDAN_BRIDGE_TIMES;

    /// <summary>Key: gameserver.iron_wall_warfront.registration_period (default 60)</summary>
    public static long IRON_WALL_WARFRONT_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.iron_wall_warfront.time (default "0 0 0,12 ? * SUN")</summary>
    public static CronExpression[] IRON_WALL_WARFRONT_TIMES;

    /// <summary>Key: gameserver.idgel_dome.registration_period (default 60)</summary>
    public static long IDGEL_DOME_REGISTRATION_PERIOD = 60;

    /// <summary>Key: gameserver.idgel_dome.time (default "0 0 23 ? * *")</summary>
    public static CronExpression[] IDGEL_DOME_TIMES;

    /// <summary>Key: gameserver.autogroup.announce_battleground_registrations (default false)</summary>
    public static bool ANNOUNCE_BATTLEGROUND_REGISTRATIONS = false;
}
