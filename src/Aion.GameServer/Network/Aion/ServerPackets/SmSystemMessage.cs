using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSystemMessage : GameServerPacket
{
	public const int PacketOpCode = 25;
	private const int GoldenYellowChatType = 25;

	private readonly int _messageId;
	private readonly IReadOnlyList<string> _parameters;
	private readonly IReadOnlyList<string> _specialParameters;

	public SmSystemMessage(int messageId, params string[] parameters)
		: this(messageId, parameters, Array.Empty<string>())
	{
	}

	private SmSystemMessage(int messageId, IReadOnlyList<string> parameters, IReadOnlyList<string> specialParameters)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_SYSTEM_MESSAGE(ChatType.GOLDEN_YELLOW, null, msgId, params).
		_messageId = messageId;
		_parameters = parameters;
		_specialParameters = specialParameters;
	}

	public static SmSystemMessage NotEnoughMoney()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY.
		return new SmSystemMessage(1300388);
	}

	public static SmSystemMessage BrokerPriceExceedsLimit()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_LIMITED_VENDOR_CANT_OVER_GOLD.
		return new SmSystemMessage(1402666);
	}

	public static SmSystemMessage VendorCannotBuyOwnRegisteredItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_VENDOR_CAN_NOT_BUY_MY_REGISTER_ITEM.
		return new SmSystemMessage(1400750);
	}

	public static SmSystemMessage FullInventory()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_FULL_INVENTORY.
		return new SmSystemMessage(1300762);
	}

	public static SmSystemMessage ExchangeFullInventory()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_FULL_INVENTORY.
		return new SmSystemMessage(1300366);
	}

	public static SmSystemMessage MailTakeAllCancel()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MAIL_TAKE_ALL_CANCEL.
		return new SmSystemMessage(1402251);
	}

	public static SmSystemMessage MailSendCannotSendEquippedItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MAIL_SEND_CAN_NOT_SEND_EQUIPPED_ITEM.
		return new SmSystemMessage(1300496);
	}

	public static SmSystemMessage MailSendUsedItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MAIL_SEND_USED_ITEM.
		return new SmSystemMessage(1300498);
	}

	public static SmSystemMessage PostmanAlreadySummoned()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_POSTMAN_ALREADY_SUMMONED.
		return new SmSystemMessage(1300877);
	}

	public static SmSystemMessage PostmanUnableInCooltime()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_POSTMAN_UNABLE_IN_COOLTIME.
		return new SmSystemMessage(1300878);
	}

	public static SmSystemMessage PostmanNotify()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_POSTMAN_NOTIFY.
		return new SmSystemMessage(1300899);
	}

	public static SmSystemMessage HousingBidCancel()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_CANCEL.
		return new SmSystemMessage(1401266);
	}

	public static SmSystemMessage HousingBidWin(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_WIN.
		return new SmSystemMessage(1401267, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingAuctionSuccess(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_SUCCESS.
		return new SmSystemMessage(1401269, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingAuctionFail(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_FAIL.
		return new SmSystemMessage(1401270, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage NoSuchUser(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER.
		return new SmSystemMessage(1300627, playerName);
	}

	public static SmSystemMessage YouExcluded(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_YOU_EXCLUDED.
		return new SmSystemMessage(1300628, playerName);
	}

	public static SmSystemMessage WhisperRefuse(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WHISPER_REFUSE.
		return new SmSystemMessage(1300629, playerName);
	}

	public static SmSystemMessage CantWhisperLevel(int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANT_WHISPER_LEVEL.
		return new SmSystemMessage(1310004, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage CantWhisperOtherRace()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANT_WHISPER_OTHER_RACE.
		return new SmSystemMessage(1401174);
	}

	public static SmSystemMessage BuddyListNotInList()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUDDYLIST_NOT_IN_LIST.
		return new SmSystemMessage(1300889);
	}

	public static SmSystemMessage BlockListNotInList()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BLOCKLIST_NOT_IN_LIST.
		return new SmSystemMessage(1300897);
	}

	public static SmSystemMessage BlockListNoBuddy()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BLOCKLIST_NO_BUDDY.
		return new SmSystemMessage(1300891);
	}

	public static SmSystemMessage BlockListAlreadyBlocked()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BLOCKLIST_ALREADY_BLOCKED.
		return new SmSystemMessage(1300894);
	}

	public static SmSystemMessage BuddyListBusy()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUDDYLIST_BUSY.
		return new SmSystemMessage(900847);
	}

	public static SmSystemMessage BuddyCantAddWhenAskedQuestion(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUDDY_CANT_ADD_WHEN_HE_IS_ASKED_QUESTION.
		return new SmSystemMessage(1300795, playerName);
	}

	public static SmSystemMessage BuddyListNoBlockedCharacter()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUDDYLIST_NO_BLOCKED_CHARACTER.
		return new SmSystemMessage(1300884);
	}

	public static SmSystemMessage RejectedFriend(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_FRIEND.
		return new SmSystemMessage(1390119, playerName);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_SYSTEM_MESSAGE.writeImpl.
		buffer.WriteC(GoldenYellowChatType);
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteD(_messageId);
		buffer.WriteC(_parameters.Count);
		foreach (var parameter in _parameters)
			buffer.WriteS(parameter);
		buffer.WriteC(_specialParameters.Count);
		foreach (var parameter in _specialParameters)
			buffer.WriteS(parameter);
	}
}
