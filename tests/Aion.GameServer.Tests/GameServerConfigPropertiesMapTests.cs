using System.Collections.Generic;
using System.IO;
using Aion.Commons.Configuration;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

/// <summary>
/// Full-Parity §C proof for the @Properties (key-pattern → Map) annotation. The last four game-server config
/// holders (CommandsConfig, RankingConfig, HousingConfig, PffConfig) were blocked on this annotation; this test
/// loads the REAL checked-in game-server/config/*.properties and asserts:
///   - CommandsConfig.ACCESS_LEVELS binds the command→access-level map (keyPattern with NO capturing group → whole
///     key is the map key; value Java Byte → C# sbyte).
///   - RankingConfig quota/gp_loss maps bind with an AbyssRankEnum key (capturing group strips the prefix) AND the
///     CronExpression fields bind via the CronExpressionTransformer.
///   - HousingConfig.AUCTION_AUTO_FILL_LIMITS binds a HouseType→int map AND the auction Cron fields bind.
///   - PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE binds a hex-opcode→ms map (NumberTransformer decodes 0x...);
///     the shipped file has the map commented out, so this is asserted via an explicit override.
///
/// Static config holders are process-global; this test restores them to their annotated defaults on exit. It joins
/// the GoldenDataManager (non-parallel) collection so it does not race the bootstrap test over global config state.
/// </summary>
[Xunit.Collection("GoldenDataManager")]
public sealed class GameServerConfigPropertiesMapTests
{
    [Fact]
    public void RealPropertiesFile_BindsCommandsAccessLevelMap()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return; // data-less checkout.
        var configAdmin = Path.Combine(repoRoot, "game-server", "config", "administration");
        if (!File.Exists(Path.Combine(configAdmin, "commands.properties")))
            return;

