using Aion.Commons.Database;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class SystemMailRepositoryDatabaseIntegrationTests
{
	private const int PlayerObjectId = 1001;

	[Fact]
	public async Task StoreSystemMailOperations_WriteLetterItemAndOfflineCounterAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: SystemMailService.sendMail, MailDAO.saveLetter, InventoryDAO.insertItems, MailDAO.updateOfflineMailCounter.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();

		var repository = new MySqlMailRepository(NullLogger<MySqlMailRepository>.Instance);
		var mail = new PlayerMail(
			9001,
			PlayerObjectId,
			"Beyond Aion",
			"Bonus Pack",
			"Body",
			IsUnread: true,
			9101,
			186000242,
			AttachedKinah: 123,
			LetterType: 1,
			new DateTime(2026, 5, 26, 9, 0, 0));
		var attachedItem = new InventoryItem
		{
			ObjectId = 9101,
			ItemId = 186000242,
			Count = 15,
			OwnerId = 0,
			IsEquipped = false,
			IsSoulBound = true,
			Slot = 0,
			Location = 127,
			Enchant = 3,
			EnchantBonus = 1,
			ItemSkin = 186000242,
			OptionalSocket = 2,
			OptionalFusionSocket = 1,
			Charge = 50,
			TuneCount = 4,
			RandomBonus = 55,
			FusionRandomBonus = 66,
			Tempering = 7,
			PackCount = 1,
			IsAmplified = true,
			BuffSkill = 1234,
			RandomPlumeBonus = 8,
		};

		Assert.True(await repository.StoreSystemMailLetterAsync(mail));
		Assert.True(await repository.StoreSystemMailAttachedItemAsync(attachedItem, PlayerObjectId));
		Assert.True(await repository.UpdateOfflineMailboxCounterAsync("Mailreward", mailboxLetters: 5));

		Assert.Equal(PlayerObjectId, await ExecuteScalarLongAsync("SELECT mail_recipient_id FROM mail WHERE mail_unique_id = 9001"));
		Assert.Equal(9101, await ExecuteScalarLongAsync("SELECT attached_item_id FROM mail WHERE mail_unique_id = 9001"));
		Assert.Equal(123, await ExecuteScalarLongAsync("SELECT attached_kinah_count FROM mail WHERE mail_unique_id = 9001"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT express FROM mail WHERE mail_unique_id = 9001"));
		Assert.Equal(186000242, await ExecuteScalarLongAsync("SELECT item_id FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(15, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(PlayerObjectId, await ExecuteScalarLongAsync("SELECT item_owner FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(127, await ExecuteScalarLongAsync("SELECT item_location FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT is_equipped FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT is_soul_bound FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT enchant FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(55, await ExecuteScalarLongAsync("SELECT rnd_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(5, await ExecuteScalarLongAsync("SELECT mailbox_letters FROM players WHERE id = 1001"));
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

	private static Task SeedPlayerAsync()
	{
		return ExecuteNonQueryAsync(
			"""
			INSERT INTO players (
				id, name, account_id, account_name, exp, recoverexp, old_level, x, y, z, heading, world_id,
				gender, race, player_class, creation_date, mailbox_letters
			)
			VALUES (1001, 'Mailreward', 1, 'integration', 0, 0, 0, 0, 0, 0, 0, 210010000,
				'MALE', 'ELYOS', 'RANGER', CURRENT_TIMESTAMP, 4)
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
