using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IPlayerEnterWorldRepository
{
	Task<Player?> LoadPlayerAsync(int accountId, int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<InventoryItem>> LoadPlayerItemsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerSkill>> LoadPlayerSkillsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, long>> LoadPlayerSkillCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, PlayerItemCooldown>> LoadPlayerItemCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerQuestState>> LoadPlayerQuestsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerSettings> LoadPlayerSettingsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerBindPoint?> LoadPlayerBindPointAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<bool> MarkPlayerOnlineAsync(int playerObjectId, DateTime lastOnline, CancellationToken cancellationToken = default);
}

public sealed class EmptyPlayerEnterWorldRepository : IPlayerEnterWorldRepository
{
	public Task<Player?> LoadPlayerAsync(int accountId, int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<Player?>(null);
	}

	public Task<IReadOnlyList<InventoryItem>> LoadPlayerItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<InventoryItem>>(Array.Empty<InventoryItem>());
	}

	public Task<IReadOnlyList<PlayerSkill>> LoadPlayerSkillsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerSkill>>(Array.Empty<PlayerSkill>());
	}

	public Task<IReadOnlyDictionary<int, long>> LoadPlayerSkillCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, long>>(new Dictionary<int, long>());
	}

	public Task<IReadOnlyDictionary<int, PlayerItemCooldown>> LoadPlayerItemCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, PlayerItemCooldown>>(new Dictionary<int, PlayerItemCooldown>());
	}

	public Task<IReadOnlyList<PlayerQuestState>> LoadPlayerQuestsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerQuestState>>(Array.Empty<PlayerQuestState>());
	}

	public Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerMotion>>(Array.Empty<PlayerMotion>());
	}

	public Task<PlayerSettings> LoadPlayerSettingsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new PlayerSettings());
	}

	public Task<PlayerBindPoint?> LoadPlayerBindPointAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<PlayerBindPoint?>(null);
	}

	public Task<bool> MarkPlayerOnlineAsync(int playerObjectId, DateTime lastOnline, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlPlayerEnterWorldRepository : IPlayerEnterWorldRepository
{
	private readonly ILogger<MySqlPlayerEnterWorldRepository> _logger;

	public MySqlPlayerEnterWorldRepository(ILogger<MySqlPlayerEnterWorldRepository> logger)
	{
		_logger = logger;
	}

	public async Task<Player?> LoadPlayerAsync(int accountId, int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerDAO.loadPlayerCommonData, scoped to the authenticated account.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT id, account_id, name, player_class, race, gender, exp, online, last_online,
					quest_expands, npc_expands, item_expands, title_id,
					world_id, x, y, z, heading
				FROM players
				WHERE id = ? AND account_id = ? AND (deletion_date IS NULL OR deletion_date > CURRENT_TIMESTAMP)
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = accountId },
				});

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return null;

			return new Player
			{
				ObjectId = reader.GetInt32(reader.GetOrdinal("id")),
				AccountId = reader.GetInt32(reader.GetOrdinal("account_id")),
				Name = ReadString(reader, "name"),
				PlayerClass = ReadString(reader, "player_class"),
				Race = ReadString(reader, "race"),
				Gender = ReadString(reader, "gender"),
				Exp = reader.GetInt64(reader.GetOrdinal("exp")),
				IsOnline = ReadBoolean(reader, "online"),
				LastOnline = ReadDateTime(reader, "last_online"),
				NpcExpands = ReadInt(reader, "npc_expands"),
				QuestExpands = ReadInt(reader, "quest_expands"),
				ItemExpands = ReadInt(reader, "item_expands"),
				TitleId = ReadInt(reader, "title_id"),
				Position = new WorldPosition(
					ReadInt(reader, "world_id"),
					reader.GetFloat(reader.GetOrdinal("x")),
					reader.GetFloat(reader.GetOrdinal("y")),
					reader.GetFloat(reader.GetOrdinal("z")),
					(byte)ReadInt(reader, "heading")),
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load player {PlayerObjectId} for enter-world", playerObjectId);
			return null;
		}
	}

	public async Task<bool> MarkPlayerOnlineAsync(int playerObjectId, DateTime lastOnline, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerDAO.onlinePlayer + PlayerDAO.storeLastOnlineTime.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var onlineCommand = connection.CreateCommand();
			onlineCommand.CommandText = "UPDATE players SET online = ? WHERE id = ?";
			onlineCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = true },
					new MySqlParameter { Value = playerObjectId },
				});
			var onlineRows = await onlineCommand.ExecuteNonQueryAsync(cancellationToken);
			if (onlineRows <= 0)
				return false;

			await using var lastOnlineCommand = connection.CreateCommand();
			lastOnlineCommand.CommandText = "UPDATE players SET last_online = ? WHERE id = ?";
			lastOnlineCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = lastOnline },
					new MySqlParameter { Value = playerObjectId },
				});
			return await lastOnlineCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not mark player {PlayerObjectId} online", playerObjectId);
			return false;
		}
	}

	public async Task<IReadOnlyList<InventoryItem>> LoadPlayerItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.loadStorage for player-owned non-account-warehouse storage.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					item_unique_id, item_id, item_count, item_color, color_expires, item_creator, expire_time, activation_count,
					item_owner, is_equipped, is_soul_bound, slot, item_location, enchant, enchant_bonus, item_skin, fusioned_item,
					optional_socket, optional_fusion_socket, charge, tune_count, rnd_bonus, fusion_rnd_bonus, tempering, pack_count,
					is_amplified, buff_skill, rnd_plume_bonus
				FROM inventory
				WHERE item_owner = ? AND item_location <> 2
				ORDER BY item_location, slot, item_unique_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var items = new List<InventoryItem>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				items.Add(ReadItem(reader));
			return items;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load inventory items for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<InventoryItem>();
		}
	}

	public async Task<IReadOnlyList<PlayerSkill>> LoadPlayerSkillsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerSkillListDAO.loadSkillList.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT skill_id, skill_level FROM player_skills WHERE player_id = ? ORDER BY skill_id";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var skills = new List<PlayerSkill>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				skills.Add(
					new PlayerSkill
					{
						SkillId = ReadInt(reader, "skill_id"),
						SkillLevel = ReadInt(reader, "skill_level"),
						SkillType = 0,
					});
			}

			return skills;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load skill list for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerSkill>();
		}
	}

	public async Task<IReadOnlyDictionary<int, long>> LoadPlayerSkillCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerCooldownsDAO.loadPlayerCooldowns.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT cooldown_id, reuse_delay FROM player_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, long>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseDelay = ReadLong(reader, "reuse_delay");
				if (reuseDelay <= nowMillis)
					continue;

				cooldowns[ReadInt(reader, "cooldown_id")] = reuseDelay;
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load skill cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, long>();
		}
	}

	public async Task<IReadOnlyDictionary<int, PlayerItemCooldown>> LoadPlayerItemCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/ItemCooldownsDAO.loadItemCooldowns.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT delay_id, use_delay, reuse_time FROM item_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, PlayerItemCooldown>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseTime = ReadLong(reader, "reuse_time");
				if (reuseTime <= nowMillis)
					continue;

				cooldowns[ReadInt(reader, "delay_id")] = new PlayerItemCooldown(
					reuseTime,
					ReadInt(reader, "use_delay"));
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load item cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, PlayerItemCooldown>();
		}
	}

	public async Task<IReadOnlyList<PlayerQuestState>> LoadPlayerQuestsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerQuestListDAO.load.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT quest_id, status, quest_vars, flags, complete_count
				FROM player_quests
				WHERE player_id = ?
				ORDER BY quest_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var quests = new List<PlayerQuestState>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				quests.Add(
					new PlayerQuestState(
						ReadInt(reader, "quest_id"),
						ReadString(reader, "status"),
						ReadInt(reader, "quest_vars"),
						ReadInt(reader, "flags"),
						ReadInt(reader, "complete_count")));
			}

			return quests;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load quests for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerQuestState>();
		}
	}

	public async Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/MotionDAO.loadMotionList.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT motion_id, active, time
				FROM player_motions
				WHERE player_id = ?
				ORDER BY motion_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var motions = new List<PlayerMotion>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				motions.Add(
					new PlayerMotion(
						ReadInt(reader, "motion_id"),
						ReadInt(reader, "time"),
						ReadBoolean(reader, "active")));
			}

			return motions;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load motions for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerMotion>();
		}
	}

	public async Task<PlayerSettings> LoadPlayerSettingsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerSettingsDAO.loadSettings, scoped to client setting blobs.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT settings_type, settings FROM player_settings WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			byte[]? uiSettings = null;
			byte[]? shortcuts = null;
			byte[]? houseBuddies = null;
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var settings = ReadBytes(reader, "settings");
				switch (ReadInt(reader, "settings_type"))
				{
					case 0:
						uiSettings = settings;
						break;
					case 1:
						shortcuts = settings;
						break;
					case 2:
						houseBuddies = settings;
						break;
				}
			}

			return new PlayerSettings
			{
				UiSettings = uiSettings,
				Shortcuts = shortcuts,
				HouseBuddies = houseBuddies,
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load settings for player {PlayerObjectId}", playerObjectId);
			return new PlayerSettings();
		}
	}

	public async Task<PlayerBindPoint?> LoadPlayerBindPointAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerBindPointDAO.loadBindPoint.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT map_id, x, y, z, heading FROM player_bind_point WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return null;

			return new PlayerBindPoint(
				ReadInt(reader, "map_id"),
				reader.GetFloat(reader.GetOrdinal("x")),
				reader.GetFloat(reader.GetOrdinal("y")),
				reader.GetFloat(reader.GetOrdinal("z")),
				(byte)ReadInt(reader, "heading"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load bind point for player {PlayerObjectId}", playerObjectId);
			return null;
		}
	}

	private static string ReadString(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
	}

	private static DateTime? ReadDateTime(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
	}

	private static InventoryItem ReadItem(MySqlDataReader reader)
	{
		// Java parity: dao/InventoryDAO.constructItem column mapping.
		return new InventoryItem
		{
			ObjectId = ReadInt(reader, "item_unique_id"),
			ItemId = ReadInt(reader, "item_id"),
			Count = reader.GetInt64(reader.GetOrdinal("item_count")),
			Color = ReadNullableInt(reader, "item_color"),
			ColorExpires = ReadInt(reader, "color_expires"),
			Creator = ReadNullableString(reader, "item_creator"),
			ExpireTime = ReadInt(reader, "expire_time"),
			ActivationCount = ReadInt(reader, "activation_count"),
			OwnerId = ReadInt(reader, "item_owner"),
			IsEquipped = ReadBoolean(reader, "is_equipped"),
			IsSoulBound = ReadBoolean(reader, "is_soul_bound"),
			Slot = reader.GetInt64(reader.GetOrdinal("slot")),
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

	private static string? ReadNullableString(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
	}

	private static byte[] ReadBytes(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? Array.Empty<byte>() : (byte[])reader.GetValue(ordinal);
	}

	private static bool ReadBoolean(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return !reader.IsDBNull(ordinal) && Convert.ToInt32(reader.GetValue(ordinal)) != 0;
	}
}
