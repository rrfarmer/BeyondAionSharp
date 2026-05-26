using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class SystemMailRewardPersistencePlanService
{
	public const string JavaMailInsertSql =
		"INSERT INTO `mail` (`mail_unique_id`, `mail_recipient_id`, `sender_name`, `mail_title`, `mail_message`, `unread`, `attached_item_id`, `attached_kinah_count`, `express`, `recieved_time`) VALUES(?,?,?,?,?,?,?,?,?,?)";

	public const string JavaOfflineMailboxCounterSql = "UPDATE players SET mailbox_letters=? WHERE name=?";

	public const string JavaInventoryInsertSql =
		"INSERT INTO `inventory` (`item_unique_id`, `item_id`, `item_count`, `item_color`, `color_expires`, `item_creator`, `expire_time`, `activation_count`, `item_owner`, `is_equipped`, is_soul_bound, `slot`, `item_location`, `enchant`, `enchant_bonus`, `item_skin`, `fusioned_item`, `optional_socket`, `optional_fusion_socket`, `charge`, `tune_count`, `rnd_bonus`, `fusion_rnd_bonus`, `tempering`, `pack_count`, `is_amplified`, `buff_skill`, `rnd_plume_bonus`) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)";

	public static SystemMailRewardPersistencePlan CreatePlan(
		SystemMailRewardPlan mailPlan,
		SystemMailRecipientRuntimeState recipientState)
	{
		// Java parity: services/mail/SystemMailService.sendMail persistence and updateRecipientMailbox ordering.
		if (mailPlan.Status != SystemMailRewardPlanStatus.Planned || mailPlan.Mail == null)
			return SystemMailRewardPersistencePlan.Skipped(mailPlan, recipientState);

		var operations = new List<SystemMailRewardPersistenceOperation>
		{
			SystemMailRewardPersistenceOperation.StoreLetter(mailPlan.Mail),
		};

		if (mailPlan.Mail.AttachedItem != null)
			operations.Add(SystemMailRewardPersistenceOperation.StoreAttachedItem(mailPlan.Mail.AttachedItem, mailPlan.RecipientObjectId));

		if (!recipientState.IsOnline)
		{
			operations.Add(SystemMailRewardPersistenceOperation.UpdateOfflineMailboxCounter(
				mailPlan.RecipientName,
				mailPlan.MailboxLetters + 1));
		}
		else if (recipientState.HasMailbox)
		{
			operations.Add(SystemMailRewardPersistenceOperation.PutLetterToOnlineMailbox(mailPlan.Mail, mailPlan.MailboxLetters + 1));
			operations.Add(SystemMailRewardPersistenceOperation.SendMailboxStatePacket(mailPlan.RecipientObjectId));

			if (recipientState.MailboxState != Player.MailboxClosedState)
			{
				var expressOnly = (recipientState.MailboxState & Player.MailboxExpressState) == Player.MailboxExpressState;
				operations.Add(SystemMailRewardPersistenceOperation.SendMailListPackets(mailPlan.RecipientObjectId, expressOnly));
			}

			if (mailPlan.Mail.LetterType == SystemMailRewardPlanService.ExpressLetterTypeId)
				operations.Add(SystemMailRewardPersistenceOperation.SendPostmanNotify(mailPlan.RecipientObjectId));
		}

		return new SystemMailRewardPersistencePlan(
			SystemMailRewardPersistencePlanStatus.Planned,
			mailPlan,
			recipientState,
			operations,
			IsLive: false,
			JavaSource: "SystemMailService.sendMail -> MailDAO.storeLetter -> InventoryDAO.store -> updateRecipientMailbox");
	}
}

public sealed record SystemMailRecipientRuntimeState(
	bool IsOnline,
	bool HasMailbox,
	byte MailboxState)
{
	public static SystemMailRecipientRuntimeState Offline { get; } = new(false, false, Player.MailboxClosedState);

	public static SystemMailRecipientRuntimeState Online(byte mailboxState = Player.MailboxClosedState, bool hasMailbox = true)
	{
		return new SystemMailRecipientRuntimeState(true, hasMailbox, mailboxState);
	}
}

public sealed record SystemMailRewardPersistencePlan(
	SystemMailRewardPersistencePlanStatus Status,
	SystemMailRewardPlan MailPlan,
	SystemMailRecipientRuntimeState RecipientState,
	IReadOnlyList<SystemMailRewardPersistenceOperation> Operations,
	bool IsLive,
	string JavaSource)
{
	public bool Applied => Status == SystemMailRewardPersistencePlanStatus.Planned;

	public static SystemMailRewardPersistencePlan Skipped(
		SystemMailRewardPlan mailPlan,
		SystemMailRecipientRuntimeState recipientState)
	{
		return new SystemMailRewardPersistencePlan(
			SystemMailRewardPersistencePlanStatus.SkippedMailNotPlanned,
			mailPlan,
			recipientState,
			Array.Empty<SystemMailRewardPersistenceOperation>(),
			IsLive: false,
			JavaSource: "SystemMailService.sendMail skipped before DAO persistence");
	}
}

