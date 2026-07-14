using Aion.Commons.Database;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface ICharacterSelectionRepository
{
	Task<IReadOnlyList<CharacterSelectionEntry>> LoadCharactersAsync(int accountId, CancellationToken cancellationToken = default);

	Task<int> GetCharacterCountAsync(int accountId, CancellationToken cancellationToken = default);

	Task<int> MarkCharacterForDeletionAsync(int accountId, int characterObjectId, TimeSpan deletionDelay, CancellationToken cancellationToken = default);

	Task<bool> RestoreCharacterAsync(int accountId, int characterObjectId, CancellationToken cancellationToken = default);
}

public sealed class EmptyCharacterSelectionRepository : ICharacterSelectionRepository
{
	public Task<IReadOnlyList<CharacterSelectionEntry>> LoadCharactersAsync(int accountId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<CharacterSelectionEntry>>(Array.Empty<CharacterSelectionEntry>());
	}

	public Task<int> GetCharacterCountAsync(int accountId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(0);
	}

	public Task<int> MarkCharacterForDeletionAsync(int accountId, int characterObjectId, TimeSpan deletionDelay, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(0);
	}

	public Task<bool> RestoreCharacterAsync(int accountId, int characterObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlCharacterSelectionRepository : ICharacterSelectionRepository
{
	private const int CubeStorageId = 0;
	private const int GodstoneCategory = 1;
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly ILogger<MySqlCharacterSelectionRepository> _logger;

	public MySqlCharacterSelectionRepository(
		GameServerRuntimeContext runtimeContext,
		ILogger<MySqlCharacterSelectionRepository> logger)
	{
		_runtimeContext = runtimeContext;
		_logger = logger;
	}

	public async Task<IReadOnlyList<CharacterSelectionEntry>> LoadCharactersAsync(int accountId, CancellationToken cancellationToken = default)
	{
		// Java parity: services/AccountService.loadAccountData + PlayerDAO.loadPlayerCommonData for select screen.
		try
		{
			var visibleItems = await LoadVisibleItemsAsync(accountId, cancellationToken);
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					p.id, p.name, p.exp, p.x, p.y, p.z, p.heading, p.world_id, p.gender, p.race, p.player_class,
					CAST(FLOOR(UNIX_TIMESTAMP(p.deletion_date) * 1000) AS SIGNED) AS deletion_date_epoch_millis,
					CAST(FLOOR(UNIX_TIMESTAMP(p.last_online) * 1000) AS SIGNED) AS last_online_epoch_millis,
					p.title_id,
					pa.face, pa.hair, pa.deco, pa.tattoo, pa.face_contour, pa.expression, pa.jaw_line,
					pa.skin_rgb, pa.hair_rgb, pa.eye_rgb, pa.lip_rgb, pa.face_shape, pa.forehead, pa.eye_height,
					pa.eye_space, pa.eye_width, pa.eye_size, pa.eye_shape, pa.eye_angle, pa.brow_height,
					pa.brow_angle, pa.brow_shape, pa.nose, pa.nose_bridge, pa.nose_width, pa.nose_tip,
					pa.cheek, pa.lip_height, pa.mouth_size, pa.lip_size, pa.smile, pa.lip_shape, pa.jaw_height,
					pa.chin_jut, pa.ear_shape, pa.head_size, pa.neck, pa.neck_length, pa.shoulders,
					pa.shoulder_size, pa.torso, pa.chest, pa.waist, pa.hips, pa.arm_thickness, pa.arm_length,
					pa.hand_size, pa.leg_thickness, pa.leg_length, pa.foot_size, pa.facial_rate, pa.voice, pa.height
				FROM players p
				LEFT JOIN player_appearance pa ON pa.player_id = p.id
				WHERE p.account_id = ? AND (p.deletion_date IS NULL OR p.deletion_date > CURRENT_TIMESTAMP)
				ORDER BY p.id
				""";
			command.Parameters.Add(new MySqlParameter { Value = accountId });

			var characters = new List<CharacterSelectionEntry>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var id = reader.GetInt32(reader.GetOrdinal("id"));
				var genderId = ToGenderId(ReadString(reader, "gender"));
				var raceId = ToRaceId(ReadString(reader, "race"));
				var exp = reader.GetInt64(reader.GetOrdinal("exp"));
				characters.Add(
					new CharacterSelectionEntry
					{
						ObjectId = id,
						Name = ReadString(reader, "name"),
						GenderId = genderId,
						RaceId = raceId,
						ClassId = ToClassId(ReadString(reader, "player_class")),
						Appearance = ReadAppearance(reader),
						TemplateId = 100000 + raceId * 2 + genderId,
						MapId = reader.GetInt32(reader.GetOrdinal("world_id")),
						X = reader.GetFloat(reader.GetOrdinal("x")),
						Y = reader.GetFloat(reader.GetOrdinal("y")),
						Z = reader.GetFloat(reader.GetOrdinal("z")),
						Heading = reader.GetInt32(reader.GetOrdinal("heading")),
						Level = Math.Max(1, _runtimeContext.DataManager?.StaticData.PlayerExperienceTable.GetLevelForExp(exp) ?? 1),
						TitleId = reader.GetInt32(reader.GetOrdinal("title_id")),
						LastOnlineEpochSeconds = DatabaseTimestamp.MillisecondsToInt32UnixTimeSeconds(ReadNullableLong(reader, "last_online_epoch_millis")),
						VisibleItems = visibleItems.TryGetValue(id, out var items) ? items : Array.Empty<VisibleCharacterItem>(),
						DeletionTimeSeconds = DatabaseTimestamp.MillisecondsToInt32UnixTimeSeconds(ReadNullableLong(reader, "deletion_date_epoch_millis")),
					});
			}

			return characters;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load character-selection data for account {AccountId}", accountId);
			return Array.Empty<CharacterSelectionEntry>();
		}
	}

	public async Task<int> GetCharacterCountAsync(int accountId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerDAO.getCharacterCountOnAccount.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT COUNT(*) FROM players WHERE account_id = ? AND (deletion_date IS NULL OR deletion_date > CURRENT_TIMESTAMP)";
			command.Parameters.Add(new MySqlParameter { Value = accountId });
			var result = await command.ExecuteScalarAsync(cancellationToken);
			return Convert.ToInt32(result);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load character count for account {AccountId}", accountId);
			return 0;
		}
	}

	public async Task<int> MarkCharacterForDeletionAsync(
		int accountId,
		int characterObjectId,
		TimeSpan deletionDelay,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/AccountService.deleteCharacter + dao/PlayerDAO.updateDeletionTime.
		try
		{
			var currentDeletionEpoch = await GetDeletionEpochSecondsAsync(accountId, characterObjectId, cancellationToken);
			var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			if (currentDeletionEpoch.HasValue)
				return currentDeletionEpoch.Value > nowEpoch ? unchecked((int)currentDeletionEpoch.Value) : 0;

			var deletionEpoch = DateTimeOffset.UtcNow.Add(deletionDelay).ToUnixTimeSeconds();
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE players SET deletion_date = FROM_UNIXTIME(?) WHERE id = ? AND account_id = ? AND deletion_date IS NULL";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = deletionEpoch },
					new MySqlParameter { Value = characterObjectId },
					new MySqlParameter { Value = accountId },
				});

			var rows = await command.ExecuteNonQueryAsync(cancellationToken);
			return rows > 0 ? unchecked((int)deletionEpoch) : 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not mark character {CharacterObjectId} for deletion", characterObjectId);
			return 0;
		}
	}

	public async Task<bool> RestoreCharacterAsync(int accountId, int characterObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: services/AccountService.restoreCharacter + PlayerDAO.updateDeletionTime(null).
		try
		{
			var currentDeletionEpoch = await GetDeletionEpochSecondsAsync(accountId, characterObjectId, cancellationToken);
			if (!currentDeletionEpoch.HasValue)
				return true;
			if (currentDeletionEpoch.Value <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
				return false;

			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE players SET deletion_date = NULL WHERE id = ? AND account_id = ? AND deletion_date > CURRENT_TIMESTAMP";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = characterObjectId },
					new MySqlParameter { Value = accountId },
				});

			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not restore character {CharacterObjectId}", characterObjectId);
			return false;
		}
	}

	private async Task<long?> GetDeletionEpochSecondsAsync(int accountId, int characterObjectId, CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerDAO.setCreationDeletionTime deletion_date read.
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT UNIX_TIMESTAMP(deletion_date) FROM players WHERE id = ? AND account_id = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = characterObjectId },
				new MySqlParameter { Value = accountId },
			});

		var result = await command.ExecuteScalarAsync(cancellationToken);
		return result == null || result == DBNull.Value ? null : Convert.ToInt64(result);
	}

	private async Task<Dictionary<int, IReadOnlyList<VisibleCharacterItem>>> LoadVisibleItemsAsync(int accountId, CancellationToken cancellationToken)
	{
		// Java parity: dao/InventoryDAO.loadVisibleEquipment.
		var byCharacter = new Dictionary<int, List<VisibleCharacterItem>>();
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT i.item_owner, i.item_skin, i.slot, i.item_color, s.item_id AS godstone_item_id
			FROM inventory i
			INNER JOIN players p ON p.id = i.item_owner
			LEFT JOIN item_stones s ON s.item_unique_id = i.item_unique_id AND s.slot = 0 AND s.category = ?
			WHERE p.account_id = ? AND i.item_location = ? AND i.is_equipped = 1
			ORDER BY i.item_owner, i.slot
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = GodstoneCategory },
				new MySqlParameter { Value = accountId },
				new MySqlParameter { Value = CubeStorageId },
			});

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var slotType = GetEquipmentSlotType(reader.GetInt64(reader.GetOrdinal("slot")));
			if (slotType == 0)
				continue;

			var ownerId = reader.GetInt32(reader.GetOrdinal("item_owner"));
			if (!byCharacter.TryGetValue(ownerId, out var list))
			{
				list = new List<VisibleCharacterItem>();
				byCharacter[ownerId] = list;
			}

			list.Add(
				new VisibleCharacterItem(
					slotType,
					reader.GetInt32(reader.GetOrdinal("item_skin")),
					ReadNullableInt(reader, "godstone_item_id") ?? 0,
					ReadNullableInt(reader, "item_color")));
		}

		return byCharacter.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<VisibleCharacterItem>)pair.Value);
	}

	private static CharacterAppearance ReadAppearance(MySqlDataReader reader)
	{
		// Java parity: dao/PlayerAppearanceDAO.loadPlayerAppearance.
		return new CharacterAppearance
		{
			Face = ReadInt(reader, "face"),
			Hair = ReadInt(reader, "hair"),
			Deco = ReadInt(reader, "deco"),
			Tattoo = ReadInt(reader, "tattoo"),
			FaceContour = ReadInt(reader, "face_contour"),
			Expression = ReadInt(reader, "expression"),
			JawLine = ReadInt(reader, "jaw_line"),
			SkinRgb = ReadInt(reader, "skin_rgb"),
			HairRgb = ReadInt(reader, "hair_rgb"),
			EyeRgb = ReadInt(reader, "eye_rgb"),
			LipRgb = ReadInt(reader, "lip_rgb"),
			FaceShape = ReadInt(reader, "face_shape"),
			Forehead = ReadInt(reader, "forehead"),
			EyeHeight = ReadInt(reader, "eye_height"),
			EyeSpace = ReadInt(reader, "eye_space"),
			EyeWidth = ReadInt(reader, "eye_width"),
			EyeSize = ReadInt(reader, "eye_size"),
			EyeShape = ReadInt(reader, "eye_shape"),
			EyeAngle = ReadInt(reader, "eye_angle"),
			BrowHeight = ReadInt(reader, "brow_height"),
			BrowAngle = ReadInt(reader, "brow_angle"),
			BrowShape = ReadInt(reader, "brow_shape"),
			Nose = ReadInt(reader, "nose"),
			NoseBridge = ReadInt(reader, "nose_bridge"),
			NoseWidth = ReadInt(reader, "nose_width"),
			NoseTip = ReadInt(reader, "nose_tip"),
			Cheek = ReadInt(reader, "cheek"),
			LipHeight = ReadInt(reader, "lip_height"),
			MouthSize = ReadInt(reader, "mouth_size"),
			LipSize = ReadInt(reader, "lip_size"),
			Smile = ReadInt(reader, "smile"),
			LipShape = ReadInt(reader, "lip_shape"),
			JawHeight = ReadInt(reader, "jaw_height"),
			ChinJut = ReadInt(reader, "chin_jut"),
			EarShape = ReadInt(reader, "ear_shape"),
			HeadSize = ReadInt(reader, "head_size"),
			Neck = ReadInt(reader, "neck"),
			NeckLength = ReadInt(reader, "neck_length"),
			Shoulders = ReadInt(reader, "shoulders"),
			ShoulderSize = ReadInt(reader, "shoulder_size"),
			Torso = ReadInt(reader, "torso"),
			Chest = ReadInt(reader, "chest"),
			Waist = ReadInt(reader, "waist"),
			Hips = ReadInt(reader, "hips"),
			ArmThickness = ReadInt(reader, "arm_thickness"),
			ArmLength = ReadInt(reader, "arm_length"),
			HandSize = ReadInt(reader, "hand_size"),
			LegThickness = ReadInt(reader, "leg_thickness"),
			LegLength = ReadInt(reader, "leg_length"),
			FootSize = ReadInt(reader, "foot_size"),
			FacialRate = ReadInt(reader, "facial_rate"),
			Voice = ReadInt(reader, "voice"),
			Height = ReadFloat(reader, "height"),
		};
	}

	private static int ReadInt(MySqlDataReader reader, string name)
	{
		var ordinal = reader.GetOrdinal(name);
		return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
	}

	private static int? ReadNullableInt(MySqlDataReader reader, string name)
	{
		var ordinal = reader.GetOrdinal(name);
		return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
	}

	private static float ReadFloat(MySqlDataReader reader, string name)
	{
		var ordinal = reader.GetOrdinal(name);
		return reader.IsDBNull(ordinal) ? 0 : reader.GetFloat(ordinal);
	}

	private static string ReadString(MySqlDataReader reader, string name)
	{
		var ordinal = reader.GetOrdinal(name);
		return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
	}

	private static long? ReadNullableLong(MySqlDataReader reader, string name)
	{
		var ordinal = reader.GetOrdinal(name);
		return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
	}

	private static int ToGenderId(string gender)
	{
		return string.Equals(gender, "FEMALE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}

	private static int ToRaceId(string race)
	{
		return string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}

	private static int ToClassId(string playerClass)
	{
		return playerClass.ToUpperInvariant() switch
		{
			"WARRIOR" => 0,
			"GLADIATOR" => 1,
			"TEMPLAR" => 2,
			"SCOUT" => 3,
			"ASSASSIN" => 4,
			"RANGER" => 5,
			"MAGE" => 6,
			"SORCERER" => 7,
			"SPIRIT_MASTER" => 8,
			"PRIEST" => 9,
			"CLERIC" => 10,
			"CHANTER" => 11,
			"ENGINEER" => 12,
			"RIDER" => 13,
			"GUNNER" => 14,
			"ARTIST" => 15,
			"BARD" => 16,
			_ => 0,
		};
	}

	private static byte GetEquipmentSlotType(long slot)
	{
		// Java parity: model/items/ItemSlot.getEquipmentSlotType.
		if ((VisibleSlotMask & slot) != slot)
			return 0;

		return (slot & LeftSlotMask) == 0 || IsTwoHandedWeapon(slot) ? (byte)1 : (byte)2;
	}

	private static bool IsTwoHandedWeapon(long slot)
	{
		return (slot & MainOrSubMask) == MainOrSubMask || (slot & MainOffOrSubOffMask) == MainOffOrSubOffMask;
	}

	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long Helmet = 1L << 2;
	private const long Torso = 1L << 3;
	private const long Gloves = 1L << 4;
	private const long Boots = 1L << 5;
	private const long EarringsLeft = 1L << 6;
	private const long EarringsRight = 1L << 7;
	private const long Necklace = 1L << 10;
	private const long Shoulder = 1L << 11;
	private const long Pants = 1L << 12;
	private const long PowerShardRight = 1L << 13;
	private const long PowerShardLeft = 1L << 14;
	private const long Wings = 1L << 15;
	private const long MainOffHand = 1L << 17;
	private const long SubOffHand = 1L << 18;
	private const long Plume = 1L << 19;
	private const long MainOrSubMask = MainHand | SubHand;
	private const long MainOffOrSubOffMask = MainOffHand | SubOffHand;
	private const long LeftSlotMask = SubHand | EarringsLeft | PowerShardLeft | SubOffHand;
	private const long VisibleSlotMask = MainHand | SubHand | Helmet | Torso | Gloves | Boots | EarringsLeft | EarringsRight | Necklace | Shoulder | Pants
		| PowerShardRight | PowerShardLeft | Wings | Plume;
}
