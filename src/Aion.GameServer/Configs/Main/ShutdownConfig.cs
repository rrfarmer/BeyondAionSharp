using Quartz;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/ShutdownConfig (lord_rex). @Property defaults as field initializers; CronExpression (Quartz) populated by config loader.</summary>
public static class ShutdownConfig
{
    /// <summary>Shutdown Hook delay in seconds. Key: gameserver.shutdown.delay (default 120)</summary>
    public static int DELAY = 120;

    /// <summary>Shutdown restart schedule. Key: gameserver.shutdown.restart_schedule</summary>
    public static CronExpression RESTART_SCHEDULE;
}
