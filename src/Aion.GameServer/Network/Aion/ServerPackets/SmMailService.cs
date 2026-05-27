using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMailService : GameServerPacket
{
	public const int PacketOpCode = 161;
	public const int MailSendSuccess = 0;
	public const int NoSuchCharacterName = 1;
	public const int RecipientMailboxFull = 2;
	public const int MailIsOneRaceOnly = 3;
	public const int YouAreInRecipientIgnoreList = 4;
	public const int RecipientIgnoringMailFromPlayersLower206Level = 5;
	public const int MailSpamWaitForSomeTime = 6;

	private const int MailboxStateServiceId = 0;
	private const int MailMessageServiceId = 1;
	private const int LettersListServiceId = 2;
	private const int ReadLetterServiceId = 3;
	private const int AttachmentStateServiceId = 5;
	private const int DeleteLetterServiceId = 6;
	private const int StaticBodySize = 8;

	private readonly int _serviceId;
	private readonly IReadOnlyList<PlayerMail> _mailbox;
	private readonly PlayerMail? _letter;
	private readonly ItemTemplateTable? _itemTemplates;
	private readonly int _generalInfoWarehouseRestrictionFlag;
	private readonly int _playerObjectId;
	private readonly int _mailMessageId;
	private readonly int _letterId;
	private readonly int _attachmentType;
	private readonly IReadOnlyList<int> _letterIds = Array.Empty<int>();
	private readonly bool _isLastPacket;

	public SmMailService(IReadOnlyList<PlayerMail> mailbox)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MAIL_SERVICE() with serviceId 0.
		_serviceId = MailboxStateServiceId;
		_mailbox = mailbox;
	}

	private SmMailService(int mailMessageId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MAIL_SERVICE(MailMessage) with serviceId 1.
		_serviceId = MailMessageServiceId;
		_mailbox = Array.Empty<PlayerMail>();
		_mailMessageId = mailMessageId;
	}

	private SmMailService(int playerObjectId, IReadOnlyList<PlayerMail> letters, bool isLastPacket)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MAIL_SERVICE(Player, List<Letter>, boolean) with serviceId 2.
		_serviceId = LettersListServiceId;
		_playerObjectId = playerObjectId;
		_mailbox = letters;
		_isLastPacket = isLastPacket;
	}

	private SmMailService(
		IReadOnlyList<PlayerMail> mailbox,
		PlayerMail letter,
		ItemTemplateTable? itemTemplates,
		int generalInfoWarehouseRestrictionFlag)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MAIL_SERVICE(Player, Letter, long) with serviceId 3.
		_serviceId = ReadLetterServiceId;
		_mailbox = mailbox;
		_letter = letter;
		_itemTemplates = itemTemplates;
		_generalInfoWarehouseRestrictionFlag = generalInfoWarehouseRestrictionFlag;
	}

	private SmMailService(int letterId, int attachmentType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MAIL_SERVICE(int, byte) with serviceId 5.
		_serviceId = AttachmentStateServiceId;
		_mailbox = Array.Empty<PlayerMail>();
		_letterId = letterId;
		_attachmentType = attachmentType;
	}

	private SmMailService(IReadOnlyList<PlayerMail> mailbox, IReadOnlyList<int> letterIds)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MAIL_SERVICE(int[]) with serviceId 6.
		_serviceId = DeleteLetterServiceId;
		_mailbox = mailbox;
		_letterIds = letterIds;
	}

	public static SmMailService CreateMailMessage(int mailMessageId)
	{
		// Java parity: services/mail/MailService.sendMail status response.
		return new SmMailService(mailMessageId);
	}

	public static IReadOnlyList<SmMailService> CreateListPackets(int playerObjectId, IReadOnlyList<PlayerMail> mailbox, bool expressOnly)
	{
		// Java parity: services/mail/MailService.sendMailList and DynamicServerPacketBodySplitList.
		var letters = mailbox
			.Where(mail => !expressOnly || mail.IsUnreadExpress || mail.IsUnreadBlackCloud)
			.OrderByDescending(mail => mail.ReceivedTime)
			.ToArray();
		if (letters.Length == 0)
			return [new SmMailService(playerObjectId, Array.Empty<PlayerMail>(), isLastPacket: true)];

		var packets = new List<SmMailService>();
		var part = new List<PlayerMail>();
		var partSize = 0;
		var maxDynamicSize = MaxUsablePacketBodySize - StaticBodySize;
		foreach (var letter in letters)
		{
			var letterSize = GetLetterListSize(letter);
			if (letterSize > maxDynamicSize)
				throw new InvalidOperationException($"Mail {letter.Id} exceeds maximum SM_MAIL_SERVICE list body size.");

			if (part.Count > 0 && partSize + letterSize > maxDynamicSize)
			{
				packets.Add(new SmMailService(playerObjectId, part.ToArray(), isLastPacket: false));
				part.Clear();
				partSize = 0;
			}

			part.Add(letter);
			partSize += letterSize;
		}

		packets.Add(new SmMailService(playerObjectId, part.ToArray(), isLastPacket: true));
		return packets;
	}

	public static SmMailService CreateReadPacket(
		IReadOnlyList<PlayerMail> mailbox,
		PlayerMail letter,
		ItemTemplateTable? itemTemplates,
		int generalInfoWarehouseRestrictionFlag = 0)
	{
		// Java parity: services/mail/MailService.readMail sends SM_MAIL_SERVICE before marking the letter read.
		return new SmMailService(mailbox, letter, itemTemplates, generalInfoWarehouseRestrictionFlag);
	}

	public static SmMailService CreateAttachmentState(int letterId, int attachmentType)
	{
		// Java parity: services/mail/MailService.getAttachments sends serviceId 5 after moving item/kinah.
		return new SmMailService(letterId, attachmentType);
	}

	public static SmMailService CreateDeletePacket(IReadOnlyList<PlayerMail> mailbox, IReadOnlyList<int> letterIds)
	{
		// Java parity: services/mail/MailService.deleteMail sends serviceId 6 after mailbox removal.
		return new SmMailService(mailbox, letterIds);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MAIL_SERVICE.writeImpl service switch.
		buffer.WriteC(_serviceId);
		if (_serviceId == MailMessageServiceId)
		{
			buffer.WriteC(_mailMessageId);
			return;
		}

		if (_serviceId == LettersListServiceId)
		{
			WriteLettersList(buffer);
			return;
		}

		if (_serviceId == ReadLetterServiceId)
		{
			WriteLetterRead(buffer);
			return;
		}

		if (_serviceId == AttachmentStateServiceId)
		{
			WriteLetterState(buffer);
			return;
		}

		if (_serviceId == DeleteLetterServiceId)
		{
			WriteLetterDelete(buffer);
			return;
		}

		// Java parity: SM_MAIL_SERVICE.writeMailboxState.
		buffer.WriteH(_mailbox.Count);
		buffer.WriteH(_mailbox.Count(mail => mail.IsUnread));
		buffer.WriteH(_mailbox.Count(mail => mail.IsUnreadExpress));
		buffer.WriteH(_mailbox.Count(mail => mail.IsUnreadBlackCloud));
	}

	private void WriteLettersList(PacketBuffer buffer)
	{
		// Java parity: SM_MAIL_SERVICE.writeLettersList.
		buffer.WriteD(_playerObjectId);
		buffer.WriteC(0);
		buffer.WriteH(_isLastPacket ? -_mailbox.Count : _mailbox.Count);
		foreach (var letter in _mailbox)
		{
			buffer.WriteD(letter.Id);
			buffer.WriteS(letter.SenderName);
			buffer.WriteS(letter.Title);
			buffer.WriteC(letter.IsUnread ? 0 : 1);
			buffer.WriteD(letter.AttachedItemObjectId);
			buffer.WriteD(letter.AttachedItemObjectId == 0 ? 0 : letter.AttachedItemTemplateId);
			buffer.WriteQ(letter.AttachedKinah);
			buffer.WriteC(letter.LetterType);
		}
	}

	private void WriteLetterRead(PacketBuffer buffer)
	{
		// Java parity: SM_MAIL_SERVICE.writeLetterRead.
		var letter = _letter ?? throw new InvalidOperationException("Read-mail packet requires a letter.");
		var unreadCount = _mailbox.Count(mail => mail.IsUnread);
		var unreadExpressCount = _mailbox.Count(mail => mail.IsUnreadExpress);
		var unreadBlackCloudCount = _mailbox.Count(mail => mail.IsUnreadBlackCloud);

		buffer.WriteD(letter.RecipientId);
		buffer.WriteD(_mailbox.Count + unreadCount * 0x10000);
		buffer.WriteD(unreadExpressCount + unreadBlackCloudCount);
		buffer.WriteD(letter.Id);
		buffer.WriteD(letter.RecipientId);
		buffer.WriteS(letter.SenderName);
		buffer.WriteS(letter.Title);
		buffer.WriteS(letter.Message);

		var attachedItem = letter.AttachedItem;
		var itemTemplate = attachedItem == null ? null : _itemTemplates?.GetItemTemplate(attachedItem.ItemId);
		if (attachedItem != null && itemTemplate != null)
		{
			buffer.WriteD(attachedItem.ObjectId);
			buffer.WriteD(itemTemplate.TemplateId);
			buffer.WriteD(1);
			buffer.WriteD(0);
			buffer.WriteS(itemTemplate.GetClientName());
			SmInventoryInfo.WriteItemInfoBlob(buffer, attachedItem, itemTemplate, _generalInfoWarehouseRestrictionFlag);
		}
		else
		{
			buffer.WriteQ(0);
			buffer.WriteQ(0);
			buffer.WriteD(0);
		}

		buffer.WriteD((int)letter.AttachedKinah);
		buffer.WriteD(0);
		buffer.WriteC(0);
		buffer.WriteD(GetReceivedEpochSeconds(letter.ReceivedTime));
		buffer.WriteC(letter.LetterType);
	}

	private void WriteLetterState(PacketBuffer buffer)
	{
		// Java parity: SM_MAIL_SERVICE.writeLetterState.
		buffer.WriteD(_letterId);
		buffer.WriteC(_attachmentType);
		buffer.WriteC(1);
	}

	private void WriteLetterDelete(PacketBuffer buffer)
	{
		// Java parity: SM_MAIL_SERVICE.writeLetterDelete.
		var unreadCount = _mailbox.Count(mail => mail.IsUnread);
		var unreadExpressCount = _mailbox.Count(mail => mail.IsUnreadExpress);
		var unreadBlackCloudCount = _mailbox.Count(mail => mail.IsUnreadBlackCloud);
		buffer.WriteD(_mailbox.Count + unreadCount * 0x10000);
		buffer.WriteD(unreadExpressCount + unreadBlackCloudCount);
		buffer.WriteH(_letterIds.Count);
		foreach (var letterId in _letterIds)
			buffer.WriteD(letterId);
	}

	private static int GetLetterListSize(PlayerMail letter)
	{
		// Java parity: SM_MAIL_SERVICE.DYNAMIC_BODY_PART_SIZE_CALCULATOR.
		return 22 + ByteLengthForString(letter.SenderName) + ByteLengthForString(letter.Title);
	}

	private static int ByteLengthForString(string? value)
	{
		return ((value?.Length ?? 0) + 1) * 2;
	}

	private static int GetReceivedEpochSeconds(DateTime receivedTime)
	{
		return receivedTime == DateTime.MinValue
			? 0
			: (int)new DateTimeOffset(receivedTime).ToUnixTimeSeconds();
	}
}
