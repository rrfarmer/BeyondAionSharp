using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IHousingRepository
{
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

	Task<bool> DeleteHouseRegisteredObjectAsync(
		int playerObjectId,
		int itemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> StoreHouseScriptAsync(
		int houseObjectId,
		int scriptId,
		string scriptXml,
		CancellationToken cancellationToken = default);

	Task<bool> DeleteHouseScriptAsync(
		int houseObjectId,
		int scriptId,
		CancellationToken cancellationToken = default);
}

public sealed class EmptyHousingRepository : IHousingRepository
{
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

	public Task<bool> DeleteHouseRegisteredObjectAsync(
		int playerObjectId,
		int itemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> StoreHouseScriptAsync(
		int houseObjectId,
		int scriptId,
		string scriptXml,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> DeleteHouseScriptAsync(
		int houseObjectId,
		int scriptId,
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

	public async Task<bool> StoreHouseScriptAsync(
		int houseObjectId,
		int scriptId,
		string scriptXml,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/HouseScriptsDAO.storeScript.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				INSERT INTO house_scripts (house_id, script_id, script)
				VALUES (?, ?, ?)
				ON DUPLICATE KEY UPDATE house_id = VALUES(house_id), script_id = VALUES(script_id), script = VALUES(script)
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = houseObjectId },
					new MySqlParameter { Value = scriptId },
					new MySqlParameter { Value = scriptXml },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save script data for houseId: {HouseObjectId}", houseObjectId);
			return false;
		}
	}

	public async Task<bool> DeleteHouseScriptAsync(
		int houseObjectId,
		int scriptId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/HouseScriptsDAO.deleteScript.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM house_scripts WHERE house_id = ? AND script_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = houseObjectId },
					new MySqlParameter { Value = scriptId },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete script for houseId: {HouseObjectId}", houseObjectId);
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

}