public sealed record SystemMailRewardPersistenceOperation(
	SystemMailRewardPersistenceOperationKind Kind,
	string JavaArtifact,
	string? Sql,
	IReadOnlyList<string> ParameterOrder,
	string? RecipientName = null,
	PlayerMail? MailPayload = null,
	InventoryItem? AttachedItemPayload = null,
	int? MailObjectId = null,
	int? RecipientObjectId = null,
	int? AttachedItemObjectId = null,
	int? MailboxLettersAfterOperation = null,
	bool? ExpressOnly = null,
	bool StopsOnFailure = false)
{
	public static SystemMailRewardPersistenceOperation StoreLetter(PlayerMail mail)
	{
		return new SystemMailRewardPersistenceOperation(
			SystemMailRewardPersistenceOperationKind.StoreLetter,
			"com.aionemu.gameserver.dao.MailDAO.storeLetter/saveLetter",
			SystemMailRewardPersistencePlanService.JavaMailInsertSql,
			[
				"mail_unique_id",
				"mail_recipient_id",
				"sender_name",
				"mail_title",
				"mail_message",
				"unread",
				"attached_item_id",
				"attached_kinah_count",
				"express",
				"recieved_time",
			],
			MailPayload: mail,
			MailObjectId: mail.Id,
			RecipientObjectId: mail.RecipientId,
			AttachedItemObjectId: mail.AttachedItemObjectId == 0 ? null : mail.AttachedItemObjectId,
			StopsOnFailure: true);
	}

	public static SystemMailRewardPersistenceOperation StoreAttachedItem(InventoryItem item, int recipientObjectId)
	{
		return new SystemMailRewardPersistenceOperation(
			SystemMailRewardPersistenceOperationKind.StoreAttachedItem,
			"com.aionemu.gameserver.dao.InventoryDAO.store/insertItems",
			SystemMailRewardPersistencePlanService.JavaInventoryInsertSql,
			[
				"item_unique_id",
				"item_id",
				"item_count",
				"item_color",
				"color_expires",
				"item_creator",
				"expire_time",
				"activation_count",
				"item_owner",
				"is_equipped",
				"is_soul_bound",
				"slot",
				"item_location",
				"enchant",
				"enchant_bonus",
				"item_skin",
				"fusioned_item",
				"optional_socket",
				"optional_fusion_socket",
				"charge",
				"tune_count",
				"rnd_bonus",
				"fusion_rnd_bonus",
				"tempering",
				"pack_count",
				"is_amplified",
				"buff_skill",
				"rnd_plume_bonus",
			],
			AttachedItemPayload: item,
			RecipientObjectId: recipientObjectId,
			AttachedItemObjectId: item.ObjectId,
			StopsOnFailure: true);
	}

	public static SystemMailRewardPersistenceOperation UpdateOfflineMailboxCounter(string recipientName, int mailboxLetters)
	{
		return new SystemMailRewardPersistenceOperation(
			SystemMailRewardPersistenceOperationKind.UpdateOfflineMailboxCounter,
			"com.aionemu.gameserver.dao.MailDAO.updateOfflineMailCounter",
			SystemMailRewardPersistencePlanService.JavaOfflineMailboxCounterSql,
			["mailbox_letters", "name"],
			RecipientName: recipientName,
			MailboxLettersAfterOperation: mailboxLetters);
	}

	public static SystemMailRewardPersistenceOperation PutLetterToOnlineMailbox(PlayerMail mail, int mailboxLetters)
	{
		return new SystemMailRewardPersistenceOperation(
			SystemMailRewardPersistenceOperationKind.PutLetterToOnlineMailbox,
			"com.aionemu.gameserver.model.gameobjects.player.Mailbox.putLetterToMailbox",
			Sql: null,
			ParameterOrder: Array.Empty<string>(),
			MailPayload: mail,
			MailObjectId: mail.Id,
			RecipientObjectId: mail.RecipientId,
			MailboxLettersAfterOperation: mailboxLetters);
	}

	public static SystemMailRewardPersistenceOperation SendMailboxStatePacket(int recipientObjectId)
	{
		return new SystemMailRewardPersistenceOperation(
			SystemMailRewardPersistenceOperationKind.SendMailboxStatePacket,
			"com.aionemu.gameserver.network.aion.serverpackets.SM_MAIL_SERVICE()",
			Sql: null,
			ParameterOrder: Array.Empty<string>(),
			RecipientObjectId: recipientObjectId);
	}

	public static SystemMailRewardPersistenceOperation SendMailListPackets(int recipientObjectId, bool expressOnly)
	{
		return new SystemMailRewardPersistenceOperation(
			SystemMailRewardPersistenceOperationKind.SendMailListPackets,
			"com.aionemu.gameserver.services.mail.MailService.sendMailList",
			Sql: null,
			ParameterOrder: Array.Empty<string>(),
			RecipientObjectId: recipientObjectId,
			ExpressOnly: expressOnly);
	}

	public static SystemMailRewardPersistenceOperation SendPostmanNotify(int recipientObjectId)
	{
		return new SystemMailRewardPersistenceOperation(
			SystemMailRewardPersistenceOperationKind.SendPostmanNotify,
			"com.aionemu.gameserver.network.aion.serverpackets.SM_SYSTEM_MESSAGE.STR_POSTMAN_NOTIFY",
			Sql: null,
			ParameterOrder: Array.Empty<string>(),
			RecipientObjectId: recipientObjectId);
	}
}

public enum SystemMailRewardPersistencePlanStatus
{
	Planned,
	SkippedMailNotPlanned,
}

public enum SystemMailRewardPersistenceOperationKind
{
	StoreLetter,
	StoreAttachedItem,
	UpdateOfflineMailboxCounter,
	PutLetterToOnlineMailbox,
	SendMailboxStatePacket,
	SendMailListPackets,
	SendPostmanNotify,
}
