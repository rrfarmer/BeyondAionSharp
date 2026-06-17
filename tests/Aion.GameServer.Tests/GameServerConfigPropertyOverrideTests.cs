using System.IO;
using Aion.Commons.Configuration;
using Aion.GameServer.Configs;
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
/// does not leak into sibling tests.
/// </summary>
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
