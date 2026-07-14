using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IMailRepository
{
	Task<MailRecipientInfo?> LoadRecipientAsync(string recipientName, CancellationToken cancellationToken = default);

	Task<bool> IsBlockedByRecipientAsync(int recipientObjectId, int senderObjectId, CancellationToken cancellationToken = default);

	Task<bool> StoreSentMailAsync(
		PlayerMail mail,
		int? senderKinahObjectId,
		long senderKinahCount,
		CancellationToken cancellationToken = default);

	Task<bool> StoreSentItemMailAsync(
		PlayerMail mail,
		int senderKinahObjectId,
		long senderKinahCount,
		InventoryItem attachedItem,
		int? reducedSenderItemObjectId,
		long reducedSenderItemCount,
		IReadOnlyList<InventoryItem> senderItemUpdates,
		IReadOnlyList<int> senderItemDeletes,
		CancellationToken cancellationToken = default);

	Task<bool> StoreSystemMailLetterAsync(PlayerMail mail, CancellationToken cancellationToken = default);

	Task<bool> StoreSystemMailAttachedItemAsync(
		InventoryItem attachedItem,
		int recipientObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> UpdateOfflineMailboxCounterAsync(
		string recipientName,
		int mailboxLetters,
		CancellationToken cancellationToken = default);

	Task MarkMailReadAsync(int letterId, CancellationToken cancellationToken = default);

	Task ClearAttachedItemAsync(int letterId, int itemObjectId, int playerObjectId, CancellationToken cancellationToken = default);

	Task ClearAttachedKinahAsync(int letterId, CancellationToken cancellationToken = default);

	Task DeleteLettersAsync(IReadOnlyList<int> letterIds, CancellationToken cancellationToken = default);
}

public sealed record MailRecipientInfo(int PlayerObjectId, string Name, string Race, int MailboxLetters);

public sealed record SystemMailRepositoryCommandPlan(
	string JavaArtifact,
	string Sql,
	IReadOnlyList<SystemMailRepositoryParameter> Parameters);

public sealed record SystemMailRepositoryParameter(string Name, object? Value);

public static class SystemMailRepositoryPlan
{
	public const string StoreLetterSql =
		"INSERT INTO `mail` (`mail_unique_id`, `mail_recipient_id`, `sender_name`, `mail_title`, `mail_message`, `unread`, `attached_item_id`, `attached_kinah_count`, `express`, `recieved_time`) VALUES(?,?,?,?,?,?,?,?,?,FROM_UNIXTIME(? / 1000.0))";

	public const string StoreAttachedItemSql =
		"INSERT INTO `inventory` (`item_unique_id`, `item_id`, `item_count`, `item_color`, `color_expires`, `item_creator`, `expire_time`, `activation_count`, `item_owner`, `is_equipped`, is_soul_bound, `slot`, `item_location`, `enchant`, `enchant_bonus`, `item_skin`, `fusioned_item`, `optional_socket`, `optional_fusion_socket`, `charge`, `tune_count`, `rnd_bonus`, `fusion_rnd_bonus`, `tempering`, `pack_count`, `is_amplified`, `buff_skill`, `rnd_plume_bonus`) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)";

	public const string UpdateOfflineMailboxCounterSql = "UPDATE players SET mailbox_letters=? WHERE name=?";

	public static SystemMailRepositoryCommandPlan StoreLetter(PlayerMail mail)
	{
		return new SystemMailRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.MailDAO.storeLetter/saveLetter",
			StoreLetterSql,
			[
				new("mail_unique_id", mail.Id),
				new("mail_recipient_id", mail.RecipientId),
				new("sender_name", mail.SenderName),
				new("mail_title", mail.Title),
				new("mail_message", mail.Message),
				new("unread", mail.IsUnread),
				new("attached_item_id", mail.AttachedItemObjectId),
				new("attached_kinah_count", mail.AttachedKinah),
				new("express", mail.LetterType),
				new("recieved_time_epoch_millis", DatabaseTimestamp.ToUnixTimeMilliseconds(mail.ReceivedTime)),
			]);
	}

	public static SystemMailRepositoryCommandPlan StoreAttachedItem(InventoryItem item, int recipientObjectId)
	{
		return new SystemMailRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.InventoryDAO.store/insertItems",
			StoreAttachedItemSql,
			[
				new("item_unique_id", item.ObjectId),
				new("item_id", item.ItemId),
				new("item_count", item.Count),
				new("item_color", item.Color),
				new("color_expires", item.ColorExpires),
				new("item_creator", item.Creator),
				new("expire_time", item.ExpireTime),
				new("activation_count", item.ActivationCount),
				new("item_owner", recipientObjectId),
				new("is_equipped", item.IsEquipped),
				new("is_soul_bound", item.IsSoulBound ? 1 : 0),
				new("slot", item.Slot),
				new("item_location", item.Location),
				new("enchant", item.Enchant),
				new("enchant_bonus", item.EnchantBonus),
				new("item_skin", item.ItemSkin),
				new("fusioned_item", item.FusionedItem),
				new("optional_socket", item.OptionalSocket),
				new("optional_fusion_socket", item.OptionalFusionSocket),
				new("charge", item.Charge),
				new("tune_count", item.TuneCount),
				new("rnd_bonus", item.RandomBonus),
				new("fusion_rnd_bonus", item.FusionRandomBonus),
				new("tempering", item.Tempering),
				new("pack_count", item.PackCount),
				new("is_amplified", item.IsAmplified),
				new("buff_skill", item.BuffSkill),
				new("rnd_plume_bonus", item.RandomPlumeBonus),
			]);
	}

	public static SystemMailRepositoryCommandPlan UpdateOfflineMailboxCounter(string recipientName, int mailboxLetters)
	{
		return new SystemMailRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.MailDAO.updateOfflineMailCounter",
			UpdateOfflineMailboxCounterSql,
			[
				new("mailbox_letters", mailboxLetters),
				new("name", recipientName),
			]);
	}
}

