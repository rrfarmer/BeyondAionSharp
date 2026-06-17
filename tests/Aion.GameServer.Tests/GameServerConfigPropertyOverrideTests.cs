using System.Collections.Generic;
using System.IO;
using Aion.Commons.Configuration;
using Aion.GameServer.Configs;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;

namespace Aion.GameServer.Tests;

/// <summary>
/// Full-Parity §C proof: the game-server config holders migrated to [Property] now honor real config/*.properties
/// overrides at boot (the hard-contract gap previously TODO'd in Config.cs). Loads the ACTUAL checked-in
/// game-server/config files and asserts an overridden key (gameserver.character.reentry.time = 10 in
/// gameserver.properties, vs the [Property] default 20) actually changes the static field — and that the
/// ConfigurableProcessor binds GSConfig.TIME_ZONE_ID through the new ZoneIdTransformer.
///
/// Static config holders are process-global; this test restores them to their annotated defaults on exit so it
/// does not leak into sibling tests. It joins the GoldenDataManager (non-parallel) collection because
/// GameServerBootstrapTests calls Config.Load() — which now also binds these shared static holders — and the two
/// must not race over the global config state.
/// </summary>
[Xunit.Collection("GoldenDataManager")]
public sealed class GameServerConfigPropertyOverrideTests
{
    [Fact]
    public void RealPropertiesFile_OverridesMigratedDefault()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return; // data-less checkout: the real config tree is not present.
        var configMain = Path.Combine(repoRoot, "game-server", "config", "main");
        var gameserverProps = Path.Combine(configMain, "gameserver.properties");
        if (!File.Exists(gameserverProps))
            return;

