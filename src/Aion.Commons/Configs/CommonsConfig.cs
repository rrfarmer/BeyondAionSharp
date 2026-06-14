namespace Aion.Commons.Configs;

/// <summary>
/// Java parity: commons/configs/CommonsConfig. Only the fields consumed by the port are declared here; values are
/// populated by the config loader (default false until then), matching the Java @Property-annotated static fields.
/// </summary>
public static class CommonsConfig
{
    /// <summary>Java parity: CommonsConfig.RUNNABLESTATS_ENABLE — enables per-runnable execution-time profiling.</summary>
    public static bool RUNNABLESTATS_ENABLE;
}