public sealed class MySqlMailRepository : IMailRepository
{
	private const int CubeStorageId = 0;
	private const int MailboxStorageId = 127;
	private const int FirstAvailableSlot = 65535;

	private readonly ILogger<MySqlMailRepository> _logger;

	public MySqlMailRepository(ILogger<MySqlMailRepository> logger)
	{
		_logger = logger;
	}

	public async Task<MailRecipientInfo?> LoadRecipientAsync(string recipientName, CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerService.getOrLoadPlayerCommonData for MailService.validateRecipient.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT id, name, race, mailbox_letters
				FROM players
				WHERE name = ? AND (deletion_date IS NULL OR deletion_date > CURRENT_TIMESTAMP)
				""";
			command.Parameters.Add(new MySqlParameter { Value = recipientName });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return null;

			return new MailRecipientInfo(
				ReadInt(reader, "id"),
				ReadString(reader, "name"),
				ReadString(reader, "race"),
				ReadInt(reader, "mailbox_letters"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load mail recipient {RecipientName}", recipientName);
			return null;
		}
	}

	public async Task<bool> IsBlockedByRecipientAsync(
		int recipientObjectId,
		int senderObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/BlockListDAO.load(recipient).contains(sender).
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT 1 FROM blocks WHERE player = ? AND blocked_player = ? LIMIT 1";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = recipientObjectId },
					new MySqlParameter { Value = senderObjectId },
				});
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			return await reader.ReadAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not check mail block state for recipient {RecipientObjectId} and sender {SenderObjectId}",
				recipientObjectId,
				senderObjectId);
			return false;
		}
	}

	public async Task<bool> StoreSentMailAsync(
		PlayerMail mail,
		int? senderKinahObjectId,
		long senderKinahCount,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/mail/MailService.sendMail final DB state for non-item mail.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await UpdateSenderKinahAsync(connection, transaction, senderKinahObjectId, senderKinahCount, cancellationToken);
			await InsertMailAsync(connection, transaction, mail, cancellationToken);
			await IncrementMailboxCountAsync(connection, transaction, mail.RecipientId, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not store sent mail {MailId} for recipient {RecipientId}", mail.Id, mail.RecipientId);
			return false;
		}
	}

	public async Task<bool> StoreSentItemMailAsync(
		PlayerMail mail,
		int senderKinahObjectId,
		long senderKinahCount,
		InventoryItem attachedItem,
		int? reducedSenderItemObjectId,
		long reducedSenderItemCount,
		IReadOnlyList<InventoryItem> senderItemUpdates,
		IReadOnlyList<int> senderItemDeletes,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/mail/MailService.sendMail final DB state for item-attached mail.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await UpdateSenderKinahAsync(connection, transaction, senderKinahObjectId, senderKinahCount, cancellationToken);
			foreach (var itemUpdate in senderItemUpdates)
				await UpdateInventoryItemCountAsync(connection, transaction, itemUpdate.ObjectId, itemUpdate.Count, cancellationToken);
			foreach (var itemDelete in senderItemDeletes)
				await DeleteInventoryItemAsync(connection, transaction, itemDelete, cancellationToken);

			if (reducedSenderItemObjectId.HasValue)
			{
				await UpdateInventoryItemCountAsync(
					connection,
					transaction,
					reducedSenderItemObjectId.Value,
					reducedSenderItemCount,
					cancellationToken);
				await InsertInventoryItemAsync(connection, transaction, attachedItem, cancellationToken);
			}
			else
			{
				await MoveExistingInventoryItemToMailboxAsync(connection, transaction, attachedItem, cancellationToken);
			}

			await InsertMailAsync(connection, transaction, mail, cancellationToken);
			await IncrementMailboxCountAsync(connection, transaction, mail.RecipientId, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not store sent item mail {MailId} with item {ItemObjectId} for recipient {RecipientId}",
				mail.Id,
				attachedItem.ObjectId,
				mail.RecipientId);
			return false;
		}
	}

	public async Task<bool> StoreSystemMailLetterAsync(PlayerMail mail, CancellationToken cancellationToken = default)
	{
		// Java parity: services/mail/SystemMailService.sendMail calls MailDAO.storeLetter before any item persistence.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			ApplyPlan(command, SystemMailRepositoryPlan.StoreLetter(mail));
			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not store system mail {MailId} for recipient {RecipientId}", mail.Id, mail.RecipientId);
			return false;
		}
	}

	public async Task<bool> StoreSystemMailAttachedItemAsync(
		InventoryItem attachedItem,
		int recipientObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: SystemMailService.sendMail calls InventoryDAO.store(attachedItem, recipientId) after MailDAO.storeLetter.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			ApplyPlan(command, SystemMailRepositoryPlan.StoreAttachedItem(attachedItem, recipientObjectId));
			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not store system mail attached item {ItemObjectId} for recipient {RecipientId}",
				attachedItem.ObjectId,
				recipientObjectId);
			return false;
		}
	}

	public async Task<bool> UpdateOfflineMailboxCounterAsync(
		string recipientName,
		int mailboxLetters,
		CancellationToken cancellationToken = default)
	{
		// Java parity: MailDAO.updateOfflineMailCounter updates by player name after incrementing PlayerCommonData.mailboxLetters.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			ApplyPlan(command, SystemMailRepositoryPlan.UpdateOfflineMailboxCounter(recipientName, mailboxLetters));
			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not update offline mailbox counter for recipient {RecipientName}", recipientName);
			return false;
		}
	}

	public async Task MarkMailReadAsync(int letterId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/MailDAO.storeLetter for Letter.setReadLetter final persisted state.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE mail SET unread = ? WHERE mail_unique_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = false },
					new MySqlParameter { Value = letterId },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not mark mail {LetterId} read", letterId);
		}
	}

	public async Task ClearAttachedItemAsync(
		int letterId,
		int itemObjectId,
		int playerObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: MailService.getAttachments item path final state after InventoryDAO.store and mailbox save.
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
					SET item_owner = ?, item_location = ?, slot = ?
					WHERE item_unique_id = ? AND item_location = ?
					""";
				itemCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = playerObjectId },
						new MySqlParameter { Value = CubeStorageId },
						new MySqlParameter { Value = FirstAvailableSlot },
						new MySqlParameter { Value = itemObjectId },
						new MySqlParameter { Value = MailboxStorageId },
					});
				await itemCommand.ExecuteNonQueryAsync(cancellationToken);
			}

			await using (var mailCommand = connection.CreateCommand())
			{
				mailCommand.Transaction = transaction;
				mailCommand.CommandText = "UPDATE mail SET attached_item_id = ? WHERE mail_unique_id = ? AND attached_item_id = ?";
				mailCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = 0 },
						new MySqlParameter { Value = letterId },
						new MySqlParameter { Value = itemObjectId },
					});
				await mailCommand.ExecuteNonQueryAsync(cancellationToken);
			}

			await transaction.CommitAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not clear attached item {ItemObjectId} from mail {LetterId}", itemObjectId, letterId);
		}
	}

	public async Task ClearAttachedKinahAsync(int letterId, CancellationToken cancellationToken = default)
	{
		// Java parity: MailService.getAttachments kinah path calls MailDAO.storeLetter after removeAttachedKinah.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE mail SET attached_kinah_count = ? WHERE mail_unique_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = 0 },
					new MySqlParameter { Value = letterId },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not clear attached kinah from mail {LetterId}", letterId);
		}
	}

	public async Task DeleteLettersAsync(IReadOnlyList<int> letterIds, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/MailDAO.deleteLetter for each selected letter.
		if (letterIds.Count == 0)
			return;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			var placeholders = new string[letterIds.Count];
			for (var i = 0; i < letterIds.Count; i++)
			{
				var parameterName = $"@letter{i}";
				placeholders[i] = parameterName;
				command.Parameters.Add(new MySqlParameter(parameterName, letterIds[i]));
			}

			command.CommandText = $"DELETE FROM mail WHERE mail_unique_id IN ({string.Join(", ", placeholders)})";
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete mail letters {LetterIds}", string.Join(", ", letterIds));
		}
	}

	private static async Task UpdateSenderKinahAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int? senderKinahObjectId,
		long senderKinahCount,
		CancellationToken cancellationToken)
	{
		if (!senderKinahObjectId.HasValue)
			return;

		await using var kinahCommand = connection.CreateCommand();
		kinahCommand.Transaction = transaction;
		kinahCommand.CommandText = "UPDATE inventory SET item_count = ? WHERE item_unique_id = ?";
		kinahCommand.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = senderKinahCount },
				new MySqlParameter { Value = senderKinahObjectId.Value },
			});
		await kinahCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task InsertMailAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		PlayerMail mail,
		CancellationToken cancellationToken)
	{
		await using var mailCommand = connection.CreateCommand();
		mailCommand.Transaction = transaction;
		mailCommand.CommandText = """
			INSERT INTO mail (
				mail_unique_id, mail_recipient_id, sender_name, mail_title, mail_message, unread,
				attached_item_id, attached_kinah_count, express, recieved_time
			)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, FROM_UNIXTIME(? / 1000.0))
			""";
		mailCommand.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = mail.Id },
				new MySqlParameter { Value = mail.RecipientId },
				new MySqlParameter { Value = mail.SenderName },
				new MySqlParameter { Value = mail.Title },
				new MySqlParameter { Value = mail.Message },
				new MySqlParameter { Value = mail.IsUnread },
				new MySqlParameter { Value = mail.AttachedItemObjectId },
				new MySqlParameter { Value = mail.AttachedKinah },
				new MySqlParameter { Value = mail.LetterType },
				new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(mail.ReceivedTime) },
			});
		await mailCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task IncrementMailboxCountAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int recipientId,
		CancellationToken cancellationToken)
	{
		await using var mailboxCommand = connection.CreateCommand();
		mailboxCommand.Transaction = transaction;
		mailboxCommand.CommandText = "UPDATE players SET mailbox_letters = LEAST(mailbox_letters + 1, 255) WHERE id = ?";
		mailboxCommand.Parameters.Add(new MySqlParameter { Value = recipientId });
		await mailboxCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task UpdateInventoryItemCountAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int itemObjectId,
		long itemCount,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE inventory SET item_count = ? WHERE item_unique_id = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = itemCount },
				new MySqlParameter { Value = itemObjectId },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task DeleteInventoryItemAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int itemObjectId,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "DELETE FROM inventory WHERE item_unique_id = ?";
		command.Parameters.Add(new MySqlParameter { Value = itemObjectId });
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task MoveExistingInventoryItemToMailboxAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			UPDATE inventory
			SET item_count = ?, item_owner = ?, is_equipped = ?, slot = ?, item_location = ?, pack_count = ?
			WHERE item_unique_id = ?
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.Count },
				new MySqlParameter { Value = item.OwnerId },
				new MySqlParameter { Value = item.IsEquipped },
				new MySqlParameter { Value = item.Slot },
				new MySqlParameter { Value = item.Location },
				new MySqlParameter { Value = item.PackCount },
				new MySqlParameter { Value = item.ObjectId },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
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
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static void ApplyPlan(MySqlCommand command, SystemMailRepositoryCommandPlan plan)
	{
		command.CommandText = plan.Sql;
		foreach (var parameter in plan.Parameters)
		{
			command.Parameters.Add(new MySqlParameter { Value = parameter.Value ?? DBNull.Value });
		}
	}

	private static int ReadInt(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
	}

	private static string ReadString(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
	}
}