        try
        {
            // Baseline: annotated defaults only (empty properties) -> CHARACTER_REENTRY_TIME default is 20.
            ConfigurableProcessor.Process(new JavaProperties(), typeof(GSConfig));
            Assert.Equal(20, GSConfig.CHARACTER_REENTRY_TIME);

            // Real defaults cascade (config/main/*) then process GSConfig: gameserver.properties overrides
            // gameserver.character.reentry.time to 10.
            var props = new JavaProperties();
            props.LoadFromDirectory(configMain, false);
            ConfigurableProcessor.Process(props, typeof(GSConfig));

            // Sanity: the raw property is actually 10 in the checked-in file.
            Assert.Equal("10", props.GetProperty("gameserver.character.reentry.time"));
            // Proof: the override flowed into the static field (10), not the [Property] default (20).
            Assert.Equal(10, GSConfig.CHARACTER_REENTRY_TIME);
            Assert.NotEqual(20, GSConfig.CHARACTER_REENTRY_TIME);

            // ZoneIdTransformer parity: gameserver.timezone is present-but-empty -> system default time zone.
            Assert.Equal(TimeZoneInfo.Local, GSConfig.TIME_ZONE_ID);
        }
        finally
        {
            // Restore annotated defaults so the global static holder doesn't leak into other tests.
            ConfigurableProcessor.Process(new JavaProperties(), typeof(GSConfig));
        }
    }

    [Fact]
    public void ConfigLoadFrom_ProcessesOnlyMigratedHolders_AndOverridesRates()
    {
        try
        {
            // Override a RatesConfig float[] via the Config.LoadFrom entrypoint (mirrors Java Config.load over props).
            var props = new JavaProperties();
            props.SetProperty("gameserver.rates.xp.solo", "3.0, 5.0, 7.0");
            Config.LoadFrom(props, typeof(RatesConfig));

            Assert.Equal(new[] { 3.0f, 5.0f, 7.0f }, RatesConfig.XP_SOLO_RATES);
            // Unset rate keeps its annotated default (1.0, 2.0).
            Assert.Equal(new[] { 1.0f, 2.0f }, RatesConfig.XP_GROUP_RATES);
        }
        finally
        {
            ConfigurableProcessor.Process(new JavaProperties(), typeof(RatesConfig));
        }
    }

    [Fact]
    public void RealPropertiesFile_OverridesNewlyMigratedHolders()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return; // data-less checkout: the real config tree is not present.
        var configMain = Path.Combine(repoRoot, "game-server", "config", "main");
        if (!File.Exists(Path.Combine(configMain, "ai.properties")))
            return;

        try
        {
            var props = new JavaProperties();
            props.LoadFromDirectory(configMain, false);

            // Process the batch-C migrated holders against the real cascaded config/main/*.properties.
            ConfigurableProcessor.Process(props, typeof(AIConfig), typeof(CleaningConfig), typeof(CraftConfig),
                typeof(FallDamageConfig), typeof(GeoDataConfig), typeof(GroupConfig), typeof(MembershipConfig),
                typeof(NameConfig), typeof(PeriodicSaveConfig));

            // Real file values flow into the static fields (these mirror the shipped config/main/*.properties).
            Assert.Equal("./data/handlers/ai", AIConfig.HANDLER_DIRECTORY);
            Assert.Equal(365, CleaningConfig.MIN_ACCOUNT_INACTIVITY_DAYS);
            Assert.Equal(1.0f, FallDamageConfig.FALL_DAMAGE_PERCENTAGE);
            Assert.True(GeoDataConfig.GEO_ENABLE);
            Assert.Equal(new[] { "Premium" }, MembershipConfig.MEMBERSHIP_TYPES);
            Assert.NotNull(NameConfig.CHAR_NAME_PATTERN);
            Assert.Equal(900, PeriodicSaveConfig.PLAYER_GENERAL);
        }
        finally
        {
            RestoreBatchCDefaults();
        }
    }

    [Fact]
    public void ExplicitOverride_DiffersFromAnnotatedDefault_AcrossBatchCHolders()
    {
        try
        {
            // Baseline: empty properties -> annotated [Property] defaults.
            RestoreBatchCDefaults();
            Assert.False(AIConfig.SHOUTS_ENABLE);                  // default false
            Assert.Equal(365, CleaningConfig.MIN_ACCOUNT_INACTIVITY_DAYS); // default 365
            Assert.Equal(33, CraftConfig.MAX_CRAFT_FAILURE_CHANCE); // default 33
            Assert.Equal(600, GroupConfig.GROUP_REMOVE_TIME);       // default 600
            Assert.Equal((byte)8, MembershipConfig.CHARACTER_ADDITIONAL_COUNT); // default 8
            Assert.Equal(30, NameConfig.RESERVE_OLD_NAME_DAYS);     // default 30

            // Override every key to a value that differs from the [Property] default and prove it flows through.
            var props = new JavaProperties();
            props.SetProperty("gameserver.npcshouts.enable", "true");
            props.SetProperty("gameserver.cleaning.min_account_inactivity", "180");
            props.SetProperty("gameserver.craft.fail.chance", "50");
            props.SetProperty("gameserver.playergroup.removetime", "999");
            props.SetProperty("gameserver.character.additional.count", "16");
            props.SetProperty("gameserver.name.reserve_old_name_days", "7");
            props.SetProperty("gameserver.name.forbidden_words", "foo, bar, baz");
            props.SetProperty("gameserver.membership.types", "Premium, Gold, Silver");

            ConfigurableProcessor.Process(props, typeof(AIConfig), typeof(CleaningConfig), typeof(CraftConfig),
                typeof(GroupConfig), typeof(MembershipConfig), typeof(NameConfig));

            Assert.True(AIConfig.SHOUTS_ENABLE);
            Assert.Equal(180, CleaningConfig.MIN_ACCOUNT_INACTIVITY_DAYS);
            Assert.Equal(50, CraftConfig.MAX_CRAFT_FAILURE_CHANCE);
            Assert.Equal(999, GroupConfig.GROUP_REMOVE_TIME);
            Assert.Equal((byte)16, MembershipConfig.CHARACTER_ADDITIONAL_COUNT);
            Assert.Equal(7, NameConfig.RESERVE_OLD_NAME_DAYS);
            Assert.Equal(new[] { "foo", "bar", "baz" }, NameConfig.FORBIDDEN_WORDS);
            Assert.Equal(new[] { "Premium", "Gold", "Silver" }, MembershipConfig.MEMBERSHIP_TYPES);
        }
        finally
        {
            RestoreBatchCDefaults();
        }
    }

    [Fact]
    public void RealPropertiesFile_BindsCollectionFields_AdminAndSecurity()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return; // data-less checkout: the real config tree is not present.
        var configAdmin = Path.Combine(repoRoot, "game-server", "config", "administration");
        var configMain = Path.Combine(repoRoot, "game-server", "config", "main");
        if (!File.Exists(Path.Combine(configAdmin, "admin.properties"))
            || !File.Exists(Path.Combine(configMain, "security.properties")))
            return;

        try
        {
            var adminProps = new JavaProperties();
            adminProps.LoadFromDirectory(configAdmin, false);
            ConfigurableProcessor.Process(adminProps, typeof(AdminConfig));

            // Collection transformer parity: //invis, //invul, //enemy none, //see is comma-split into a List<string>.
            Assert.Equal(
                new List<string> { "//invis", "//invul", "//enemy none", "//see" },
                AdminConfig.LOGIN_EXECUTE_COMMANDS);
            // Single-element list (no comma): "*".
            Assert.Equal(new List<string> { "*" }, AdminConfig.ANNOUNCE_LEVELS);
            // string[] (Array transformer) — the customtags value has 9 %s entries.
            Assert.NotNull(AdminConfig.NAME_TAGS);
            Assert.Equal(9, AdminConfig.NAME_TAGS.Length);

            var secProps = new JavaProperties();
            secProps.LoadFromDirectory(configMain, false);
            ConfigurableProcessor.Process(secProps, typeof(SecurityConfig));

            // Set transformer parity: the shipped value is empty -> empty HashSet (never null).
            Assert.NotNull(SecurityConfig.MULTI_CLIENTING_IGNORED_MAC_ADDRESSES);
            Assert.Empty(SecurityConfig.MULTI_CLIENTING_IGNORED_MAC_ADDRESSES);
            // Enum transformer parity: restriction mode NONE.
            Assert.Equal(SecurityConfig.MultiClientingRestrictionMode.NONE,
                SecurityConfig.MULTI_CLIENTING_RESTRICTION_MODE);
        }
        finally
        {
            ConfigurableProcessor.Process(new JavaProperties(), typeof(AdminConfig), typeof(SecurityConfig));
        }
    }

    [Fact]
    public void ExplicitOverride_CollectionTransformer_ListAndSet()
    {
        try
        {
            var props = new JavaProperties();
            // List<string> override with quoted token containing a comma (CSV quote semantics).
            props.SetProperty("gameserver.administration.login.execute_commands", "//see, \"//enemy a,b\", //invis");
            // Set<string> override that de-duplicates (HashSet semantics like Java HashSet).
            props.SetProperty("gameserver.security.multi_clienting.ignored_mac_addresses", "AA, BB, AA, CC");

            ConfigurableProcessor.Process(props, typeof(AdminConfig), typeof(SecurityConfig));

            Assert.Equal(
                new List<string> { "//see", "//enemy a,b", "//invis" },
                AdminConfig.LOGIN_EXECUTE_COMMANDS);
            Assert.Equal(
                new HashSet<string> { "AA", "BB", "CC" },
                SecurityConfig.MULTI_CLIENTING_IGNORED_MAC_ADDRESSES);
            Assert.Equal(3, SecurityConfig.MULTI_CLIENTING_IGNORED_MAC_ADDRESSES.Count);
        }
        finally
        {
            ConfigurableProcessor.Process(new JavaProperties(), typeof(AdminConfig), typeof(SecurityConfig));
        }
    }

    [Fact]
    public void ExplicitOverride_DiffersFromAnnotatedDefault_AcrossBatchC2Holders()
    {
        try
        {
            // Baseline: empty properties -> annotated [Property] defaults for the batch-C2 holders.
            RestoreBatchC2Defaults();
            Assert.False(HTMLConfig.ENABLE_HTML_WELCOME);          // default false
            Assert.Equal("UTF-8", HTMLConfig.HTML_ENCODING);       // default UTF-8
            Assert.False(InGameShopConfig.ENABLE_IN_GAME_SHOP);    // default false
            Assert.True(InGameShopConfig.ALLOW_GIFTS);             // default true
            Assert.Equal(1, InstanceConfig.INSTANCE_COOLDOWN_RATE); // default 1
            Assert.Empty(InstanceConfig.INSTANCE_COOLDOWN_RATE_EXCLUDED_MAPS); // default empty set
            Assert.Equal(86400, LegionConfig.LEGION_DISBAND_TIME); // default 86400
            Assert.True(LegionConfig.LEGION_WAREHOUSE);            // default true
            Assert.True(LoggingConfig.LOG_AUDIT);                  // default true
            Assert.False(LoggingConfig.LOG_KILL);                 // default false
            Assert.Equal(0L, PlayerTransferConfig.MAX_KINAH);     // default 0
            Assert.Equal("*", PlayerTransferConfig.REMOVE_SKILL_LIST); // default *
            Assert.Equal(100, PricesConfig.DEFAULT_PRICES);       // default 100
            Assert.Equal(20, PricesConfig.VENDOR_SELL_MODIFIER);  // default 20
            Assert.False(PunishmentConfig.PUNISHMENT_ENABLE);     // default false
            Assert.Equal(1440, PunishmentConfig.PUNISHMENT_TIME); // default 1440

            // Override every probed key to a value that differs from the [Property] default; prove flow-through.
            var props = new JavaProperties();
            props.SetProperty("gameserver.html.welcome.enable", "true");
            props.SetProperty("gameserver.html.encoding", "ISO-8859-1");
            props.SetProperty("gameserver.ingameshop.enable", "true");
            props.SetProperty("gameserver.ingameshop.allow.gift", "false");
            props.SetProperty("gameserver.instance.cooldown_rate", "3");
            props.SetProperty("gameserver.instance.cooldown_rate.excluded_maps", "300080000, 300090000, 300060000");
            props.SetProperty("gameserver.legion.disbandtime", "172800");
            props.SetProperty("gameserver.legion.warehouse", "false");
            props.SetProperty("gameserver.log.audit", "false");
            props.SetProperty("gameserver.log.kill", "true");
            props.SetProperty("ptransfer.max.kinah", "5000000000"); // > int range: proves long binding
            props.SetProperty("ptransfer.remove.skills.list", "1500, 1501");
            props.SetProperty("gameserver.prices.default.prices", "150");
            props.SetProperty("gameserver.prices.vendor.sellmod", "35");
            props.SetProperty("gameserver.punishment.enable", "true");
            props.SetProperty("gameserver.punishment.time", "60");

            ConfigurableProcessor.Process(props, typeof(HTMLConfig), typeof(InGameShopConfig),
                typeof(InstanceConfig), typeof(LegionConfig), typeof(LoggingConfig),
                typeof(PlayerTransferConfig), typeof(PricesConfig), typeof(PunishmentConfig));

            Assert.True(HTMLConfig.ENABLE_HTML_WELCOME);
            Assert.Equal("ISO-8859-1", HTMLConfig.HTML_ENCODING);
            Assert.True(InGameShopConfig.ENABLE_IN_GAME_SHOP);
            Assert.False(InGameShopConfig.ALLOW_GIFTS);
            Assert.Equal(3, InstanceConfig.INSTANCE_COOLDOWN_RATE);
            Assert.Equal(new HashSet<int> { 300080000, 300090000, 300060000 },
                InstanceConfig.INSTANCE_COOLDOWN_RATE_EXCLUDED_MAPS);
            Assert.Equal(172800, LegionConfig.LEGION_DISBAND_TIME);
            Assert.False(LegionConfig.LEGION_WAREHOUSE);
            Assert.False(LoggingConfig.LOG_AUDIT);
            Assert.True(LoggingConfig.LOG_KILL);
            Assert.Equal(5000000000L, PlayerTransferConfig.MAX_KINAH);
            Assert.Equal("1500, 1501", PlayerTransferConfig.REMOVE_SKILL_LIST);
            Assert.Equal(150, PricesConfig.DEFAULT_PRICES);
            Assert.Equal(35, PricesConfig.VENDOR_SELL_MODIFIER);
            Assert.True(PunishmentConfig.PUNISHMENT_ENABLE);
            Assert.Equal(60, PunishmentConfig.PUNISHMENT_TIME);
        }
        finally
        {
            RestoreBatchC2Defaults();
        }
    }

    [Fact]
    public void RealPropertiesFile_BindsNewlyMigratedHolders_BatchC2()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return; // data-less checkout: the real config tree is not present.
        var configMain = Path.Combine(repoRoot, "game-server", "config", "main");
        if (!File.Exists(Path.Combine(configMain, "legions.properties")))
            return;

        try
        {
            var props = new JavaProperties();
            props.LoadFromDirectory(configMain, false);

            ConfigurableProcessor.Process(props, typeof(HTMLConfig), typeof(InGameShopConfig),
                typeof(InstanceConfig), typeof(LegionConfig), typeof(PricesConfig), typeof(PunishmentConfig));

            // Pattern transformer parity: the shipped legion name pattern compiles into a Regex.
            Assert.NotNull(LegionConfig.LEGION_NAME_PATTERN);
            // Real cascaded values flow into the static fields (mirror the shipped config/main/*.properties).
            Assert.Equal("./data/static_data/HTML/", HTMLConfig.HTML_ROOT);
            Assert.Equal(86400, LegionConfig.LEGION_DISBAND_TIME);
            Assert.True(LegionConfig.LEGION_WAREHOUSE);
            Assert.Equal(60, LegionConfig.LEGION_LEVEL2_MAX_MEMBERS);
            Assert.Equal(100, PricesConfig.DEFAULT_PRICES);
            Assert.Equal(1, PunishmentConfig.PUNISHMENT_TYPE);
            // Set transformer parity: shipped excluded_maps is empty -> empty HashSet (never null).
            Assert.NotNull(InstanceConfig.INSTANCE_COOLDOWN_RATE_EXCLUDED_MAPS);
            Assert.Empty(InstanceConfig.INSTANCE_COOLDOWN_RATE_EXCLUDED_MAPS);
        }
        finally
        {
            RestoreBatchC2Defaults();
        }
    }

    /// <summary>Restore the batch-C2 holders to their annotated [Property] defaults (empty properties).</summary>
    private static void RestoreBatchC2Defaults()
    {
        ConfigurableProcessor.Process(new JavaProperties(), typeof(HTMLConfig), typeof(InGameShopConfig),
            typeof(InstanceConfig), typeof(LegionConfig), typeof(LoggingConfig), typeof(PlayerTransferConfig),
            typeof(PricesConfig), typeof(PunishmentConfig));
    }

    /// <summary>Restore the batch-C holders to their annotated [Property] defaults (empty properties).</summary>
    private static void RestoreBatchCDefaults()
    {
        ConfigurableProcessor.Process(new JavaProperties(), typeof(AIConfig), typeof(CleaningConfig),
            typeof(CraftConfig), typeof(FallDamageConfig), typeof(GeoDataConfig), typeof(GroupConfig),
            typeof(MembershipConfig), typeof(NameConfig), typeof(PeriodicSaveConfig));
    }

    [Fact]
    public void RealPropertiesFile_BindsNewlyMigratedHolders_BatchC3_IncludingCron()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return; // data-less checkout: the real config tree is not present.
        var configMain = Path.Combine(repoRoot, "game-server", "config", "main");
        if (!File.Exists(Path.Combine(configMain, "custom.properties")))
            return;

        // The Cron transformer lives in game-server (it depends on Quartz CronExpression). Config's static ctor
        // registers it; touch Config so the transformer is registered before processing CronExpression holders.
        Services.Cron.CronExpressionTransformer.EnsureRegistered();

        try
        {
            var props = new JavaProperties();
            props.LoadFromDirectory(configMain, false);

            ConfigurableProcessor.Process(props, typeof(AutoGroupConfig), typeof(CustomConfig),
                typeof(DropConfig), typeof(EventsConfig), typeof(SiegeConfig), typeof(ShutdownConfig));

            // Scalar overrides flow from the real shipped files.
            Assert.Equal(2, CustomConfig.VORTEX_DURATION);                // custom.properties = 2 (vs [Property] default 1)
            Assert.True(SiegeConfig.BALAUR_AUTO_ASSAULT);                 // siege.properties = true (vs default false)
            // Enum (nullable) override: drop.properties announce_quality = MYTHIC.
            Assert.Equal(Model.Templates.Items.ItemQuality.MYTHIC, DropConfig.MIN_ANNOUNCE_QUALITY);
            // Set<int> present-but-empty -> empty HashSet (never null).
            Assert.NotNull(DropConfig.DISABLE_RANGE_CHECK_MAPS);
            Assert.Empty(DropConfig.DISABLE_RANGE_CHECK_MAPS);
            Assert.Empty(EventsConfig.DISABLED_EVENTS);

            // Cron transformer parity (REAL Quartz CronExpression): the shipped vortex schedules compile.
            Assert.NotNull(CustomConfig.VORTEX_BRUSTHONIN_SCHEDULE);
            Assert.Equal("0 0 16 ? * SAT", CustomConfig.VORTEX_BRUSTHONIN_SCHEDULE.CronExpressionString);
            Assert.NotNull(SiegeConfig.MOLTENUS_SPAWN_SCHEDULE);
            Assert.Equal("0 0 22 ? * SUN", SiegeConfig.MOLTENUS_SPAWN_SCHEDULE.CronExpressionString);
            // CronExpression[] (ArrayTransformer -> per-element CronExpressionTransformer): quoted single-cron default.
            Assert.Single(AutoGroupConfig.DREDGION_TIMES);
            Assert.Equal("0 0 0,12,20 ? * *", AutoGroupConfig.DREDGION_TIMES[0].CronExpressionString);
            // shutdown.properties restart_schedule is present-but-empty -> null (Java: value.isEmpty() ? null).
            Assert.Null(ShutdownConfig.RESTART_SCHEDULE);
        }
        finally
        {
            RestoreBatchC3Defaults();
        }
    }

    [Fact]
    public void ExplicitOverride_BindsCron_AndScalars_BatchC3()
    {
        Services.Cron.CronExpressionTransformer.EnsureRegistered();
        try
        {
            var props = new JavaProperties();
            // Override a Cron field to a value that differs from the [Property] default and prove it flows through.
            props.SetProperty("gameserver.shutdown.restart_schedule", "0 0 6 ? * *");
            props.SetProperty("gameserver.vortex.duration", "9");
            props.SetProperty("gameserver.cp.worlds", "111, 222, 333");
            props.SetProperty("gameserver.drop.announce_quality", "LEGEND");

            ConfigurableProcessor.Process(props, typeof(CustomConfig), typeof(DropConfig), typeof(ShutdownConfig));

            Assert.NotNull(ShutdownConfig.RESTART_SCHEDULE);
            Assert.Equal("0 0 6 ? * *", ShutdownConfig.RESTART_SCHEDULE.CronExpressionString);
            Assert.Equal(9, CustomConfig.VORTEX_DURATION);
            Assert.Equal(new HashSet<int> { 111, 222, 333 }, CustomConfig.CONQUEROR_AND_PROTECTOR_WORLDS);
            Assert.Equal(Model.Templates.Items.ItemQuality.LEGEND, DropConfig.MIN_ANNOUNCE_QUALITY);
        }
        finally
        {
            RestoreBatchC3Defaults();
        }
    }

    /// <summary>Restore the batch-C3 holders to their annotated [Property] defaults (empty properties).</summary>
    private static void RestoreBatchC3Defaults()
    {
        Services.Cron.CronExpressionTransformer.EnsureRegistered();
        ConfigurableProcessor.Process(new JavaProperties(), typeof(AutoGroupConfig), typeof(CustomConfig),
            typeof(DropConfig), typeof(EventsConfig), typeof(SiegeConfig), typeof(ShutdownConfig));
    }

    [Fact]
    public void ActiveEventConfigProperties_OverrideBaseConfig_AtProcessTime()
    {
        // Full-Parity §C proof of the active-event override merge (Java Config.load line 49:
        // properties.putAll(EventService.getInstance().getActiveEventConfigProperties())). An active event's inline
        // <config_properties><property>key=value</property> overrides the loaded base config when the holder is
        // processed. We mirror the exact merge shape: base props -> PutAll(event props) -> ConfigurableProcessor.Process.
        try
        {
            // Base config sets the key to a non-default value (as a shipped .properties would).
            var properties = new JavaProperties();
            properties.SetProperty("gameserver.character.reentry.time", "10");

            // An active event template carries an inline config_properties override (parsed exactly like Java's
            // EventTemplate.loadConfigProperties via StringReader key=value).
            var eventTemplate = new Model.Templates.Event.EventTemplate
            {
                Name = "TestReentryEvent",
                ConfigProperties = new List<string> { "gameserver.character.reentry.time=42" },
            };
            var eventProps = eventTemplate.LoadConfigProperties();
            Assert.NotNull(eventProps);
            Assert.Equal("42", eventProps["gameserver.character.reentry.time"]);

            // Merge: the event's config properties putAll over the base config (event wins).
            properties.PutAll(eventProps);
            Assert.Equal("42", properties.GetProperty("gameserver.character.reentry.time"));

            // Process: the merged value flows into the static holder, proving the override took effect.
            ConfigurableProcessor.Process(properties, typeof(GSConfig));
            Assert.Equal(42, GSConfig.CHARACTER_REENTRY_TIME);
            Assert.NotEqual(10, GSConfig.CHARACTER_REENTRY_TIME);
            Assert.NotEqual(20, GSConfig.CHARACTER_REENTRY_TIME);
        }
        finally
        {
            ConfigurableProcessor.Process(new JavaProperties(), typeof(GSConfig));
        }
    }

    [Fact]
    public void GetActiveEventConfigProperties_EmptyWhenNoActiveEvents_AndLaterEventWins()
    {
        // With no active events, the merge contributes nothing (base config is untouched).
        var fromService = Services.Event.EventService.GetInstance().GetActiveEventConfigProperties();
        Assert.Empty(fromService);

        // Ordering parity: EventService sorts events nullsFirst by startDate and putAll's in order, so a later-starting
        // event's value wins on key collision. Mirror that putAll ordering at the dictionary level.
        var early = new Model.Templates.Event.EventTemplate
        {
            Name = "Early", Start = "2024-01-01T00:00:00",
            ConfigProperties = new List<string> { "gameserver.character.reentry.time=11" },
        };
        var late = new Model.Templates.Event.EventTemplate
        {
            Name = "Late", Start = "2024-12-31T00:00:00",
            ConfigProperties = new List<string> { "gameserver.character.reentry.time=99" },
        };

        var merged = new JavaProperties();
        // Emulate nullsFirst-by-startDate ordering: early then late; late putAll overwrites.
        merged.PutAll(early.LoadConfigProperties()!);
        merged.PutAll(late.LoadConfigProperties()!);
        Assert.Equal("99", merged.GetProperty("gameserver.character.reentry.time"));
    }

    private static string? FindRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "game-server", "config", "main", "gameserver.properties")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return null;
    }
}
