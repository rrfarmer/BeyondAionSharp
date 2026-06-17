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

    /// <summary>Restore the batch-C holders to their annotated [Property] defaults (empty properties).</summary>
    private static void RestoreBatchCDefaults()
    {
        ConfigurableProcessor.Process(new JavaProperties(), typeof(AIConfig), typeof(CleaningConfig),
            typeof(CraftConfig), typeof(FallDamageConfig), typeof(GeoDataConfig), typeof(GroupConfig),
            typeof(MembershipConfig), typeof(NameConfig), typeof(PeriodicSaveConfig));
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