        try
        {
            var props = new JavaProperties();
            props.LoadFromDirectory(configAdmin, false);
            ConfigurableProcessor.Process(props, typeof(CommandsConfig));

            // @Properties keyPattern ^[a-zA-Z0-9_]+$ — no capturing group → whole key is the map key.
            Assert.NotNull(CommandsConfig.ACCESS_LEVELS);
            Assert.Equal((sbyte)9, CommandsConfig.ACCESS_LEVELS["access"]);
            Assert.Equal((sbyte)1, CommandsConfig.ACCESS_LEVELS["announce"]);
            Assert.Equal((sbyte)0, CommandsConfig.ACCESS_LEVELS["help"]);          // player command, level 0
            Assert.Equal((sbyte)10, CommandsConfig.ACCESS_LEVELS["faction"]);      // player command, level 10
            Assert.Equal((sbyte)9, CommandsConfig.ACCESS_LEVELS["Bookmark_add"]);  // console command (underscore key)
            Assert.DoesNotContain("bk", CommandsConfig.ACCESS_LEVELS.Keys);
            // The dotted handler-directories key does NOT match ^[a-zA-Z0-9_]+$ → excluded from the map.
            Assert.DoesNotContain("gameserver.commands.handler_directories", CommandsConfig.ACCESS_LEVELS.Keys);

            // @Property File[] → FileInfo[] (FileTransformer): the shipped 3-directory value binds.
            Assert.NotNull(CommandsConfig.HANDLER_DIRECTORIES);
            Assert.Equal(3, CommandsConfig.HANDLER_DIRECTORIES.Length);
        }
        finally
        {
            ConfigurableProcessor.Process(new JavaProperties(), typeof(CommandsConfig));
        }
    }

    [Fact]
    public void RealPropertiesFile_BindsRankingEnumMaps_AndCron()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return;
        var configMain = Path.Combine(repoRoot, "game-server", "config", "main");
        if (!File.Exists(Path.Combine(configMain, "ranking.properties")))
            return;

        Services.Cron.CronExpressionTransformer.EnsureRegistered();
        try
        {
            var props = new JavaProperties();
            props.LoadFromDirectory(configMain, false);
            ConfigurableProcessor.Process(props, typeof(RankingConfig));

            // @Properties keyPattern with capturing group → AbyssRankEnum key strips the prefix; int value.
            Assert.NotNull(RankingConfig.TOP_RANKING_QUOTA);
            Assert.Equal(1000, RankingConfig.TOP_RANKING_QUOTA[AbyssRankEnum.STAR1_OFFICER]);
            Assert.Equal(1, RankingConfig.TOP_RANKING_QUOTA[AbyssRankEnum.SUPREME_COMMANDER]);
            Assert.NotNull(RankingConfig.TOP_RANKING_GP_LOSS);
            Assert.Equal(9, RankingConfig.TOP_RANKING_GP_LOSS[AbyssRankEnum.STAR1_OFFICER]);
            Assert.Equal(219, RankingConfig.TOP_RANKING_GP_LOSS[AbyssRankEnum.SUPREME_COMMANDER]);

            // Enum scalar + Cron parity.
            Assert.Equal(AbyssRankEnum.STAR5_OFFICER, RankingConfig.XFORM_MIN_RANK);
            Assert.NotNull(RankingConfig.TOP_RANKING_UPDATE_RULE);
            Assert.Equal("0 0 0 ? * *", RankingConfig.TOP_RANKING_UPDATE_RULE.CronExpressionString);
        }
        finally
        {
            ConfigurableProcessor.Process(new JavaProperties(), typeof(RankingConfig));
        }
    }

    [Fact]
    public void RealPropertiesFile_BindsHousingHouseTypeMap_AndCron()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return;
        var configMain = Path.Combine(repoRoot, "game-server", "config", "main");
        if (!File.Exists(Path.Combine(configMain, "housing.properties")))
            return;

        Services.Cron.CronExpressionTransformer.EnsureRegistered();
        try
        {
            var props = new JavaProperties();
            props.LoadFromDirectory(configMain, false);
            ConfigurableProcessor.Process(props, typeof(HousingConfig));

            // @Properties keyPattern with capturing group → HouseType key; int value.
            Assert.NotNull(HousingConfig.AUCTION_AUTO_FILL_LIMITS);
            Assert.Equal(20, HousingConfig.AUCTION_AUTO_FILL_LIMITS[HouseType.HOUSE]);
            Assert.Equal(10, HousingConfig.AUCTION_AUTO_FILL_LIMITS[HouseType.MANSION]);
            Assert.Equal(5, HousingConfig.AUCTION_AUTO_FILL_LIMITS[HouseType.ESTATE]);
            Assert.Equal(1, HousingConfig.AUCTION_AUTO_FILL_LIMITS[HouseType.PALACE]);

            // Cron parity + int[] (no defaultValue → bound from shipped "1, 5").
            Assert.NotNull(HousingConfig.HOUSE_AUCTION_END_TIME);
            Assert.Equal("0 0 12 ? * SUN", HousingConfig.HOUSE_AUCTION_END_TIME.CronExpressionString);
            Assert.Equal(new[] { 1, 5 }, HousingConfig.HOUSE_AUCTION_REGISTER_DAYS);
        }
        finally
        {
            ConfigurableProcessor.Process(new JavaProperties(), typeof(HousingConfig));
        }
    }

    [Fact]
    public void ExplicitOverride_BindsPffHexOpcodeMap()
    {
        try
        {
            // The shipped pff.properties has the packet map commented out → empty map (never null).
            ConfigurableProcessor.Process(new JavaProperties(), typeof(PffConfig));
            Assert.NotNull(PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE);
            Assert.Empty(PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE);

            // Override: hex opcode keys → the NumberTransformer decodes 0x... into the int map key.
            var props = new JavaProperties();
            props.SetProperty("gameserver.network.pff.mode", "2");
            props.SetProperty("gameserver.network.pff.packet.0x0C4", "150");
            props.SetProperty("gameserver.network.pff.packet.0xAB", "75");
            // Non-hex packet key does NOT match the keyPattern → excluded.
            props.SetProperty("gameserver.network.pff.packet.notHex", "999");

            ConfigurableProcessor.Process(props, typeof(PffConfig));

            Assert.Equal(2, PffConfig.PFF_MODE);
            Assert.Equal(150, PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE[0x0C4]);
            Assert.Equal(75, PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE[0xAB]);
            Assert.Equal(2, PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE.Count); // notHex excluded
        }
        finally
        {
            ConfigurableProcessor.Process(new JavaProperties(), typeof(PffConfig));
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
