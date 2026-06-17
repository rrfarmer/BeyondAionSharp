using System;
using System.Collections.Generic;
using System.Linq;
using Aion.Commons.Configs;
using Aion.Commons.Configuration;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Network;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Configs;

/// <summary>
/// Java parity: configs/Config (Nemesiss, SoulKeeper). Aggregate config loader: loadProperties() merges
/// config/{administration,main,network}/* defaults with config/mygs.properties (and active-event overrides), then
/// ConfigurableProcessor.process() reflects them onto the static config-holder classes; load() with no args
/// processes all CONFIGS, otherwise only the allowed subset (validated against CONFIGS).
/// <para>
/// Migration state (Full-Parity-Backlog §C): the [Property]-migrated holders are processed for real against the
/// loaded .properties (operator overrides live). Holders not yet [Property]-migrated keep their field-initializer
/// defaults — they remain in <see cref="UnmigratedConfigs"/> so the no-arg load() still validates allowed configs
/// against the full Java CONFIGS list, but ConfigurableProcessor only reflects over the migrated set. The Java
/// loadProperties() unknown-key warning, the logback-xml key filtering, and the CLIENT_CONNECT_ADDRESS local-IP
/// discovery remain deferred infra.
/// </para>
/// </summary>
public static class Config
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(Config));

    /// <summary>
    /// Java parity: GameServer.main registers the CronExpressionTransformer into PropertyTransformers before any
    /// config is processed (it lives in game-server because it depends on the Quartz CronExpression type). The C#
    /// port registers it from this static ctor so every Config.Load/LoadFrom — and the bootstrap path — binds the
    /// CronExpression(/[]) holder fields (AutoGroup/Custom/Shutdown/Siege/Ranking/Housing) for real.
    /// </summary>
    static Config()
    {
        Aion.GameServer.Services.Cron.CronExpressionTransformer.EnsureRegistered();
    }

    /// <summary>
    /// Java parity: Config.CONFIGS — the full allowed-config list (declaration order). Used to validate allowedConfigs
    /// passed to <see cref="Load"/>, exactly as Java does. ConfigurableProcessor only reflects over the
    /// <see cref="MigratedConfigs"/> intersection; the others keep their field-initializer defaults until migrated.
    /// </summary>
    private static readonly IReadOnlyList<Type> Configs = new[]
    {
        typeof(AdminConfig), typeof(CommandsConfig), typeof(AIConfig), typeof(AutoGroupConfig), typeof(CommonsConfig),
        typeof(CleaningConfig), typeof(CraftConfig), typeof(CustomConfig), typeof(DropConfig), typeof(EventsConfig),
        typeof(FallDamageConfig), typeof(GSConfig), typeof(GeoDataConfig), typeof(GroupConfig), typeof(HousingConfig),
        typeof(HTMLConfig), typeof(InGameShopConfig), typeof(InstanceConfig), typeof(LegionConfig), typeof(LoggingConfig),
        typeof(MembershipConfig), typeof(NameConfig), typeof(PeriodicSaveConfig), typeof(PlayerTransferConfig),
        typeof(PricesConfig), typeof(PunishmentConfig), typeof(RankingConfig), typeof(RatesConfig), typeof(SecurityConfig),
        typeof(ShutdownConfig), typeof(SiegeConfig), typeof(ThreadConfig), typeof(WorldConfig),
        typeof(NetworkConfig), typeof(PffConfig),
    };

    /// <summary>
    /// Java parity: the [Property]-migrated subset of CONFIGS, in Java CONFIGS declaration order. ConfigurableProcessor
    /// reflects over exactly these against the loaded properties; the rest keep field-initializer defaults.
    /// </summary>
    private static readonly IReadOnlyList<Type> MigratedConfigs = new[]
    {
        typeof(AdminConfig),
        typeof(CommandsConfig),
        typeof(AIConfig),
        typeof(AutoGroupConfig),
        typeof(CommonsConfig),
        typeof(CleaningConfig),
        typeof(CraftConfig),
        typeof(CustomConfig),
        typeof(DropConfig),
        typeof(EventsConfig),
        typeof(FallDamageConfig),
        typeof(GSConfig),
        typeof(GeoDataConfig),
        typeof(GroupConfig),
        typeof(HousingConfig),
        typeof(HTMLConfig),
        typeof(InGameShopConfig),
        typeof(InstanceConfig),
        typeof(LegionConfig),
        typeof(LoggingConfig),
        typeof(MembershipConfig),
        typeof(NameConfig),
        typeof(PeriodicSaveConfig),
        typeof(PlayerTransferConfig),
        typeof(PricesConfig),
        typeof(PunishmentConfig),
        typeof(RankingConfig),
        typeof(RatesConfig),
        typeof(SecurityConfig),
        typeof(ShutdownConfig),
        typeof(SiegeConfig),
        typeof(ThreadConfig),
        typeof(WorldConfig),
        typeof(NetworkConfig),
        typeof(PffConfig),
    };

    /// <summary>Java parity: loadProperties default folders (relative to the game-server working directory).</summary>
    private static readonly IReadOnlyList<string> DefaultsFolders = new[]
    {
        "./config/administration",
        "./config/main",
        "./config/network",
    };

    /// <summary>Load configs of the given classes or all (the migrated set) if allowedConfigs is empty.</summary>
    public static void Load(params Type[] allowedConfigs)
    {
        JavaProperties properties = LoadProperties();

        // Java parity (Config.load line 49): properties.putAll(EventService.getInstance().getActiveEventConfigProperties()).
        // Active events' inline <config_properties> override the loaded base config (operator/.properties values), with
        // later-starting events winning (EventService sorts nullsFirst by startDate and putAll's in order).
        properties.PutAll(Aion.GameServer.Services.Event.EventService.GetInstance().GetActiveEventConfigProperties());

        // Java parity: validate each requested config is an allowed config (against the full CONFIGS list).
        foreach (var config in allowedConfigs)
        {
            if (!Configs.Contains(config))
                throw new ArgumentException(config + " is not an allowed config");
        }

        bool processAllConfigs = allowedConfigs.Length == 0;
        // Only reflect over the [Property]-migrated holders; unmigrated requested holders keep initializer defaults.
        IEnumerable<Type> requested = processAllConfigs ? MigratedConfigs : allowedConfigs;
        object[] targets = requested.Where(MigratedConfigs.Contains).Cast<object>().ToArray();
        if (targets.Length == 0)
            return;
        ISet<string> unusedProperties = ConfigurableProcessor.Process(properties, targets);
        _ = unusedProperties; // Java logs unknown keys here; deferred (most keys belong to unmigrated holders).
    }

    /// <summary>Java parity: loadProperties() — cascade config/{administration,main,network}/* then mygs.properties.</summary>
    private static JavaProperties LoadProperties()
    {
        var defaults = new JavaProperties();
        foreach (var configDir in DefaultsFolders)
        {
            log.LogInformation("Loading default configuration values from: {ConfigDir}/*", configDir);
            defaults.LoadFromDirectory(configDir, false);
        }
        log.LogInformation("Loading: ./config/mygs.properties");
        JavaProperties properties = JavaProperties.Load("./config/mygs.properties", defaults);
        if (properties.Count == 0)
            log.LogInformation("No override properties found");
        return properties;
    }

    /// <summary>Java parity: Config.load(properties, classes) — process explicit properties (test/host entrypoint).</summary>
    public static ISet<string> LoadFrom(JavaProperties properties, params Type[] allowedConfigs)
    {
        bool processAllConfigs = allowedConfigs.Length == 0;
        object[] targets = (processAllConfigs ? MigratedConfigs : allowedConfigs).Cast<object>().ToArray();
        return ConfigurableProcessor.Process(properties, targets);
    }
}
