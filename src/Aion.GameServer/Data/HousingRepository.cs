using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IHousingRepository
{
	Task<IReadOnlyList<WorldHouse>> LoadWorldHousesAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<WorldHouse>> LoadWorldStudiosAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default);

	Task<HouseRegistrySummary> LoadHouseRegistryAsync(
		int playerObjectId,
		int buildingId,
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable housingObjectTemplates,
		CancellationToken cancellationToken = default);

	Task<bool> SaveHouseObjectPlacementAsync(
		int playerObjectId,
		RegisteredHouseObjectSummary houseObject,
		CancellationToken cancellationToken = default);

	Task<bool> RegisterHouseObjectFromInventoryAsync(
		int playerObjectId,
		int sourceItemObjectId,
		RegisteredHouseObjectSummary houseObject,
		int? expireTimeSeconds,
		CancellationToken cancellationToken = default);

	Task<bool> RegisterHouseDecorationFromInventoryAsync(
		int playerObjectId,
		int sourceItemObjectId,
		RegisteredHouseDecorationSummary decoration,
		CancellationToken cancellationToken = default);

	Task<bool> SaveHouseDecorationMutationAsync(
		int playerObjectId,
		IReadOnlyList<RegisteredHouseDecorationSummary> updatedDecorations,
		IReadOnlyList<int> deletedDecorationObjectIds,
		CancellationToken cancellationToken = default);

	Task<bool> SaveHouseRenovationAsync(
		int playerObjectId,
		int houseObjectId,
		int buildingId,
		IReadOnlyList<InventoryItem> updatedCouponItems,
		IReadOnlyList<int> deletedCouponItemObjectIds,
		CancellationToken cancellationToken = default);

	Task<bool> SaveHouseObjectUseAsync(
		int houseOwnerObjectId,
		int usingPlayerObjectId,
		RegisteredHouseObjectSummary? updatedHouseObject,
		int? deletedHouseObjectId,
		IReadOnlyList<InventoryItem> updatedConsumedItems,
		IReadOnlyList<int> deletedConsumedObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default);

	Task<bool> DeleteHouseRegisteredObjectAsync(
		int playerObjectId,
		int itemObjectId,
		CancellationToken cancellationToken = default);
}

