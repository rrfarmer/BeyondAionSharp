using Aion.Commons.Database;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PlayerEnterWorldRepositoryDatabaseIntegrationTests
{
	private const int PlayerObjectId = 1001;

	[Fact]
	public async Task SaveItemPurificationMutation_WritesInventoryStonesAndAbyssRankAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: InventoryDAO, ItemStoneListDAO, AbyssRankDAO, and game-server/sql/aion_gs.sql.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9001, itemId: 186000001, count: 5);
		await SeedInventoryItemAsync(9002, itemId: 186000002, count: 1);
		await SeedInventoryItemAsync(9003, itemId: 100000401, count: 1, enchant: 15);
		await SeedItemStoneAsync(9002, itemId: 167000001, slot: 0, category: 0);
		await SeedItemStoneAsync(9003, itemId: 168000001, slot: 0, category: 1, procCount: 7);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var targetItem = new InventoryItem
		{
			ObjectId = 9101,
			ItemId = 100000402,
			Count = 1,
			OwnerId = PlayerObjectId,
			Location = 0,
			Enchant = 10,
			EnchantBonus = 3,
			ItemSkin = 100000401,
			OptionalSocket = 2,
			OptionalFusionSocket = 1,
			TuneCount = 4,
			RandomBonus = 55,
			FusionRandomBonus = 66,
			Tempering = 7,
			PackCount = 1,
			IsAmplified = true,
			BuffSkill = 1234,
			RandomPlumeBonus = 8,
			ManaStones =
			[
				new ItemStoneSocket(167000010, 0),
			],
			FusionStones =
			[
				new ItemStoneSocket(167100010, 1),
			],
			Godstone = new PlayerGodstone(168000010, ProcCount: 44),
			IdianStone = new PlayerIdianStone(169000010, PolishNumber: 9, PolishCharge: 250),
		};
		var updatedRank = new PlayerAbyssRank(
			DailyAp: 10,
			WeeklyAp: 20,
			Ap: 700,
			DailyGp: 30,
			WeeklyGp: 40,
			Gp: 50,
			Rank: 3,
			DailyKill: 1,
			WeeklyKill: 2,
			AllKill: 3,
			MaxRank: 4,
			LastKill: 5,
			LastAp: 6,
			LastGp: 7,
			RankingListPosition: 8);

		var saved = await repository.SaveItemPurificationMutationAsync(
			PlayerObjectId,
			materialItemUpdates:
			[
				new InventoryItem { ObjectId = 9001, Count = 2 },
			],
			deletedMaterialItemObjectIds:
			[
				9002,
			],
			baseItemUpdate: null,
			deletedBaseItemObjectId: 9003,
			updatedTargetItems: [],
			addedTargetItems:
			[
				targetItem,
			],
			abyssRank: updatedRank);

		Assert.True(saved);
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9001"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM inventory WHERE item_unique_id IN (9002, 9003)"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM item_stones WHERE item_unique_id IN (9002, 9003)"));
		Assert.Equal(100000402, await ExecuteScalarLongAsync("SELECT item_id FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(10, await ExecuteScalarLongAsync("SELECT enchant FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT enchant_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT optional_socket FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT optional_fusion_socket FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(4, await ExecuteScalarLongAsync("SELECT tune_count FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(55, await ExecuteScalarLongAsync("SELECT rnd_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(66, await ExecuteScalarLongAsync("SELECT fusion_rnd_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(7, await ExecuteScalarLongAsync("SELECT tempering FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT is_amplified FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(1234, await ExecuteScalarLongAsync("SELECT buff_skill FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(8, await ExecuteScalarLongAsync("SELECT rnd_plume_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(4, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM item_stones WHERE item_unique_id = 9101"));
		Assert.Equal(167000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9101 AND category = 0 AND slot = 0"));
		Assert.Equal(168000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9101 AND category = 1 AND slot = 0"));
		Assert.Equal(44, await ExecuteScalarLongAsync("SELECT proc_count FROM item_stones WHERE item_unique_id = 9101 AND category = 1 AND slot = 0"));
		Assert.Equal(167100010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9101 AND category = 2 AND slot = 1"));
		Assert.Equal(169000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9101 AND category = 3 AND slot = 0"));
		Assert.Equal(9, await ExecuteScalarLongAsync("SELECT polishNumber FROM item_stones WHERE item_unique_id = 9101 AND category = 3 AND slot = 0"));
		Assert.Equal(250, await ExecuteScalarLongAsync("SELECT polishCharge FROM item_stones WHERE item_unique_id = 9101 AND category = 3 AND slot = 0"));
		Assert.Equal(700, await ExecuteScalarLongAsync("SELECT ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT `rank` FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(10, await ExecuteScalarLongAsync("SELECT daily_ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(20, await ExecuteScalarLongAsync("SELECT weekly_ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(8, await ExecuteScalarLongAsync("SELECT rank_pos FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(50, await ExecuteScalarLongAsync("SELECT gp FROM abyss_rank WHERE player_id = 1001"));
	}

	[Fact]
	public async Task SaveItemPurificationMutation_RollsBackPriorUpdatesWhenRequiredDeleteFailsAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: InventoryDAO.deleteItems/updateItems commit by category; C# keeps this write set atomic.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9201, itemId: 186000001, count: 5);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var saved = await repository.SaveItemPurificationMutationAsync(
			PlayerObjectId,
			materialItemUpdates:
			[
				new InventoryItem { ObjectId = 9201, Count = 2 },
			],
			deletedMaterialItemObjectIds:
			[
				9299,
			],
			baseItemUpdate: null,
			deletedBaseItemObjectId: null,
			updatedTargetItems: [],
			addedTargetItems: [],
			abyssRank: null);

		Assert.False(saved);
		Assert.Equal(5, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9201"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM inventory WHERE item_unique_id = 9299"));
	}

	private static void InitializeDatabaseFactory()
	{
		DatabaseFactory.Initialize(
			server: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_HOST") ?? "localhost",
			userId: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_USER") ?? "root",
			password: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_PASSWORD") ?? "aion",
			database: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_NAME") ?? "aion_gs",
			port: int.Parse(Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_PORT") ?? "3307"));
	}

	private static async Task InitializeSchemaAsync()
	{
		var sql = await File.ReadAllTextAsync(FindGameSchemaPath());
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		foreach (var statement in SplitSqlStatements(sql))
		{
			await using var command = connection.CreateCommand();
			command.CommandText = statement;
			await command.ExecuteNonQueryAsync();
		}
	}

	private static string FindGameSchemaPath()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "game-server", "sql", "aion_gs.sql");
			if (File.Exists(candidate))
				return candidate;
			directory = directory.Parent;
		}

		throw new FileNotFoundException("Could not find game-server/sql/aion_gs.sql from test output directory.", "game-server/sql/aion_gs.sql");
	}

	private static IEnumerable<string> SplitSqlStatements(string sql)
	{
		var lines = sql.Split('\n')
			.Select(line => line.TrimEnd('\r'))
			.Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line));
		return string.Join('\n', lines)
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(statement => !string.IsNullOrWhiteSpace(statement));
	}

	private static async Task SeedPlayerAsync()
	{
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO players (
				id, name, account_id, account_name, exp, recoverexp, old_level, x, y, z, heading, world_id,
				gender, race, player_class, creation_date
			)
			VALUES (1001, 'PurifyIntegration', 1, 'integration', 0, 0, 0, 0, 0, 0, 0, 210010000,
				'MALE', 'ELYOS', 'RANGER', CURRENT_TIMESTAMP)
			""");
	}

	private static Task SeedInventoryItemAsync(
		int objectId,
		int itemId,
		long count,
		int enchant = 0)
	{
		return ExecuteNonQueryAsync(
			$"""
			INSERT INTO inventory (
				item_unique_id, item_id, item_count, item_owner, item_location, enchant
			)
			VALUES ({objectId}, {itemId}, {count}, {PlayerObjectId}, 0, {enchant})
			""");
	}

	private static Task SeedItemStoneAsync(
		int itemObjectId,
		int itemId,
		int slot,
		int category,
		int polishNumber = 0,
		int polishCharge = 0,
		int procCount = 0)
	{
		return ExecuteNonQueryAsync(
			$"""
			INSERT INTO item_stones (item_unique_id, item_id, slot, category, polishNumber, polishCharge, proc_count)
			VALUES ({itemObjectId}, {itemId}, {slot}, {category}, {polishNumber}, {polishCharge}, {procCount})
			""");
	}

	private static async Task ExecuteNonQueryAsync(string sql)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<long> ExecuteScalarLongAsync(string sql)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		var value = await command.ExecuteScalarAsync();
		Assert.NotNull(value);
		return Convert.ToInt64(value);
	}
}
