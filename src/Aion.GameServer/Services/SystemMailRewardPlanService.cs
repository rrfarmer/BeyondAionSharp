using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class SystemMailRewardPlanService
{
	public const int ExpressLetterTypeId = 1;
	public const int MailboxStorageId = 127;
	public const int MaxRecipientNameLength = 16;
	public const int MaxSenderNameLength = 16;
	public const int MaxTitleLength = 20;
	public const int MaxMessageLength = 1000;
	public const int MaxSystemMailboxLetters = 199;

	public static SystemMailRewardPlan CreatePlan(
		Player? recipient,
		CustomLevelRewardDescriptor descriptor,
		int mailObjectId,
		int attachedItemObjectId,
		DateTime receivedTime,
		ItemTemplateTable? itemTemplates)
	{
		return CreatePlan(
			recipient,
			new SystemMailRewardRequest(
				descriptor.Sender,
				descriptor.Title,
				descriptor.Body,
				descriptor.Reward.ItemId,
				descriptor.Reward.Count,
				AttachedKinah: 0,
				descriptor.LetterType,
				descriptor.JavaSource),
			mailObjectId,
			attachedItemObjectId,
			receivedTime,
			itemTemplates);
	}

	public static SystemMailRewardPlan CreatePlan(
		Player? recipient,
		StarterKitLevelChangeDescriptor descriptor,
		int mailObjectId,
		int attachedItemObjectId,
		DateTime receivedTime,
		ItemTemplateTable? itemTemplates)
	{
		return CreatePlan(
			recipient,
			new SystemMailRewardRequest(
				descriptor.Sender,
				descriptor.Title,
				descriptor.Body,
				descriptor.Reward.ItemId,
				descriptor.Reward.Count,
				AttachedKinah: 0,
				descriptor.LetterType,
				descriptor.JavaSource),
			mailObjectId,
			attachedItemObjectId,
			receivedTime,
			itemTemplates);
	}

	public static SystemMailRewardPlan CreatePlan(
		Player? recipient,
		SystemMailRewardRequest request,
		int mailObjectId,
		int attachedItemObjectId,
		DateTime receivedTime,
		ItemTemplateTable? itemTemplates)
	{
		// Java parity: services/mail/SystemMailService.sendMail guard and Letter construction order.
		if (recipient == null)
			return SystemMailRewardPlan.Skipped(SystemMailRewardPlanStatus.MissingRecipient, request);
		if (request.AttachedItemId != 0 && request.AttachedItemCount <= 0)
			return SystemMailRewardPlan.Skipped(SystemMailRewardPlanStatus.InvalidAttachedItemCount, request, recipient);
		if (request.AttachedItemId != 0 && itemTemplates?.GetItemTemplate(request.AttachedItemId) == null)
			return SystemMailRewardPlan.Skipped(SystemMailRewardPlanStatus.MissingItemTemplate, request, recipient);
		if (request.RecipientName(recipient).Length > MaxRecipientNameLength)
			return SystemMailRewardPlan.Skipped(SystemMailRewardPlanStatus.RecipientNameTooLong, request, recipient);
		if (!request.Sender.StartsWith("$$", StringComparison.Ordinal) && request.Sender.Length > MaxSenderNameLength)
			return SystemMailRewardPlan.Skipped(SystemMailRewardPlanStatus.SenderNameTooLong, request, recipient);
		if (recipient.Mailbox.Count > MaxSystemMailboxLetters)
			return SystemMailRewardPlan.Skipped(SystemMailRewardPlanStatus.RecipientMailboxFull, request, recipient);

		var attachedItem = request.AttachedItemId == 0
			? null
			: new InventoryItem
			{
				ObjectId = attachedItemObjectId,
				ItemId = request.AttachedItemId,
				Count = request.AttachedItemCount,
				OwnerId = recipient.ObjectId,
				IsEquipped = false,
				Slot = 0,
				Location = MailboxStorageId,
			};
		var mail = new PlayerMail(
			mailObjectId,
			recipient.ObjectId,
			request.Sender,
			Truncate(request.Title, MaxTitleLength),
			Truncate(request.Message, MaxMessageLength),
			IsUnread: true,
			attachedItem?.ObjectId ?? 0,
			request.AttachedItemId,
			request.AttachedKinah > 0 ? request.AttachedKinah : 0,
			ResolveLetterType(request.LetterType),
			receivedTime,
			attachedItem);

		return SystemMailRewardPlan.Planned(request, recipient, mail);
	}

	private static int ResolveLetterType(string letterType)
	{
		return string.Equals(letterType, "EXPRESS", StringComparison.OrdinalIgnoreCase)
			? ExpressLetterTypeId
			: 0;
	}

	private static string Truncate(string value, int maxLength)
	{
		return value.Length > maxLength ? value[..maxLength] : value;
	}
}

public sealed record SystemMailRewardRequest(
	string Sender,
	string Title,
	string Message,
	int AttachedItemId,
	long AttachedItemCount,
	long AttachedKinah,
	string LetterType,
	string JavaSource)
{
	public string RecipientName(Player recipient)
	{
		return recipient.Name;
	}
}

public sealed record SystemMailRewardPlan(
	SystemMailRewardPlanStatus Status,
	SystemMailRewardRequest Request,
	int RecipientObjectId,
	string RecipientName,
	int MailboxLetters,
	PlayerMail? Mail,
	bool IsLive,
	string JavaSource)
{
	public bool Applied => Status == SystemMailRewardPlanStatus.Planned;

	public static SystemMailRewardPlan Skipped(
		SystemMailRewardPlanStatus status,
		SystemMailRewardRequest request,
		Player? recipient = null)
	{
		return new SystemMailRewardPlan(
			status,
			request,
			recipient?.ObjectId ?? 0,
			recipient?.Name ?? string.Empty,
			recipient?.Mailbox.Count ?? 0,
			Mail: null,
			IsLive: false,
			JavaSource: request.JavaSource);
	}

	public static SystemMailRewardPlan Planned(
		SystemMailRewardRequest request,
		Player recipient,
		PlayerMail mail)
	{
		return new SystemMailRewardPlan(
			SystemMailRewardPlanStatus.Planned,
			request,
			recipient.ObjectId,
			recipient.Name,
			recipient.Mailbox.Count,
			mail,
			IsLive: false,
			JavaSource: request.JavaSource);
	}
}

public enum SystemMailRewardPlanStatus
{
	Planned,
	MissingRecipient,
	InvalidAttachedItemCount,
	MissingItemTemplate,
	RecipientNameTooLong,
	SenderNameTooLong,
	RecipientMailboxFull,
}
