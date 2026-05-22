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
}

public sealed class EmptyHousingRepository : IHousingRepository
{
	public Task<IReadOnlyList<WorldHouse>> LoadWorldHousesAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<WorldHouse>>(Array.Empty<WorldHouse>());
	}
}

public sealed class MySqlHousingRepository : IHousingRepository
{
	private readonly ILogger<MySqlHousingRepository> _logger;

	public MySqlHousingRepository(ILogger<MySqlHousingRepository> logger)
	{
		_logger = logger;
	}

	public async Task<IReadOnlyList<WorldHouse>> LoadWorldHousesAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
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
				WHERE h.address <> 2001 AND h.address <> 3001
				ORDER BY h.player_id, h.acquire_time, h.address
				""";

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

			return rows
				.GroupBy(row => row.OwnerObjectId)
				.SelectMany(group => CreateWorldHouses(group, housingTemplates))
				.ToArray();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load world houses");
			return Array.Empty<WorldHouse>();
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
		string SignNotice,
		string OwnerName,
		int LegionId,
		string LegionName,
		byte LegionEmblemId,
		byte LegionEmblemType,
		byte LegionEmblemColorA,
		byte LegionEmblemColorR,
		byte LegionEmblemColorG,
		byte LegionEmblemColorB);
}
