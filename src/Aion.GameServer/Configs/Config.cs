using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Configs;

/// <summary>
/// Java parity: configs/Config (Nemesiss, SoulKeeper). Aggregate config loader: loadProperties() merges
/// config/{administration,main,network}/* defaults with config/mygs.properties and active-event overrides,
/// then ConfigurableProcessor.process() reflects them onto the static config-holder classes; load() with no
/// args processes all CONFIGS, otherwise only the allowed subset (validated against CONFIGS).
/// <para>
/// Infra per [[gameplay-faithful-infra-idiomatic]]: the runtime properties->reflection wiring
/// (ConfigurableProcessor/PropertiesUtils, client-IP discovery) is deferred — consistent with the
/// deferred-runtime data-load convention. The faithful public surface (Load(params Type[])) is preserved so
/// gameplay consumers (Event.Start/Stop, ChatProcessor.Reload) compile and call through unchanged; the
/// reflective property load is wired in a later infra pass.
/// </para>
/// </summary>
public static class Config
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(Config));

    /// <summary>Load configs of the given classes or all if allowedConfigs is empty.</summary>
    public static void Load(params Type[] allowedConfigs)
    {
        // TODO (infra): wire ConfigurableProcessor reflection over loadProperties() + active-event overrides.
        // Deferred-runtime: the static config-holder classes currently carry their declared defaults.
    }
}
