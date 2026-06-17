using Aion.Commons.Configuration;

namespace Aion.Commons.Configs;

/// <summary>
/// Java parity: commons/configs/CommonsConfig (ATracer). [Property]-annotated static fields bound at boot by
/// <see cref="ConfigurableProcessor"/> from the loaded .properties (operator overrides honored); the field
/// initializer is only the pre-process value and is always overwritten because both keys carry an explicit
/// default value.
/// </summary>
public static class CommonsConfig
{
    /// <summary>Java parity: CommonsConfig.RUNNABLESTATS_ENABLE — enables per-runnable execution-time profiling.</summary>
    [Property(key: "commons.runnablestats.enable", defaultValue: "false")]
    public static bool RUNNABLESTATS_ENABLE;

    /// <summary>Java parity: CommonsConfig.SCRIPT_COMPILER_CACHING — enables script-compiler output caching.</summary>
    [Property(key: "commons.script_compiler.caching.enable", defaultValue: "true")]
    public static volatile bool SCRIPT_COMPILER_CACHING;
}
