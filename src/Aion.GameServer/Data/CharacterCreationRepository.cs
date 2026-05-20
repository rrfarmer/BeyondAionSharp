using Aion.Commons.Database;
using Aion.GameServer.Model.Account;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface ICharacterCreationRepository
{
	Task<bool> IsNameUsedAsync(string name, CancellationToken cancellationToken = default);

	Task<bool> IsNameUsedOrReservedAsync(string? oldName, string newName, int reservationDays, CancellationToken cancellationToken = default);

	Task<bool> StoreNewCharacterAsync(int accountId, NewCharacterRecord character, CancellationToken cancellationToken = default);
}

public sealed class EmptyCharacterCreationRepository : ICharacterCreationRepository
{
	public Task<bool> IsNameUsedAsync(string name, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> IsNameUsedOrReservedAsync(string? oldName, string newName, int reservationDays, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> StoreNewCharacterAsync(int accountId, NewCharacterRecord character, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlCharacterCreationRepository : ICharacterCreationRepository
{
	private readonly ILogger<MySqlCharacterCreationRepository> _logger;

	public MySqlCharacterCreationRepository(ILogger<MySqlCharacterCreationRepository> logger)
	{
		_logger = logger;
	}

	public async Task<bool> IsNameUsedAsync(string name, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerDAO.isNameUsed.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT COUNT(id) FROM players WHERE ? = players.name";
			command.Parameters.Add(new MySqlParameter { Value = name });
			var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
			return count > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not check if character name {CharacterName} is used", name);
			return true;
		}
	}

	public async Task<bool> IsNameUsedOrReservedAsync(string? oldName, string newName, int reservationDays, CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerService.isNameUsedOrReserved + dao/OldNamesDAO.isNameReserved.
		if (await IsNameUsedAsync(newName, cancellationToken))
			return true;
		if (reservationDays <= 0)
			return false;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT COUNT(*) FROM old_names
				WHERE old_name = ? AND COALESCE(new_name != ?, TRUE) AND renamed_date > NOW() - INTERVAL ? DAY
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = newName },
					new MySqlParameter { Value = (object?)oldName ?? DBNull.Value },
					new MySqlParameter { Value = reservationDays },
				});
			var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
			return count > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not check if character name {CharacterName} is reserved", newName);
			return false;
		}
	}

	public async Task<bool> StoreNewCharacterAsync(int accountId, NewCharacterRecord character, CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerService.storeNewPlayer.
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
		try
		{
			await InsertPlayerAsync(connection, transaction, accountId, character, cancellationToken);
			await InsertAppearanceAsync(connection, transaction, character.Character, cancellationToken);
			await InsertSkillsAsync(connection, transaction, character, cancellationToken);
			await InsertInventoryAsync(connection, transaction, character, cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			await transaction.RollbackAsync(cancellationToken);
			_logger.LogError(ex, "Could not store new character {CharacterName}", character.Character.Name);
			return false;
		}
	}

	private static async Task InsertPlayerAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int accountId,
		NewCharacterRecord record,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerDAO.saveNewPlayer.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO players(
				id, name, account_id, account_name, x, y, z, heading, world_id, gender, race, player_class,
				quest_expands, npc_expands, item_expands, wh_npc_expands, wh_bonus_expands, online, creation_date)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, 0, 0, 0, 0, 0, ?)
			""";
		var character = record.Character;
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = character.ObjectId },
				new MySqlParameter { Value = character.Name },
				new MySqlParameter { Value = accountId },
				new MySqlParameter { Value = record.AccountName },
				new MySqlParameter { Value = character.X },
				new MySqlParameter { Value = character.Y },
				new MySqlParameter { Value = character.Z },
				new MySqlParameter { Value = character.Heading },
				new MySqlParameter { Value = character.MapId },
				new MySqlParameter { Value = record.Gender },
				new MySqlParameter { Value = record.Race },
				new MySqlParameter { Value = record.PlayerClass },
				new MySqlParameter { Value = record.CreationDate },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task InsertAppearanceAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		CharacterSelectionEntry character,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerAppearanceDAO.store.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			REPLACE INTO player_appearance (
				player_id, face, hair, deco, tattoo, face_contour, expression, jaw_line, skin_rgb, hair_rgb, lip_rgb, eye_rgb,
				face_shape, forehead, eye_height, eye_space, eye_width, eye_size, eye_shape, eye_angle,
				brow_height, brow_angle, brow_shape, nose, nose_bridge, nose_width, nose_tip, cheek, lip_height, mouth_size,
				lip_size, smile, lip_shape, jaw_height, chin_jut, ear_shape, head_size, neck, neck_length, shoulders,
				shoulder_size, torso, chest, waist, hips, arm_thickness, arm_length, hand_size, leg_thickness, leg_length,
				foot_size, facial_rate, voice, height)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,
				?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
			""";
		var appearance = character.Appearance;
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = character.ObjectId },
				new MySqlParameter { Value = appearance.Face },
				new MySqlParameter { Value = appearance.Hair },
				new MySqlParameter { Value = appearance.Deco },
				new MySqlParameter { Value = appearance.Tattoo },
				new MySqlParameter { Value = appearance.FaceContour },
				new MySqlParameter { Value = appearance.Expression },
				new MySqlParameter { Value = appearance.JawLine },
				new MySqlParameter { Value = appearance.SkinRgb },
				new MySqlParameter { Value = appearance.HairRgb },
				new MySqlParameter { Value = appearance.LipRgb },
				new MySqlParameter { Value = appearance.EyeRgb },
				new MySqlParameter { Value = appearance.FaceShape },
				new MySqlParameter { Value = appearance.Forehead },
				new MySqlParameter { Value = appearance.EyeHeight },
				new MySqlParameter { Value = appearance.EyeSpace },
				new MySqlParameter { Value = appearance.EyeWidth },
				new MySqlParameter { Value = appearance.EyeSize },
				new MySqlParameter { Value = appearance.EyeShape },
				new MySqlParameter { Value = appearance.EyeAngle },
				new MySqlParameter { Value = appearance.BrowHeight },
				new MySqlParameter { Value = appearance.BrowAngle },
				new MySqlParameter { Value = appearance.BrowShape },
				new MySqlParameter { Value = appearance.Nose },
				new MySqlParameter { Value = appearance.NoseBridge },
				new MySqlParameter { Value = appearance.NoseWidth },
				new MySqlParameter { Value = appearance.NoseTip },
				new MySqlParameter { Value = appearance.Cheek },
				new MySqlParameter { Value = appearance.LipHeight },
				new MySqlParameter { Value = appearance.MouthSize },
				new MySqlParameter { Value = appearance.LipSize },
				new MySqlParameter { Value = appearance.Smile },
				new MySqlParameter { Value = appearance.LipShape },
				new MySqlParameter { Value = appearance.JawHeight },
				new MySqlParameter { Value = appearance.ChinJut },
				new MySqlParameter { Value = appearance.EarShape },
				new MySqlParameter { Value = appearance.HeadSize },
				new MySqlParameter { Value = appearance.Neck },
				new MySqlParameter { Value = appearance.NeckLength },
				new MySqlParameter { Value = appearance.Shoulders },
				new MySqlParameter { Value = appearance.ShoulderSize },
				new MySqlParameter { Value = appearance.Torso },
				new MySqlParameter { Value = appearance.Chest },
				new MySqlParameter { Value = appearance.Waist },
				new MySqlParameter { Value = appearance.Hips },
				new MySqlParameter { Value = appearance.ArmThickness },
				new MySqlParameter { Value = appearance.ArmLength },
				new MySqlParameter { Value = appearance.HandSize },
				new MySqlParameter { Value = appearance.LegThickness },
				new MySqlParameter { Value = appearance.LegLength },
				new MySqlParameter { Value = appearance.FootSize },
				new MySqlParameter { Value = appearance.FacialRate },
				new MySqlParameter { Value = appearance.Voice },
				new MySqlParameter { Value = appearance.Height },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task InsertInventoryAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		NewCharacterRecord character,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/InventoryDAO.INSERT_QUERY for PlayerService.storeNewPlayer starter inventory.
		if (character.StartingItems.Count == 0)
			return;

		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO inventory (
				item_unique_id, item_id, item_count, item_color, color_expires, item_creator, expire_time, activation_count,
				item_owner, is_equipped, is_soul_bound, slot, item_location, enchant, enchant_bonus, item_skin, fusioned_item,
				optional_socket, optional_fusion_socket, charge, tune_count, rnd_bonus, fusion_rnd_bonus, tempering, pack_count,
				is_amplified, buff_skill, rnd_plume_bonus)
			VALUES (?, ?, ?, NULL, 0, NULL, 0, 0, ?, ?, 0, ?, ?, 0, 0, ?, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
			""";
		foreach (var item in character.StartingItems)
		{
			command.Parameters.Clear();
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = item.ObjectId },
					new MySqlParameter { Value = item.ItemId },
					new MySqlParameter { Value = item.Count },
					new MySqlParameter { Value = character.Character.ObjectId },
					new MySqlParameter { Value = item.IsEquipped },
					new MySqlParameter { Value = item.EquipmentSlot },
					new MySqlParameter { Value = item.ItemLocation },
					new MySqlParameter { Value = item.ItemSkin },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static async Task InsertSkillsAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		NewCharacterRecord character,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerSkillListDAO.storeSkills.
		if (character.StartingSkills.Count == 0)
			return;

		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "REPLACE INTO player_skills (player_id, skill_id, skill_level) VALUES (?, ?, ?)";
		foreach (var skill in character.StartingSkills)
		{
			command.Parameters.Clear();
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = character.Character.ObjectId },
					new MySqlParameter { Value = skill.SkillId },
					new MySqlParameter { Value = skill.SkillLevel },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}
}
