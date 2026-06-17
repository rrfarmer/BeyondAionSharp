using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/ThreadConfig (lord_rex, Neon). [Property] keys/defaults bound at boot by
/// <see cref="ConfigurableProcessor"/> from config/main/thread.properties.
/// </summary>
public static class ThreadConfig
{
    /// <summary>Property key: gameserver.thread.base_pool_size</summary>
    [Property(key: "gameserver.thread.base_pool_size", defaultValue: "0")]
    public static int BASE_THREAD_POOL_SIZE;

    /// <summary>Property key: gameserver.thread.scheduled_pool_size</summary>
    [Property(key: "gameserver.thread.scheduled_pool_size", defaultValue: "0")]
    public static int SCHEDULED_THREAD_POOL_SIZE;

    /// <summary>Property key: gameserver.thread.runtime</summary>
    [Property(key: "gameserver.thread.runtime", defaultValue: "5000")]
    public static long MAXIMUM_RUNTIME_IN_MILLISEC_WITHOUT_WARNING = 5000;

    /// <summary>Property key: gameserver.thread.usepriority</summary>
    [Property(key: "gameserver.thread.usepriority", defaultValue: "false")]
    public static bool USE_PRIORITIES;
}
