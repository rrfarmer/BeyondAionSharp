namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/ThreadConfig. @Property defaults baked as initializers.</summary>
public static class ThreadConfig
{
    /// <summary>Property key: gameserver.thread.base_pool_size</summary>
    public static int BASE_THREAD_POOL_SIZE = 0;

    /// <summary>Property key: gameserver.thread.scheduled_pool_size</summary>
    public static int SCHEDULED_THREAD_POOL_SIZE = 0;

    /// <summary>Property key: gameserver.thread.runtime</summary>
    public static long MAXIMUM_RUNTIME_IN_MILLISEC_WITHOUT_WARNING = 5000;

    /// <summary>Property key: gameserver.thread.usepriority</summary>
    public static bool USE_PRIORITIES = false;
}
