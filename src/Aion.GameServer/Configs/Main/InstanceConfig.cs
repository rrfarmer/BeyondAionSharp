using System.Collections.Generic;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/InstanceConfig. Set&lt;Integer&gt;→ISet&lt;int&gt;; File→string path.</summary>
public static class InstanceConfig
{
    /// <summary>Property key: gameserver.instance.cooldown_rate</summary>
    public static int INSTANCE_COOLDOWN_RATE = 1;

    /// <summary>Property key: gameserver.instance.cooldown_rate.excluded_maps</summary>
    public static ISet<int> INSTANCE_COOLDOWN_RATE_EXCLUDED_MAPS = new HashSet<int>();

    /// <summary>Property key: gameserver.instance.destroy_delay_seconds</summary>
    public static int INSTANCE_DESTROY_DELAY_SECONDS = 600;

    /// <summary>Property key: gameserver.instance.solo.destroy_delay_seconds</summary>
    public static int SOLO_INSTANCE_DESTROY_DELAY_SECONDS = 600;

    /// <summary>Property key: gameserver.instance.duel.enable</summary>
    public static bool INSTANCE_DUEL_ENABLE = true;

    /// <summary>Property key: gameserver.instance.handler_directory (Java type: File).</summary>
    public static string HANDLER_DIRECTORY = "./data/handlers/instance";
}