public sealed class EmptyHousingRepository : IHousingRepository
{
	public Task<IReadOnlyList<WorldHouse>> LoadWorldHousesAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<WorldHouse>>(Array.Empty<WorldHouse>());
	}

	public Task<IReadOnlyList<WorldHouse>> LoadWorldStudiosAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<WorldHouse>>(Array.Empty<WorldHouse>());
	}

	public Task<HouseRegistrySummary> LoadHouseRegistryAsync(
		int playerObjectId,
		int buildingId,
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable housingObjectTemplates,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(HouseRegistrySummary.Empty);
	}

	public Task<bool> SaveHouseObjectPlacementAsync(
		int playerObjectId,
		RegisteredHouseObjectSummary houseObject,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> RegisterHouseObjectFromInventoryAsync(
		int playerObjectId,
		int sourceItemObjectId,
		RegisteredHouseObjectSummary houseObject,
		int? expireTimeSeconds,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> RegisterHouseDecorationFromInventoryAsync(
		int playerObjectId,
		int sourceItemObjectId,
		RegisteredHouseDecorationSummary decoration,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveHouseDecorationMutationAsync(
		int playerObjectId,
		IReadOnlyList<RegisteredHouseDecorationSummary> updatedDecorations,
		IReadOnlyList<int> deletedDecorationObjectIds,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveHouseRenovationAsync(
		int playerObjectId,
		int houseObjectId,
		int buildingId,
		IReadOnlyList<InventoryItem> updatedCouponItems,
		IReadOnlyList<int> deletedCouponItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveHouseObjectUseAsync(
		int houseOwnerObjectId,
		int usingPlayerObjectId,
		RegisteredHouseObjectSummary? updatedHouseObject,
		int? deletedHouseObjectId,
		IReadOnlyList<InventoryItem> updatedConsumedItems,
		IReadOnlyList<int> deletedConsumedObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> DeleteHouseRegisteredObjectAsync(
		int playerObjectId,
		int itemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}
}

public sealed class MySqlHousingRepository : IHousingRepository
{
	private readonly ILogger<MySqlHousingRepository> _logger;

	public MySqlHousingRepository(ILogger<MySqlHousingRepository> logger)
	{
		_logger = logger;
	}

	public Task<IReadOnlyList<WorldHouse>> LoadWorldHousesAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/HousesDAO.loadHouses(..., false) excludes studio addresses.
		return LoadWorldHouseRowsAsync(housingTemplates, studios: false, cancellationToken);
	}

	public Task<IReadOnlyList<WorldHouse>> LoadWorldStudiosAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/HousesDAO.loadHouses(..., true) loads studio addresses into the owner-keyed studio map.
		return LoadWorldHouseRowsAsync(housingTemplates, studios: true, cancellationToken);
	}

	private async Task<IReadOnlyList<WorldHouse>> LoadWorldHouseRowsAsync(
		HousingTemplateTable housingTemplates,
		bool studios,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/HousesDAO.loadHouses plus services/HousingService.updateInactiveStateForAllHouses for spawned custom houses.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					h.id, h.address, h.building_id, h.player_id, h.acquire_time, h.settings, h.sign_notice,
					p.id AS owner_row_id,
					p.name AS owner_name,
					lm.legion_id, l.name AS legion_name,
					le.emblem_id AS legion_emblem_id, le.emblem_type AS legion_emblem_type,
					le.color_a AS legion_emblem_color_a, le.color_r AS legion_emblem_color_r,
					le.color_g AS legion_emblem_color_g, le.color_b AS legion_emblem_color_b
				FROM houses h
					LEFT JOIN players p ON p.id = h.player_id
					LEFT JOIN legion_members lm ON lm.player_id = p.id
					LEFT JOIN legions l ON l.id = lm.legion_id
					LEFT JOIN legion_emblems le ON le.legion_id = lm.legion_id
				WHERE __HOUSE_ADDRESS_FILTER__
				ORDER BY h.player_id, h.acquire_time, h.address
				""".Replace(
				"__HOUSE_ADDRESS_FILTER__",
				studios ? "h.address = 2001 OR h.address = 3001" : "h.address <> 2001 AND h.address <> 3001",
				StringComparison.Ordinal);

			var rows = new List<HouseRow>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				rows.Add(
					new HouseRow(
						ReadInt(reader, "id"),
						ReadInt(reader, "address"),
						ReadInt(reader, "building_id"),
						ReadInt(reader, "player_id"),
						ReadDateTime(reader, "acquire_time"),
						ReadInt(reader, "settings"),
						ReadString(reader, "sign_notice"),
						ReadInt(reader, "owner_row_id") != 0,
						ReadString(reader, "owner_name"),
						ReadInt(reader, "legion_id"),
						ReadString(reader, "legion_name"),
						(byte)ReadInt(reader, "legion_emblem_id"),
						ToLegionEmblemTypeValue(ReadString(reader, "legion_emblem_type")),
						(byte)ReadInt(reader, "legion_emblem_color_a"),
						(byte)ReadInt(reader, "legion_emblem_color_r"),
						(byte)ReadInt(reader, "legion_emblem_color_g"),
						(byte)ReadInt(reader, "legion_emblem_color_b")));
			}

			await RevokeDeletedOwnerHousesAsync(connection, rows, housingTemplates, _logger, cancellationToken);
			var effectiveRows = rows.Select(row => row.WithDeletedOwnerRevoked(housingTemplates)).ToArray();

			var visibleRows = studios
				? effectiveRows.Where(row => row.OwnerObjectId > 0)
				: effectiveRows;
			return visibleRows
				.GroupBy(row => row.OwnerObjectId)
				.SelectMany(group => CreateWorldHouses(group, housingTemplates))
				.ToArray();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load world {HouseKind}", studios ? "studios" : "houses");
			return Array.Empty<WorldHouse>();
		}
	}

	public async Task<HouseRegistrySummary> LoadHouseRegistryAsync(
		int playerObjectId,
		int buildingId,
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable housingObjectTemplates,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerRegisteredItemsDAO.loadRegistry.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT item_unique_id, item_id, expire_time, color, color_expires, owner_use_count,
					visitor_use_count, x, y, z, h, area, room
				FROM player_registered_items
				WHERE player_id = ?
				ORDER BY item_unique_id, item_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var rows = new List<HouseRegisteredItemRow>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				rows.Add(
					new HouseRegisteredItemRow(
						ReadInt(reader, "item_unique_id"),
						ReadInt(reader, "item_id"),
						ReadNullableInt(reader, "expire_time"),
						ReadNullableInt(reader, "color"),
						ReadInt(reader, "color_expires"),
						ReadInt(reader, "owner_use_count"),
						ReadInt(reader, "visitor_use_count"),
						ReadFloat(reader, "x"),
						ReadFloat(reader, "y"),
						ReadFloat(reader, "z"),
						ReadInt(reader, "h"),
						ReadString(reader, "area"),
						ReadInt(reader, "room")));
			}

			return HouseRegistrySummary.FromRows(buildingId, housingTemplates, housingObjectTemplates, rows);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load house registry for player {PlayerObjectId}", playerObjectId);
			return HouseRegistrySummary.Empty;
		}
	}

	public async Task<bool> SaveHouseObjectPlacementAsync(
		int playerObjectId,
		RegisteredHouseObjectSummary houseObject,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerRegisteredItemsDAO.storeObjects update branch for placement fields.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				UPDATE player_registered_items
				SET x = ?, y = ?, z = ?, h = ?, area = ?
				WHERE player_id = ? AND item_unique_id = ? AND item_id = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = houseObject.X },
					new MySqlParameter { Value = houseObject.Y },
					new MySqlParameter { Value = houseObject.Z },
					new MySqlParameter { Value = houseObject.Heading },
					new MySqlParameter { Value = houseObject.IsSpawnedByPlayer ? houseObject.Area : "NONE" },
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = houseObject.ObjectId },
					new MySqlParameter { Value = houseObject.TemplateId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not save house object placement {HouseObjectId} for player {PlayerObjectId}",
				houseObject.ObjectId,
				playerObjectId);
			return false;
		}
	}

	public Task<bool> RegisterHouseObjectFromInventoryAsync(
		int playerObjectId,
		int sourceItemObjectId,
		RegisteredHouseObjectSummary houseObject,
		int? expireTimeSeconds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: player.getInventory().delete(item, REGISTER) then PlayerRegisteredItemsDAO.storeObjects INSERT_QUERY.
		return RegisterHouseItemFromInventoryAsync(
			playerObjectId,
			sourceItemObjectId,
			houseObject.ObjectId,
			houseObject.TemplateId,
			expireTimeSeconds,
			houseObject.Color,
			houseObject.ColorExpires,
			houseObject.OwnerUseCount,
			houseObject.VisitorUseCount,
			houseObject.X,
			houseObject.Y,
			houseObject.Z,
			houseObject.Heading,
			houseObject.IsSpawnedByPlayer ? houseObject.Area : "NONE",
			0,
			"house object",
			cancellationToken);
	}

	public Task<bool> RegisterHouseDecorationFromInventoryAsync(
		int playerObjectId,
		int sourceItemObjectId,
		RegisteredHouseDecorationSummary decoration,
		CancellationToken cancellationToken = default)
	{
		// Java parity: player.getInventory().delete(item, REGISTER) then PlayerRegisteredItemsDAO.storeDecors INSERT_QUERY.
		return RegisterHouseItemFromInventoryAsync(
			playerObjectId,
			sourceItemObjectId,
			decoration.ObjectId,
			decoration.TemplateId,
			null,
			null,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			"DECOR",
			decoration.Room,
			"house decoration",
			cancellationToken);
	}

	public async Task<bool> SaveHouseDecorationMutationAsync(
		int playerObjectId,
		IReadOnlyList<RegisteredHouseDecorationSummary> updatedDecorations,
		IReadOnlyList<int> deletedDecorationObjectIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PlayerRegisteredItemsDAO.storeDecors update/delete branches after HouseRegistry.setUsed/discardDecor.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var decoration in updatedDecorations)
			{
				await using var updateCommand = connection.CreateCommand();
				updateCommand.Transaction = transaction;
				updateCommand.CommandText = """
					UPDATE player_registered_items
					SET room = ?
					WHERE player_id = ? AND item_unique_id = ? AND area = 'DECOR'
					""";
				updateCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = decoration.Room },
						new MySqlParameter { Value = playerObjectId },
						new MySqlParameter { Value = decoration.ObjectId },
					});
				if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			foreach (var deletedObjectId in deletedDecorationObjectIds.Distinct())
			{
				await using var deleteCommand = connection.CreateCommand();
				deleteCommand.Transaction = transaction;
				deleteCommand.CommandText = "DELETE FROM player_registered_items WHERE player_id = ? AND item_unique_id = ? AND area = 'DECOR'";
				deleteCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = playerObjectId },
						new MySqlParameter { Value = deletedObjectId },
					});
				if (await deleteCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save house decoration mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveHouseRenovationAsync(
		int playerObjectId,
		int houseObjectId,
		int buildingId,
		IReadOnlyList<InventoryItem> updatedCouponItems,
		IReadOnlyList<int> deletedCouponItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_HOUSE_EDIT action 16 consumes the renovation coupon, then HousesDAO.storeHouse updates building_id.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await using (var houseCommand = connection.CreateCommand())
			{
				houseCommand.Transaction = transaction;
				houseCommand.CommandText = "UPDATE houses SET building_id = ? WHERE id = ? AND player_id = ?";
				houseCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = buildingId },
						new MySqlParameter { Value = houseObjectId },
						new MySqlParameter { Value = playerObjectId },
					});
				if (await houseCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			foreach (var couponItem in updatedCouponItems)
			{
				await using var couponCommand = connection.CreateCommand();
				couponCommand.Transaction = transaction;
				couponCommand.CommandText = "UPDATE inventory SET item_count = ? WHERE item_unique_id = ? AND item_owner = ?";
				couponCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = couponItem.Count },
						new MySqlParameter { Value = couponItem.ObjectId },
						new MySqlParameter { Value = playerObjectId },
					});
				if (await couponCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			foreach (var deletedCouponObjectId in deletedCouponItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedCouponObjectId, cancellationToken))
					return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not renovate house {HouseObjectId} to building {BuildingId} for player {PlayerObjectId}",
				houseObjectId,
				buildingId,
				playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveHouseObjectUseAsync(
		int houseOwnerObjectId,
		int usingPlayerObjectId,
		RegisteredHouseObjectSummary? updatedHouseObject,
		int? deletedHouseObjectId,
		IReadOnlyList<InventoryItem> updatedConsumedItems,
		IReadOnlyList<int> deletedConsumedObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: UseableItemObject scheduled completion mutates InventoryDAO and PlayerRegisteredItemsDAO.store in one use boundary.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var consumedItem in updatedConsumedItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, usingPlayerObjectId, consumedItem, cancellationToken))
					return false;
			}

			foreach (var deletedConsumedObjectId in deletedConsumedObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, usingPlayerObjectId, deletedConsumedObjectId, cancellationToken))
					return false;
			}

			foreach (var rewardItem in updatedRewardItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, usingPlayerObjectId, rewardItem, cancellationToken))
					return false;
			}

			foreach (var rewardItem in addedRewardItems)
				await InsertInventoryItemAsync(connection, transaction, rewardItem, cancellationToken);

			if (updatedHouseObject != null)
			{
				await using var updateCommand = connection.CreateCommand();
				updateCommand.Transaction = transaction;
				updateCommand.CommandText = """
					UPDATE player_registered_items
					SET expire_time = ?, owner_use_count = ?, visitor_use_count = ?
					WHERE player_id = ? AND item_unique_id = ? AND item_id = ?
					""";
				updateCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = updatedHouseObject.ExpireTimeSeconds > 0 ? updatedHouseObject.ExpireTimeSeconds : (object)DBNull.Value },
						new MySqlParameter { Value = updatedHouseObject.OwnerUseCount },
						new MySqlParameter { Value = updatedHouseObject.VisitorUseCount },
						new MySqlParameter { Value = houseOwnerObjectId },
						new MySqlParameter { Value = updatedHouseObject.ObjectId },
						new MySqlParameter { Value = updatedHouseObject.TemplateId },
					});
				if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			if (deletedHouseObjectId.HasValue)
			{
				await using var deleteCommand = connection.CreateCommand();
				deleteCommand.Transaction = transaction;
				deleteCommand.CommandText = "DELETE FROM player_registered_items WHERE item_unique_id = ? AND player_id = ?";
				deleteCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = deletedHouseObjectId.Value },
						new MySqlParameter { Value = houseOwnerObjectId },
					});
				if (await deleteCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not save house object use mutation for object {HouseObjectId} owned by {HouseOwnerObjectId}",
				updatedHouseObject?.ObjectId ?? deletedHouseObjectId ?? 0,
				houseOwnerObjectId);
			return false;
		}
	}

	public async Task<bool> DeleteHouseRegisteredObjectAsync(
		int playerObjectId,
		int itemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerRegisteredItemsDAO.DELETE_QUERY for deleted HouseObject rows.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM player_registered_items WHERE item_unique_id = ? AND player_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = itemObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not delete registered house object {HouseObjectId} for player {PlayerObjectId}",
				itemObjectId,
				playerObjectId);
			return false;
		}
	}

	private async Task<bool> RegisterHouseItemFromInventoryAsync(
		int playerObjectId,
		int sourceItemObjectId,
		int registeredItemObjectId,
		int registeredItemTemplateId,
		int? expireTimeSeconds,
		int? color,
		int colorExpires,
		int ownerUseCount,
		int visitorUseCount,
		float x,
		float y,
		float z,
		int heading,
		string area,
		int room,
		string itemKind,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, sourceItemObjectId, cancellationToken))
				return false;

			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = """
				INSERT INTO player_registered_items
					(expire_time, color, color_expires, owner_use_count, visitor_use_count, x, y, z, h, area, room, player_id, item_unique_id, item_id)
				VALUES
					(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = expireTimeSeconds.HasValue ? expireTimeSeconds.Value : DBNull.Value },
					new MySqlParameter { Value = color.HasValue ? color.Value : DBNull.Value },
					new MySqlParameter { Value = colorExpires },
					new MySqlParameter { Value = ownerUseCount },
					new MySqlParameter { Value = visitorUseCount },
					new MySqlParameter { Value = x },
					new MySqlParameter { Value = y },
					new MySqlParameter { Value = z },
					new MySqlParameter { Value = heading },
					new MySqlParameter { Value = area },
					new MySqlParameter { Value = room },
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = registeredItemObjectId },
					new MySqlParameter { Value = registeredItemTemplateId },
				});
			if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not register {ItemKind} {RegisteredItemObjectId} from inventory item {SourceItemObjectId} for player {PlayerObjectId}",
				itemKind,
				registeredItemObjectId,
				sourceItemObjectId,
				playerObjectId);
			return false;
		}
	}

	private static async Task<bool> SaveInventoryItemCountAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE inventory SET item_count = ? WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.Count },
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task InsertInventoryItemAsync(
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
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = item.ItemId },
				new MySqlParameter { Value = item.Count },
				new MySqlParameter { Value = item.Color.HasValue ? item.Color.Value : DBNull.Value },
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
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task<bool> DeleteInventoryItemAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		int itemObjectId,
		CancellationToken cancellationToken)
	{
		await using (var stoneCommand = connection.CreateCommand())
		{
			stoneCommand.Transaction = transaction;
			stoneCommand.CommandText = "DELETE FROM item_stones WHERE item_unique_id = ?";
			stoneCommand.Parameters.Add(new MySqlParameter { Value = itemObjectId });
			await stoneCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "DELETE FROM inventory WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = itemObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task RevokeDeletedOwnerHousesAsync(
		MySqlConnection connection,
		IEnumerable<HouseRow> rows,
		HousingTemplateTable housingTemplates,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		// Java parity: services/HousingService.revokeOwnershipOfDeletedPlayers -> changeOwner(house, 0).
		foreach (var row in rows.Where(row => row.HasDeletedOwner))
		{
			logger.LogWarning(
				"Player with ID {PlayerObjectId} got deleted from DB, revoking house ownership for house {AddressId}",
				row.OwnerObjectId,
				row.AddressId);
			var revoked = row.WithDeletedOwnerRevoked(housingTemplates);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				UPDATE houses
				SET building_id = ?, player_id = 0, acquire_time = NULL, settings = ?, next_pay = NULL, sign_notice = NULL
				WHERE id = ? AND player_id = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = revoked.BuildingId },
					new MySqlParameter { Value = revoked.Settings },
					new MySqlParameter { Value = row.ObjectId },
					new MySqlParameter { Value = row.OwnerObjectId },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static IEnumerable<WorldHouse> CreateWorldHouses(IEnumerable<HouseRow> rows, HousingTemplateTable housingTemplates)
	{
		var orderedRows = rows.OrderBy(row => row.AcquiredTime ?? DateTime.MinValue).ThenBy(row => row.AddressId).ToArray();
		for (var i = 0; i < orderedRows.Length; i++)
		{
			var row = orderedRows[i];
			var address = housingTemplates.GetAddress(row.AddressId);
			if (address == null || address.MapId == 0)
				continue;

			var isInactive = row.OwnerObjectId > 0 && i != 0;
			yield return new WorldHouse(
				row.ObjectId,
				row.AddressId,
				row.BuildingId,
				row.OwnerObjectId,
				row.OwnerName,
				row.LegionId,
				row.LegionName,
				row.LegionEmblemId,
				row.LegionEmblemType,
				row.LegionEmblemColorA,
				row.LegionEmblemColorR,
				row.LegionEmblemColorG,
				row.LegionEmblemColorB,
				isInactive,
				GetDoorState(row.Settings, row.OwnerObjectId, isInactive),
				PlayerHouse.GetShowOwnerNameFromSettings(row.Settings),
				row.SignNotice,
				new WorldPosition(address.MapId, address.X, address.Y, address.Z, 0));
		}
	}

	private static byte GetDoorState(int settings, int ownerObjectId, bool isInactive)
	{
		// Java parity: model/house/House.setPermissionsFromDB falls back to House.resetDoorState.
		var doorState = (byte)(settings >> 8);
		if (PlayerHouse.IsKnownDoorState(doorState))
			return doorState;

		return isInactive || ownerObjectId == 0
			? PlayerHouse.DoorClosed
			: PlayerHouse.DoorOpen;
	}

	private static int ReadInt(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
	}

	private static int? ReadNullableInt(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
	}

	private static float ReadFloat(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToSingle(reader.GetValue(ordinal));
	}

	private static DateTime? ReadDateTime(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
	}

	private static string ReadString(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
	}

	private static byte ToLegionEmblemTypeValue(string emblemType)
	{
		// Java parity: model/team/legion/LegionEmblemType values.
		return string.Equals(emblemType, "CUSTOM", StringComparison.OrdinalIgnoreCase) ? (byte)0x80 : (byte)0;
	}

	private sealed record HouseRow(
		int ObjectId,
		int AddressId,
		int BuildingId,
		int OwnerObjectId,
		DateTime? AcquiredTime,
		int Settings,
		string? SignNotice,
		bool OwnerExists,
		string OwnerName,
		int LegionId,
		string LegionName,
		byte LegionEmblemId,
		byte LegionEmblemType,
		byte LegionEmblemColorA,
		byte LegionEmblemColorR,
		byte LegionEmblemColorG,
		byte LegionEmblemColorB)
	{
		public bool HasDeletedOwner => OwnerObjectId > 0 && !OwnerExists;

		public HouseRow WithDeletedOwnerRevoked(HousingTemplateTable housingTemplates)
		{
			if (!HasDeletedOwner)
				return this;

			var defaultBuildingId = housingTemplates.GetAddress(AddressId)?.DefaultBuildingId ?? 0;
			return this with
			{
				BuildingId = defaultBuildingId == 0 ? BuildingId : defaultBuildingId,
				OwnerObjectId = 0,
				AcquiredTime = null,
				Settings = PlayerHouse.CreateSettings(PlayerHouse.DoorClosed, showOwnerName: true),
				SignNotice = null,
				OwnerName = string.Empty,
				LegionId = 0,
				LegionName = string.Empty,
				LegionEmblemId = 0,
				LegionEmblemType = 0,
				LegionEmblemColorA = 0,
				LegionEmblemColorR = 0,
				LegionEmblemColorG = 0,
				LegionEmblemColorB = 0,
			};
		}
	}
}
