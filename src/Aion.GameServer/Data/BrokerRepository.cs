using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IBrokerRepository
{
	Task<PlayerBrokerItem?> LoadRegisteredItemAsync(
		int playerObjectId,
		string race,
		int brokerItemObjectId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerBrokerItem>> LoadSettledItemsForAccountAsync(
		int playerObjectId,
		string race,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerBrokerItem>> LoadRegisteredItemsAsync(
		int playerObjectId,
		string race,
		CancellationToken cancellationToken = default);

	Task<PlayerBrokerItemPage> LoadSettledItemsAsync(
		int playerObjectId,
		string race,
		int pageIndex,
		CancellationToken cancellationToken = default);

	Task<PlayerBrokerItemPage> SearchItemsByTemplateIdsAsync(
		string race,
		byte sortType,
		int pageIndex,
		IReadOnlyList<int> itemIds,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerBrokerItem>> LoadActiveItemsAsync(
		string race,
		CancellationToken cancellationToken = default);

	Task<PlayerBrokerPriceRange> LoadPriceRangeAsync(
		string race,
		int itemId,
		CancellationToken cancellationToken = default);

	Task<bool> CancelRegisteredItemAsync(
		PlayerBrokerItem brokerItem,
		InventoryItem returnedItem,
		CancellationToken cancellationToken = default);

	Task<bool> SettleAccountAsync(
		PlayerBrokerAccountSettlement settlement,
		CancellationToken cancellationToken = default);

	Task<bool> RegisterItemAsync(
		PlayerBrokerItem brokerItem,
		InventoryItem brokerStorageItem,
		InventoryItem? reducedSourceItem,
		InventoryItem kinahItem,
		CancellationToken cancellationToken = default);
}

public sealed class EmptyBrokerRepository : IBrokerRepository
{
	public Task<PlayerBrokerItem?> LoadRegisteredItemAsync(int playerObjectId, string race, int brokerItemObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<PlayerBrokerItem?>(null);
	}

	public Task<IReadOnlyList<PlayerBrokerItem>> LoadSettledItemsForAccountAsync(int playerObjectId, string race, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerBrokerItem>>(Array.Empty<PlayerBrokerItem>());
	}

	public Task<IReadOnlyList<PlayerBrokerItem>> LoadRegisteredItemsAsync(int playerObjectId, string race, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerBrokerItem>>(Array.Empty<PlayerBrokerItem>());
	}

	public Task<PlayerBrokerItemPage> LoadSettledItemsAsync(int playerObjectId, string race, int pageIndex, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, pageIndex, 0));
	}

	public Task<PlayerBrokerPriceRange> LoadPriceRangeAsync(string race, int itemId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new PlayerBrokerPriceRange(0, 0));
	}

	public Task<PlayerBrokerItemPage> SearchItemsByTemplateIdsAsync(
		string race,
		byte sortType,
		int pageIndex,
		IReadOnlyList<int> itemIds,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, pageIndex, 0));
	}

	public Task<IReadOnlyList<PlayerBrokerItem>> LoadActiveItemsAsync(string race, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerBrokerItem>>(Array.Empty<PlayerBrokerItem>());
	}

	public Task<bool> CancelRegisteredItemAsync(PlayerBrokerItem brokerItem, InventoryItem returnedItem, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> SettleAccountAsync(PlayerBrokerAccountSettlement settlement, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> RegisterItemAsync(
		PlayerBrokerItem brokerItem,
		InventoryItem brokerStorageItem,
		InventoryItem? reducedSourceItem,
		InventoryItem kinahItem,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlBrokerRepository : IBrokerRepository
{
	private const int BrokerStorageId = 126;
	private const int SettledItemsPerPage = 9;
	private readonly ILogger<MySqlBrokerRepository> _logger;

	public MySqlBrokerRepository(ILogger<MySqlBrokerRepository> logger)
	{
		_logger = logger;
	}

	public async Task<PlayerBrokerItem?> LoadRegisteredItemAsync(
		int playerObjectId,
		string race,
		int brokerItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.cancelRegisteredItem looks up the active race broker item by object id.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null)
			return null;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			var items = await LoadBrokerItemsAsync(
				connection,
				"""
				WHERE b.seller_id = @seller_id
					AND b.broker_race = @broker_race
					AND b.item_pointer = @item_pointer
					AND b.is_sold = 0
					AND b.is_settled = 0
					AND i.item_unique_id IS NOT NULL
				""",
				[
					new MySqlParameter("@seller_id", playerObjectId),
					new MySqlParameter("@broker_race", brokerRace),
					new MySqlParameter("@item_pointer", brokerItemObjectId),
				],
				cancellationToken);
			return items.FirstOrDefault();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load registered broker item {BrokerItemObjectId} for player {PlayerObjectId}", brokerItemObjectId, playerObjectId);
			return null;
		}
	}

	public async Task<IReadOnlyList<PlayerBrokerItem>> LoadRegisteredItemsAsync(
		int playerObjectId,
		string race,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.showRegisteredItems from race broker cache.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null)
			return Array.Empty<PlayerBrokerItem>();

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			var items = await LoadBrokerItemsAsync(
				connection,
				"""
				WHERE b.seller_id = @seller_id
					AND b.broker_race = @broker_race
					AND b.is_sold = 0
					AND b.is_settled = 0
					AND i.item_unique_id IS NOT NULL
				ORDER BY b.expire_time, b.item_pointer
				""",
				[
					new MySqlParameter("@seller_id", playerObjectId),
					new MySqlParameter("@broker_race", brokerRace),
				],
				cancellationToken);
			return items;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load registered broker items for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerBrokerItem>();
		}
	}

	public async Task<IReadOnlyList<PlayerBrokerItem>> LoadSettledItemsForAccountAsync(
		int playerObjectId,
		string race,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.settleAccount collects every settled broker item for the player.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null)
			return Array.Empty<PlayerBrokerItem>();

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			return await LoadBrokerItemsAsync(
				connection,
				"""
				WHERE b.seller_id = @seller_id
					AND b.broker_race = @broker_race
					AND b.is_settled = 1
				ORDER BY b.settle_time, b.item_pointer
				""",
				[
					new MySqlParameter("@seller_id", playerObjectId),
					new MySqlParameter("@broker_race", brokerRace),
				],
				cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load settled broker account items for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerBrokerItem>();
		}
	}

	public async Task<PlayerBrokerItemPage> LoadSettledItemsAsync(
		int playerObjectId,
		string race,
		int pageIndex,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.showSettledItems.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null)
			return new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, pageIndex, 0);

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			var allItems = await LoadBrokerItemsAsync(
				connection,
				"""
				WHERE b.seller_id = @seller_id
					AND b.broker_race = @broker_race
					AND b.is_settled = 1
				ORDER BY b.settle_time, b.item_pointer
				""",
				[
					new MySqlParameter("@seller_id", playerObjectId),
					new MySqlParameter("@broker_race", brokerRace),
				],
				cancellationToken);
			var start = Math.Max(0, pageIndex) * SettledItemsPerPage;
			var pageItems = start >= allItems.Count
				? Array.Empty<PlayerBrokerItem>()
				: allItems.Skip(start).Take(SettledItemsPerPage).ToArray();
			var settledKinah = allItems.Where(item => item.IsSold).Sum(item => item.Price * item.ItemCount);
			return new PlayerBrokerItemPage(pageItems, allItems.Count, pageIndex, settledKinah);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load settled broker items for player {PlayerObjectId}", playerObjectId);
			return new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, pageIndex, 0);
		}
	}

	public async Task<PlayerBrokerPriceRange> LoadPriceRangeAsync(
		string race,
		int itemId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.showSellWindow current race price summary.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null)
			return new PlayerBrokerPriceRange(0, 0);

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT COALESCE(MIN(price), 0) AS lowest_price, COALESCE(MAX(price), 0) AS highest_price
				FROM broker
				WHERE broker_race = ? AND item_id = ? AND is_sold = 0 AND is_settled = 0
				""";
			command.Parameters.AddRange(new[] { new MySqlParameter { Value = brokerRace }, new MySqlParameter { Value = itemId } });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return new PlayerBrokerPriceRange(0, 0);
			return new PlayerBrokerPriceRange(ReadLong(reader, "lowest_price"), ReadLong(reader, "highest_price"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load broker price range for item {ItemId}", itemId);
			return new PlayerBrokerPriceRange(0, 0);
		}
	}

	public async Task<PlayerBrokerItemPage> SearchItemsByTemplateIdsAsync(
		string race,
		byte sortType,
		int pageIndex,
		IReadOnlyList<int> itemIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.showRequestedItems itemList branch when clientMask == 0.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null || itemIds.Count == 0)
			return new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, pageIndex, 0);

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			var parameters = new List<MySqlParameter> { new("@broker_race", brokerRace) };
			var placeholders = new string[itemIds.Count];
			for (var i = 0; i < itemIds.Count; i++)
			{
				var parameterName = $"@item{i}";
				placeholders[i] = parameterName;
				parameters.Add(new MySqlParameter(parameterName, itemIds[i]));
			}

			var items = await LoadBrokerItemsAsync(
				connection,
				$"""
				WHERE b.broker_race = @broker_race
					AND b.item_id IN ({string.Join(", ", placeholders)})
					AND b.is_sold = 0
					AND b.is_settled = 0
					AND i.item_unique_id IS NOT NULL
				ORDER BY b.item_pointer
				""",
				parameters,
				cancellationToken);
			var sortedItems = SortBrokerItems(
					AttachAveragePrices(items),
					sortType)
				.ToArray();
			var start = Math.Max(0, pageIndex) * 9;
			var pageItems = start >= sortedItems.Length
				? Array.Empty<PlayerBrokerItem>()
				: sortedItems.Skip(start).Take(45).ToArray();
			return new PlayerBrokerItemPage(pageItems, sortedItems.Length, pageIndex, 0);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not search broker items for race {Race}", race);
			return new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, pageIndex, 0);
		}
	}

	public async Task<IReadOnlyList<PlayerBrokerItem>> LoadActiveItemsAsync(
		string race,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.getRaceBrokerItems active race cache.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null)
			return Array.Empty<PlayerBrokerItem>();

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			var items = await LoadBrokerItemsAsync(
				connection,
				"""
				WHERE b.broker_race = @broker_race
					AND b.is_sold = 0
					AND b.is_settled = 0
					AND i.item_unique_id IS NOT NULL
				ORDER BY b.item_pointer
				""",
				[new MySqlParameter("@broker_race", brokerRace)],
				cancellationToken);
			return AttachAveragePrices(items);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load active broker items for race {Race}", race);
			return Array.Empty<PlayerBrokerItem>();
		}
	}

	public async Task<bool> CancelRegisteredItemAsync(
		PlayerBrokerItem brokerItem,
		InventoryItem returnedItem,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.cancelRegisteredItem + dao/BrokerDAO.deleteBrokerItem.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await using (var itemCommand = connection.CreateCommand())
			{
				itemCommand.Transaction = transaction;
				itemCommand.CommandText = """
					UPDATE inventory
					SET item_owner = ?, item_location = ?, slot = ?, is_equipped = ?
					WHERE item_unique_id = ? AND item_location = ?
					""";
				itemCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = returnedItem.OwnerId },
						new MySqlParameter { Value = returnedItem.Location },
						new MySqlParameter { Value = returnedItem.Slot },
						new MySqlParameter { Value = returnedItem.IsEquipped },
						new MySqlParameter { Value = returnedItem.ObjectId },
						new MySqlParameter { Value = BrokerStorageId },
					});
				if (await itemCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
				{
					await transaction.RollbackAsync(cancellationToken);
					return false;
				}
			}

			await using (var brokerCommand = connection.CreateCommand())
			{
				brokerCommand.Transaction = transaction;
				brokerCommand.CommandText = """
					DELETE FROM broker
					WHERE item_pointer = ? AND seller_id = ? AND expire_time = ?
					""";
				brokerCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = brokerItem.ItemObjectId },
						new MySqlParameter { Value = brokerItem.SellerId },
						new MySqlParameter { Value = brokerItem.ExpireTime },
					});
				if (await brokerCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
				{
					await transaction.RollbackAsync(cancellationToken);
					return false;
				}
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not cancel registered broker item {BrokerItemObjectId}", brokerItem.ItemObjectId);
			return false;
		}
	}

	public async Task<bool> SettleAccountAsync(
		PlayerBrokerAccountSettlement settlement,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.settleAccount with BrokerDAO.deleteBrokerItem final state.
		if (settlement.CollectedBrokerItems.Count == 0 && settlement.ReturnedItems.Count == 0 && settlement.KinahItem == null)
			return true;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var returnedItem in settlement.ReturnedItems)
			{
				if (!await MoveBrokerItemToCubeAsync(connection, transaction, returnedItem.ReturnedItem, cancellationToken))
				{
					await transaction.RollbackAsync(cancellationToken);
					return false;
				}
			}

			if (settlement.KinahItem != null)
				await UpsertInventoryItemAsync(connection, transaction, settlement.KinahItem, cancellationToken);

			foreach (var brokerItem in settlement.CollectedBrokerItems)
			{
				if (!await DeleteBrokerItemAsync(connection, transaction, brokerItem, cancellationToken))
				{
					await transaction.RollbackAsync(cancellationToken);
					return false;
				}
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not settle broker account");
			return false;
		}
	}

	public async Task<bool> RegisterItemAsync(
		PlayerBrokerItem brokerItem,
		InventoryItem brokerStorageItem,
		InventoryItem? reducedSourceItem,
		InventoryItem kinahItem,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.registerItem + BrokerOpSaveTask(item, brokerItem, kinahItem).
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await UpsertInventoryItemAsync(connection, transaction, brokerStorageItem, cancellationToken);
			if (reducedSourceItem != null)
				await UpsertInventoryItemAsync(connection, transaction, reducedSourceItem, cancellationToken);
			await UpsertInventoryItemAsync(connection, transaction, kinahItem, cancellationToken);
			await InsertBrokerItemAsync(connection, transaction, brokerItem, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not register broker item {BrokerItemObjectId}", brokerItem.ItemObjectId);
			return false;
		}
	}

	private static async Task InsertBrokerItemAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		PlayerBrokerItem brokerItem,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO broker (
				item_pointer, item_id, item_count, item_creator, price, broker_race, expire_time,
				seller_id, is_sold, is_settled, splitting_available
			)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = brokerItem.ItemObjectId },
				new MySqlParameter { Value = brokerItem.ItemId },
				new MySqlParameter { Value = brokerItem.ItemCount },
				new MySqlParameter { Value = brokerItem.ItemCreator.Length == 0 ? (object)DBNull.Value : brokerItem.ItemCreator },
				new MySqlParameter { Value = brokerItem.Price },
				new MySqlParameter { Value = brokerItem.BrokerRace },
				new MySqlParameter { Value = brokerItem.ExpireTime },
				new MySqlParameter { Value = brokerItem.SellerId },
				new MySqlParameter { Value = brokerItem.IsSold },
				new MySqlParameter { Value = brokerItem.IsSettled },
				new MySqlParameter { Value = brokerItem.SplittingAvailable },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task<bool> MoveBrokerItemToCubeAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		InventoryItem returnedItem,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			UPDATE inventory
			SET item_owner = ?, item_location = ?, slot = ?, is_equipped = ?
			WHERE item_unique_id = ? AND item_location = ?
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = returnedItem.OwnerId },
				new MySqlParameter { Value = returnedItem.Location },
				new MySqlParameter { Value = returnedItem.Slot },
				new MySqlParameter { Value = returnedItem.IsEquipped },
				new MySqlParameter { Value = returnedItem.ObjectId },
				new MySqlParameter { Value = BrokerStorageId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) != 0;
	}

	private static async Task<bool> DeleteBrokerItemAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		PlayerBrokerItem brokerItem,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			DELETE FROM broker
			WHERE item_pointer = ? AND seller_id = ? AND expire_time = ?
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = brokerItem.ItemObjectId },
				new MySqlParameter { Value = brokerItem.SellerId },
				new MySqlParameter { Value = brokerItem.ExpireTime },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) != 0;
	}

	private static async Task UpsertInventoryItemAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO inventory (
				item_unique_id, item_id, item_count, item_color, color_expires, item_creator, expire_time, activation_count,
				item_owner, is_equipped, is_soul_bound, slot, item_location, enchant, enchant_bonus, item_skin,
				fusioned_item, optional_socket, optional_fusion_socket, charge, tune_count, rnd_bonus, fusion_rnd_bonus,
				tempering, pack_count, is_amplified, buff_skill, rnd_plume_bonus
			)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
			ON DUPLICATE KEY UPDATE
				item_count = VALUES(item_count),
				item_owner = VALUES(item_owner),
				is_equipped = VALUES(is_equipped),
				slot = VALUES(slot),
				item_location = VALUES(item_location)
			""";
		command.Parameters.AddRange(CreateInventoryParameters(item));
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static MySqlParameter[] CreateInventoryParameters(InventoryItem item)
	{
		return
		[
			new MySqlParameter { Value = item.ObjectId },
			new MySqlParameter { Value = item.ItemId },
			new MySqlParameter { Value = item.Count },
			new MySqlParameter { Value = item.Color.HasValue ? item.Color.Value : (object)DBNull.Value },
			new MySqlParameter { Value = item.ColorExpires },
			new MySqlParameter { Value = item.Creator ?? (object)DBNull.Value },
			new MySqlParameter { Value = item.ExpireTime },
			new MySqlParameter { Value = item.ActivationCount },
			new MySqlParameter { Value = item.OwnerId },
			new MySqlParameter { Value = item.IsEquipped },
			new MySqlParameter { Value = item.IsSoulBound },
			new MySqlParameter { Value = item.Slot },
			new MySqlParameter { Value = item.Location },
			new MySqlParameter { Value = item.Enchant },
			new MySqlParameter { Value = item.EnchantBonus },
			new MySqlParameter { Value = item.ItemSkin },
			new MySqlParameter { Value = item.FusionedItem },
			new MySqlParameter { Value = item.OptionalSocket },
			new MySqlParameter { Value = item.OptionalFusionSocket },
			new MySqlParameter { Value = item.Charge },
			new MySqlParameter { Value = item.TuneCount },
			new MySqlParameter { Value = item.RandomBonus },
			new MySqlParameter { Value = item.FusionRandomBonus },
			new MySqlParameter { Value = item.Tempering },
			new MySqlParameter { Value = item.PackCount },
			new MySqlParameter { Value = item.IsAmplified },
			new MySqlParameter { Value = item.BuffSkill },
			new MySqlParameter { Value = item.RandomPlumeBonus },
		];
	}

	private static async Task<IReadOnlyList<PlayerBrokerItem>> LoadBrokerItemsAsync(
		MySqlConnection connection,
		string whereClause,
		IReadOnlyList<MySqlParameter> parameters,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = $"""
			SELECT
				b.item_pointer, b.item_id AS broker_item_id, b.item_count AS broker_item_count, b.item_creator AS broker_item_creator,
				b.price, b.broker_race, b.expire_time, b.settle_time, b.seller_id, b.is_sold, b.is_settled, b.splitting_available,
				COALESCE(p.name, '') AS seller_name,
				i.item_unique_id, i.item_id, i.item_count, i.item_color, i.color_expires, i.item_creator, i.expire_time AS item_expire_time, i.activation_count,
				i.item_owner, i.is_equipped, i.is_soul_bound, i.slot, i.item_location, i.enchant, i.enchant_bonus, i.item_skin, i.fusioned_item,
				i.optional_socket, i.optional_fusion_socket, i.charge, i.tune_count, i.rnd_bonus, i.fusion_rnd_bonus, i.tempering, i.pack_count,
				i.is_amplified, i.buff_skill, i.rnd_plume_bonus
			FROM broker b
			LEFT JOIN players p ON p.id = b.seller_id
			LEFT JOIN inventory i ON i.item_unique_id = b.item_pointer AND i.item_location = {BrokerStorageId}
			{whereClause}
			""";
		foreach (var parameter in parameters)
			command.Parameters.Add(parameter);

		var brokerItems = new List<PlayerBrokerItem>();
		var inventoryItems = new List<InventoryItem>();
		await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
		{
			while (await reader.ReadAsync(cancellationToken))
			{
				InventoryItem? item = null;
				if (!reader.IsDBNull(reader.GetOrdinal("item_unique_id")))
				{
					item = ReadItem(reader);
					inventoryItems.Add(item);
				}

				brokerItems.Add(
					new PlayerBrokerItem(
						ReadInt(reader, "item_pointer"),
						ReadInt(reader, "broker_item_id"),
						ReadLong(reader, "broker_item_count"),
						ReadString(reader, "broker_item_creator"),
						ReadLong(reader, "price"),
						ReadInt(reader, "seller_id"),
						ReadString(reader, "seller_name"),
						ReadString(reader, "broker_race"),
						ReadBoolean(reader, "is_sold"),
						ReadBoolean(reader, "is_settled"),
						ReadDateTime(reader, "expire_time") ?? DateTime.MinValue,
						ReadDateTime(reader, "settle_time") ?? DateTime.MinValue,
						ReadBoolean(reader, "splitting_available"),
						item));
			}
		}

		await LoadItemStonesForItemsAsync(connection, inventoryItems, cancellationToken);
		return brokerItems;
	}

	private static async Task LoadItemStonesForItemsAsync(
		MySqlConnection connection,
		IReadOnlyList<InventoryItem> items,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/ItemStoneListDAO.load(Collection<Item>) after BrokerDAO.loadBroker.
		if (items.Count == 0)
			return;

		var itemsByObjectId = items.ToDictionary(item => item.ObjectId);
		await using var command = connection.CreateCommand();
		var placeholders = new string[items.Count];
		for (var i = 0; i < items.Count; i++)
		{
			var parameterName = $"@item{i}";
			placeholders[i] = parameterName;
			command.Parameters.Add(new MySqlParameter(parameterName, items[i].ObjectId));
		}

		command.CommandText = $"""
			SELECT item_unique_id, item_id, slot, category, polishNumber, polishCharge, proc_count
			FROM item_stones
			WHERE item_unique_id IN ({string.Join(", ", placeholders)})
			ORDER BY item_unique_id, category, slot
			""";

		var manaStones = new Dictionary<int, List<ItemStoneSocket>>();
		var fusionStones = new Dictionary<int, List<ItemStoneSocket>>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var itemObjectId = ReadInt(reader, "item_unique_id");
			if (!itemsByObjectId.TryGetValue(itemObjectId, out var item))
				continue;

			var itemId = ReadInt(reader, "item_id");
			var slot = ReadInt(reader, "slot");
			var stoneType = ReadInt(reader, "category");
			switch (stoneType)
			{
				case 0:
					AddStone(manaStones, itemObjectId, new ItemStoneSocket(itemId, slot));
					break;
				case 1:
					item.Godstone = new PlayerGodstone(itemId, ReadInt(reader, "proc_count"));
					break;
				case 2:
					AddStone(fusionStones, itemObjectId, new ItemStoneSocket(itemId, slot));
					break;
				case 3:
					item.IdianStone = new PlayerIdianStone(itemId, ReadInt(reader, "polishNumber"), ReadInt(reader, "polishCharge"));
					break;
			}
		}

		foreach (var item in items)
		{
			if (manaStones.TryGetValue(item.ObjectId, out var itemManaStones))
				item.ManaStones = itemManaStones.OrderBy(stone => stone.Slot).ToArray();
			if (fusionStones.TryGetValue(item.ObjectId, out var itemFusionStones))
				item.FusionStones = itemFusionStones.OrderBy(stone => stone.Slot).ToArray();
		}
	}

	private static InventoryItem ReadItem(MySqlDataReader reader)
	{
		// Java parity: dao/InventoryDAO.constructItem column mapping for StorageType.BROKER.
		return new InventoryItem
		{
			ObjectId = ReadInt(reader, "item_unique_id"),
			ItemId = ReadInt(reader, "item_id"),
			Count = ReadLong(reader, "item_count"),
			Color = ReadNullableInt(reader, "item_color"),
			ColorExpires = ReadInt(reader, "color_expires"),
			Creator = ReadNullableString(reader, "item_creator"),
			ExpireTime = ReadInt(reader, "item_expire_time"),
			ActivationCount = ReadInt(reader, "activation_count"),
			OwnerId = ReadInt(reader, "item_owner"),
			IsEquipped = ReadBoolean(reader, "is_equipped"),
			IsSoulBound = ReadBoolean(reader, "is_soul_bound"),
			Slot = ReadLong(reader, "slot"),
			Location = ReadInt(reader, "item_location"),
			Enchant = ReadInt(reader, "enchant"),
			EnchantBonus = ReadInt(reader, "enchant_bonus"),
			ItemSkin = ReadInt(reader, "item_skin"),
			FusionedItem = ReadInt(reader, "fusioned_item"),
			OptionalSocket = ReadInt(reader, "optional_socket"),
			OptionalFusionSocket = ReadInt(reader, "optional_fusion_socket"),
			Charge = ReadInt(reader, "charge"),
			TuneCount = ReadInt(reader, "tune_count"),
			RandomBonus = ReadInt(reader, "rnd_bonus"),
			FusionRandomBonus = ReadInt(reader, "fusion_rnd_bonus"),
			Tempering = ReadInt(reader, "tempering"),
			PackCount = ReadInt(reader, "pack_count"),
			IsAmplified = ReadBoolean(reader, "is_amplified"),
			BuffSkill = ReadInt(reader, "buff_skill"),
			RandomPlumeBonus = ReadInt(reader, "rnd_plume_bonus"),
		};
	}

	private static IEnumerable<PlayerBrokerItem> SortBrokerItems(IEnumerable<PlayerBrokerItem> items, byte sortType)
	{
		// Java parity: model/gameobjects/BrokerItem.getComparatoryByType for the price-backed sorts.
		return sortType switch
		{
			4 => items.OrderBy(item => item.Price),
			5 => items.OrderByDescending(item => item.Price),
			6 => items.OrderBy(item => GetPiecePrice(item)),
			7 => items.OrderByDescending(item => GetPiecePrice(item)),
			_ => items.OrderBy(item => item.ItemId).ThenBy(item => item.ItemObjectId),
		};
	}

	private static IReadOnlyList<PlayerBrokerItem> AttachAveragePrices(IReadOnlyList<PlayerBrokerItem> items)
	{
		var averagePrices = items
			.GroupBy(item => item.ItemId)
			.ToDictionary(group => group.Key, group => (long)group.Average(item => item.Price));
		return items
			.Select(item => item with { AveragePrice = averagePrices.TryGetValue(item.ItemId, out var averagePrice) ? averagePrice : 0 })
			.ToArray();
	}

	private static long GetPiecePrice(PlayerBrokerItem item)
	{
		return item.ItemCount <= 0 ? item.Price : item.Price / item.ItemCount;
	}

	private static void AddStone(Dictionary<int, List<ItemStoneSocket>> stonesByItem, int itemObjectId, ItemStoneSocket stone)
	{
		if (!stonesByItem.TryGetValue(itemObjectId, out var stones))
		{
			stones = [];
			stonesByItem[itemObjectId] = stones;
		}

		stones.Add(stone);
	}

	private static string? GetBrokerRace(string race)
	{
		return race switch
		{
			"ELYOS" => "ELYOS",
			"ASMODIANS" => "ASMODIAN",
			_ => null,
		};
	}

	private static int ReadInt(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
	}

	private static long ReadLong(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
	}

	private static int? ReadNullableInt(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
	}

	private static string ReadString(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
	}

	private static string? ReadNullableString(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
	}

	private static DateTime? ReadDateTime(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
	}

	private static bool ReadBoolean(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return !reader.IsDBNull(ordinal) && Convert.ToInt32(reader.GetValue(ordinal)) != 0;
	}
}
