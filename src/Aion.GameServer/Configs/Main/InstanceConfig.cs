using System.Collections.Generic;
using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/InstanceConfig. [Property] keys/defaults bound at boot. Set&lt;Integer&gt;→ISet&lt;int&gt;
/// (CollectionTransformer); File HANDLER_DIRECTORY→string path bound by StringTransformer (established convention,
/// matching AIConfig/GSConfig/WorldConfig File handler-directory fields; consumers take string/new FileInfo(string)).
/// </summary>
public static class InstanceConfig
{
    /// <summary>Property key: gameserver.instance.cooldown_rate</summary>
    [Property(key: "gameserver.instance.cooldown_rate", defaultValue: "1")]
    public static int INSTANCE_COOLDOWN_RATE = 1;

    /// <summary>Property key: gameserver.instance.cooldown_rate.excluded_maps</summary>
    [Property(key: "gameserver.instance.cooldown_rate.excluded_maps", defaultValue: "")]
    public static ISet<int> INSTANCE_COOLDOWN_RATE_EXCLUDED_MAPS = new HashSet<int>();

    /// <summary>Property key: gameserver.instance.destroy_delay_seconds</summary>
    [Property(key: "gameserver.instance.destroy_delay_seconds", defaultValue: "600")]
    public static int INSTANCE_DESTROY_DELAY_SECONDS = 600;

    /// <summary>Property key: gameserver.instance.solo.destroy_delay_seconds</summary>
    [Property(key: "gameserver.instance.solo.destroy_delay_seconds", defaultValue: "600")]
    public static int SOLO_INSTANCE_DESTROY_DELAY_SECONDS = 600;

    /// <summary>Property key: gameserver.instance.duel.enable</summary>
    [Property(key: "gameserver.instance.duel.enable", defaultValue: "true")]
    public static bool INSTANCE_DUEL_ENABLE = true;

    /// <summary>Location of instance *.java handlers. Property key: gameserver.instance.handler_directory (Java type: File).</summary>
    [Property(key: "gameserver.instance.handler_directory", defaultValue: "./data/handlers/instance")]
    public static string HANDLER_DIRECTORY = "./data/handlers/instance";
}
