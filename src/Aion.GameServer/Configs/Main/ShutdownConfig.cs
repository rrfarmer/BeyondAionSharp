using Aion.Commons.Configuration;
using Quartz;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/ShutdownConfig (lord_rex). @Property defaults as field initializers; CronExpression (Quartz) populated by the config loader via the CronExpressionTransformer.</summary>
public static class ShutdownConfig
{
    /// <summary>Shutdown Hook delay in seconds. Key: gameserver.shutdown.delay (default 120)</summary>
    [Property(key: "gameserver.shutdown.delay", defaultValue: "120")]
    public static int DELAY = 120;

    /// <summary>Shutdown restart schedule. Key: gameserver.shutdown.restart_schedule (no default — null until configured)</summary>
    [Property(key: "gameserver.shutdown.restart_schedule")]
    public static CronExpression RESTART_SCHEDULE;
}
