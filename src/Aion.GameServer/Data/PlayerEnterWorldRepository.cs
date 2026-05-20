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

	Task<IReadOnlyList<InventoryItem>> LoadPlayerWarehouseItemsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<InventoryItem>> LoadAccountWarehouseItemsAsync(int accountId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerSkill>> LoadPlayerSkillsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, long>> LoadPlayerSkillCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, PlayerItemCooldown>> LoadPlayerItemCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerQuestState>> LoadPlayerQuestsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerTitle>> LoadPlayerTitlesAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerEmotion>> LoadPlayerEmotionsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<int>> LoadPlayerRecipesAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerMacro>> LoadPlayerMacrosAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerMail>> LoadPlayerMailboxAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerBrokerSettlementSummary> LoadBrokerSettlementsAsync(int playerObjectId, string race, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerHouse>> LoadPlayerHousesAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, long>> LoadPlayerCraftCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, PlayerPortalCooldown>> LoadPlayerPortalCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerLifeStats?> LoadPlayerLifeStatsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerFriend>> LoadPlayerFriendsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerBlockedUser>> LoadPlayerBlockedUsersAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerAbyssRank> LoadPlayerAbyssRankAsync(int playerObjectId, CancellationToken cancellationToken = default);

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

	public Task<IReadOnlyList<InventoryItem>> LoadPlayerWarehouseItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<InventoryItem>>(Array.Empty<InventoryItem>());
	}

	public Task<IReadOnlyList<InventoryItem>> LoadAccountWarehouseItemsAsync(int accountId, CancellationToken cancellationToken = default)
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

	public Task<IReadOnlyList<PlayerTitle>> LoadPlayerTitlesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerTitle>>(Array.Empty<PlayerTitle>());
	}

	public Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerMotion>>(Array.Empty<PlayerMotion>());
	}

	public Task<IReadOnlyList<PlayerEmotion>> LoadPlayerEmotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerEmotion>>(Array.Empty<PlayerEmotion>());
	}

	public Task<IReadOnlyList<int>> LoadPlayerRecipesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
	}

	public Task<IReadOnlyList<PlayerMacro>> LoadPlayerMacrosAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerMacro>>(Array.Empty<PlayerMacro>());
	}

	public Task<IReadOnlyList<PlayerMail>> LoadPlayerMailboxAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerMail>>(Array.Empty<PlayerMail>());
	}

	public Task<PlayerBrokerSettlementSummary> LoadBrokerSettlementsAsync(int playerObjectId, string race, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(PlayerBrokerSettlementSummary.Empty);
	}

	public Task<IReadOnlyList<PlayerHouse>> LoadPlayerHousesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerHouse>>(Array.Empty<PlayerHouse>());
	}

	public Task<IReadOnlyDictionary<int, long>> LoadPlayerCraftCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, long>>(new Dictionary<int, long>());
	}

	public Task<IReadOnlyDictionary<int, PlayerPortalCooldown>> LoadPlayerPortalCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, PlayerPortalCooldown>>(new Dictionary<int, PlayerPortalCooldown>());
	}

	public Task<PlayerLifeStats?> LoadPlayerLifeStatsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<PlayerLifeStats?>(null);
	}

	public Task<IReadOnlyList<PlayerFriend>> LoadPlayerFriendsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerFriend>>(Array.Empty<PlayerFriend>());
	}

	public Task<IReadOnlyList<PlayerBlockedUser>> LoadPlayerBlockedUsersAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerBlockedUser>>(Array.Empty<PlayerBlockedUser>());
	}

	public Task<PlayerAbyssRank> LoadPlayerAbyssRankAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(PlayerAbyssRank.Default());
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
				SELECT id, account_id, name, player_class, race, gender, exp, recoverexp, dp, reposte_energy, online, last_online,
					quest_expands, npc_expands, item_expands, wh_npc_expands, wh_bonus_expands, title_id, bonus_title_id,
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
				RecoverableExp = ReadLong(reader, "recoverexp"),
				Dp = ReadInt(reader, "dp"),
				ReposeEnergy = ReadLong(reader, "reposte_energy"),
				IsOnline = ReadBoolean(reader, "online"),
				LastOnline = ReadDateTime(reader, "last_online"),
				NpcExpands = ReadInt(reader, "npc_expands"),
				QuestExpands = ReadInt(reader, "quest_expands"),
				ItemExpands = ReadInt(reader, "item_expands"),
				WarehouseNpcExpands = ReadInt(reader, "wh_npc_expands"),
				WarehouseBonusExpands = ReadInt(reader, "wh_bonus_expands"),
				TitleId = ReadInt(reader, "title_id"),
				BonusTitleId = ReadInt(reader, "bonus_title_id"),
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
		// Java parity: dao/InventoryDAO.loadStorage for StorageType.CUBE.
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
				WHERE item_owner = ? AND item_location = 0
				ORDER BY item_location, slot, item_unique_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var items = new List<InventoryItem>();
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
					items.Add(ReadItem(reader));
			}

			await LoadItemStonesForItemsAsync(connection, items, cancellationToken);
			return items;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load inventory items for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<InventoryItem>();
		}
	}

	public async Task<IReadOnlyList<InventoryItem>> LoadPlayerWarehouseItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.loadStorage for StorageType.REGULAR_WAREHOUSE.
		return await LoadStorageItemsAsync(
			ownerId: playerObjectId,
			location: 1,
			playerObjectId,
			"regular warehouse",
			cancellationToken);
	}

	public async Task<IReadOnlyList<InventoryItem>> LoadAccountWarehouseItemsAsync(int accountId, CancellationToken cancellationToken = default)
	{
		// Java parity: services/AccountService.loadAccountWarehouse + InventoryDAO.loadStorage(accountId, ACCOUNT_WAREHOUSE).
		return await LoadStorageItemsAsync(
			ownerId: accountId,
			location: 2,
			accountId,
			"account warehouse",
			cancellationToken);
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

	public async Task<IReadOnlyList<PlayerTitle>> LoadPlayerTitlesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerTitleListDAO.loadTitleList.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT title_id, remaining
				FROM player_titles
				WHERE player_id = ?
				ORDER BY title_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var titles = new List<PlayerTitle>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				titles.Add(new PlayerTitle(ReadInt(reader, "title_id"), ReadInt(reader, "remaining")));

			return titles;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load titles for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerTitle>();
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

	public async Task<IReadOnlyList<PlayerEmotion>> LoadPlayerEmotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerEmotionListDAO.loadEmotions.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT emotion, remaining
				FROM player_emotions
				WHERE player_id = ?
				ORDER BY emotion
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var emotions = new List<PlayerEmotion>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				emotions.Add(new PlayerEmotion(ReadInt(reader, "emotion"), ReadInt(reader, "remaining")));

			return emotions;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load emotions for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerEmotion>();
		}
	}

	public async Task<IReadOnlyList<int>> LoadPlayerRecipesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerRecipesDAO.load.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT recipe_id
				FROM player_recipes
				WHERE player_id = ?
				ORDER BY recipe_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var recipes = new List<int>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				recipes.Add(ReadInt(reader, "recipe_id"));

			return recipes;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load recipes for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<int>();
		}
	}

	public async Task<IReadOnlyList<PlayerMacro>> LoadPlayerMacrosAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerMacrosDAO.loadMacros.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT `order`, `macro` FROM player_macrosses WHERE player_id = ? ORDER BY `order`";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var macros = new List<PlayerMacro>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				macros.Add(new PlayerMacro(ReadInt(reader, "order"), ReadString(reader, "macro")));
			return macros;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load macros for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerMacro>();
		}
	}

	public async Task<IReadOnlyList<PlayerMail>> LoadPlayerMailboxAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/MailDAO.loadPlayerMailbox.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					m.mail_unique_id, m.mail_recipient_id, m.sender_name, m.mail_title, m.mail_message, m.unread,
					m.attached_item_id, COALESCE(i.item_id, 0) AS attached_item_template_id,
					m.attached_kinah_count, m.express, m.recieved_time,
					i.item_unique_id, i.item_id, i.item_count, i.item_color, i.color_expires, i.item_creator, i.expire_time, i.activation_count,
					i.item_owner, i.is_equipped, i.is_soul_bound, i.slot, i.item_location, i.enchant, i.enchant_bonus, i.item_skin, i.fusioned_item,
					i.optional_socket, i.optional_fusion_socket, i.charge, i.tune_count, i.rnd_bonus, i.fusion_rnd_bonus, i.tempering, i.pack_count,
					i.is_amplified, i.buff_skill, i.rnd_plume_bonus
				FROM mail m
				LEFT JOIN inventory i ON i.item_unique_id = m.attached_item_id AND i.item_owner = m.mail_recipient_id AND i.item_location = 127
				WHERE m.mail_recipient_id = ?
				ORDER BY m.recieved_time
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var mailbox = new List<PlayerMail>();
			var attachedItems = new List<InventoryItem>();
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					InventoryItem? attachedItem = null;
					if (!reader.IsDBNull(reader.GetOrdinal("item_unique_id")))
					{
						attachedItem = ReadItem(reader);
						attachedItems.Add(attachedItem);
					}

					mailbox.Add(
						new PlayerMail(
							ReadInt(reader, "mail_unique_id"),
							ReadInt(reader, "mail_recipient_id"),
							ReadString(reader, "sender_name"),
							ReadString(reader, "mail_title"),
							ReadString(reader, "mail_message"),
							ReadBoolean(reader, "unread"),
							ReadInt(reader, "attached_item_id"),
							attachedItem?.ItemId ?? ReadInt(reader, "attached_item_template_id"),
							ReadLong(reader, "attached_kinah_count"),
							ReadInt(reader, "express"),
							ReadDateTime(reader, "recieved_time") ?? DateTime.MinValue,
							attachedItem));
				}
			}

			await LoadItemStonesForItemsAsync(connection, attachedItems, cancellationToken);
			return mailbox;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load mailbox for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerMail>();
		}
	}

	public async Task<PlayerBrokerSettlementSummary> LoadBrokerSettlementsAsync(
		int playerObjectId,
		string race,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.onPlayerLogin.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null)
			return PlayerBrokerSettlementSummary.Empty;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT COUNT(*) AS settled_count,
					COALESCE(SUM(CASE WHEN is_sold THEN price * item_count ELSE 0 END), 0) AS earned_kinah
				FROM broker
				WHERE seller_id = ? AND broker_race = ? AND is_settled = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = brokerRace },
					new MySqlParameter { Value = true },
				});

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return PlayerBrokerSettlementSummary.Empty;

			return new PlayerBrokerSettlementSummary(
				ReadInt(reader, "settled_count"),
				ReadLong(reader, "earned_kinah"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load broker settlement summary for player {PlayerObjectId}", playerObjectId);
			return PlayerBrokerSettlementSummary.Empty;
		}
	}

	public async Task<IReadOnlyList<PlayerHouse>> LoadPlayerHousesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingService.findPlayerHouses with HousesDAO.loadHouses startup state.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT id, address, building_id, acquire_time, next_pay
				FROM houses
				WHERE player_id = ?
				ORDER BY acquire_time, address
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var houses = new List<PlayerHouse>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				houses.Add(
					new PlayerHouse(
						ReadInt(reader, "id"),
						ReadInt(reader, "address"),
						ReadInt(reader, "building_id"),
						ReadDateTime(reader, "acquire_time"),
						ReadDateTime(reader, "next_pay"),
						IsInactive: false));
			}

			var studio = houses.FirstOrDefault(IsStudioAddress);
			if (studio != null)
				return [studio];

			var ordered = houses
				.OrderBy(house => house.AcquiredTime ?? DateTime.MinValue)
				.ThenBy(house => house.AddressId)
				.ToArray();
			for (var i = 0; i < ordered.Length; i++)
				ordered[i] = ordered[i] with { IsInactive = i != 0 };
			return ordered;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load houses for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerHouse>();
		}
	}

	public async Task<IReadOnlyDictionary<int, long>> LoadPlayerCraftCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/CraftCooldownsDAO.loadCraftCooldowns through model/gameobjects/player/Cooldowns.put.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT delay_id, reuse_time FROM craft_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, long>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseTime = ReadLong(reader, "reuse_time");
				if (reuseTime <= nowMillis)
					continue;

				cooldowns[ReadInt(reader, "delay_id")] = reuseTime;
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load craft cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, long>();
		}
	}

	public async Task<IReadOnlyDictionary<int, PlayerPortalCooldown>> LoadPlayerPortalCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PortalCooldownsDAO.loadPortalCooldowns.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT world_id, reuse_time, entry_count FROM portal_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, PlayerPortalCooldown>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseTime = ReadLong(reader, "reuse_time");
				if (reuseTime <= nowMillis)
					continue;

				var worldId = ReadInt(reader, "world_id");
				cooldowns[worldId] = new PlayerPortalCooldown(
					worldId,
					reuseTime,
					ReadInt(reader, "entry_count"));
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load portal cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, PlayerPortalCooldown>();
		}
	}

	public async Task<PlayerLifeStats?> LoadPlayerLifeStatsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerLifeStatsDAO.loadPlayerLifeStat.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT hp, mp, fp FROM player_life_stats WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return null;

			return new PlayerLifeStats(
				ReadInt(reader, "hp"),
				ReadInt(reader, "mp"),
				ReadInt(reader, "fp"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load life stats for player {PlayerObjectId}", playerObjectId);
			return null;
		}
	}

	public async Task<IReadOnlyList<PlayerFriend>> LoadPlayerFriendsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/FriendListDAO.load plus PlayerService.getOrLoadPlayerCommonData.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					f.friend, f.memo,
					p.name, p.exp, p.player_class, p.gender, p.world_id, p.last_online, p.note, p.online
				FROM friends f
				JOIN players p ON p.id = f.friend
				WHERE f.player = ?
				ORDER BY f.friend
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var friends = new List<PlayerFriend>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				friends.Add(
					new PlayerFriend(
						ReadInt(reader, "friend"),
						ReadString(reader, "name"),
						ReadLong(reader, "exp"),
						ReadString(reader, "player_class"),
						ReadString(reader, "gender"),
						ReadInt(reader, "world_id"),
						ReadDateTime(reader, "last_online"),
						ReadString(reader, "note"),
						ReadString(reader, "memo"),
						ReadBoolean(reader, "online")));
			}

			return friends;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load friends for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerFriend>();
		}
	}

	public async Task<IReadOnlyList<PlayerBlockedUser>> LoadPlayerBlockedUsersAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/BlockListDAO.load plus PlayerService.getPlayerName.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT b.blocked_player, b.reason, p.name
				FROM blocks b
				JOIN players p ON p.id = b.blocked_player
				WHERE b.player = ?
				ORDER BY b.blocked_player
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var blockedUsers = new List<PlayerBlockedUser>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				blockedUsers.Add(
					new PlayerBlockedUser(
						ReadInt(reader, "blocked_player"),
						ReadString(reader, "name"),
						ReadString(reader, "reason")));
			}

			return blockedUsers;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load block list for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerBlockedUser>();
		}
	}

	public async Task<PlayerAbyssRank> LoadPlayerAbyssRankAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/AbyssRankDAO.loadAbyssRank.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT daily_ap, weekly_ap, ap, daily_gp, weekly_gp, gp, `rank`, daily_kill, weekly_kill,
					all_kill, max_rank, last_kill, last_ap, last_gp, rank_pos
				FROM abyss_rank
				WHERE player_id = ?
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return PlayerAbyssRank.Default();

			return new PlayerAbyssRank(
				ReadInt(reader, "daily_ap"),
				ReadInt(reader, "weekly_ap"),
				ReadInt(reader, "ap"),
				ReadInt(reader, "daily_gp"),
				ReadInt(reader, "weekly_gp"),
				ReadInt(reader, "gp"),
				ReadInt(reader, "rank"),
				ReadInt(reader, "daily_kill"),
				ReadInt(reader, "weekly_kill"),
				ReadInt(reader, "all_kill"),
				ReadInt(reader, "max_rank"),
				ReadInt(reader, "last_kill"),
				ReadInt(reader, "last_ap"),
				ReadInt(reader, "last_gp"),
				ReadInt(reader, "rank_pos"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load abyss rank for player {PlayerObjectId}", playerObjectId);
			return PlayerAbyssRank.Default();
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

	private async Task<IReadOnlyList<InventoryItem>> LoadStorageItemsAsync(
		int ownerId,
		int location,
		int logOwnerId,
		string storageName,
		CancellationToken cancellationToken)
	{
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
				WHERE item_owner = ? AND item_location = ?
				ORDER BY slot, item_unique_id
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = ownerId },
					new MySqlParameter { Value = location },
				});

			var items = new List<InventoryItem>();
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
					items.Add(ReadItem(reader));
			}

			await LoadItemStonesForItemsAsync(connection, items, cancellationToken);
			return items;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load {StorageName} items for owner {OwnerId}", storageName, logOwnerId);
			return Array.Empty<InventoryItem>();
		}
	}

	private static async Task LoadItemStonesForItemsAsync(
		MySqlConnection connection,
		IReadOnlyList<InventoryItem> items,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/ItemStoneListDAO.load(Collection<Item>) after InventoryDAO.loadStorage.
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
					item.IdianStone = new PlayerIdianStone(
						itemId,
						ReadInt(reader, "polishNumber"),
						ReadInt(reader, "polishCharge"));
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

	private static void AddStone(Dictionary<int, List<ItemStoneSocket>> stonesByItem, int itemObjectId, ItemStoneSocket stone)
	{
		if (!stonesByItem.TryGetValue(itemObjectId, out var stones))
		{
			stones = [];
			stonesByItem[itemObjectId] = stones;
		}

		stones.Add(stone);
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

	private static string? GetBrokerRace(string race)
	{
		return race switch
		{
			"ELYOS" => "ELYOS",
			"ASMODIANS" => "ASMODIAN",
			_ => null,
		};
	}

	private static bool IsStudioAddress(PlayerHouse house)
	{
		return house.AddressId is 2001 or 3001;
	}
}
