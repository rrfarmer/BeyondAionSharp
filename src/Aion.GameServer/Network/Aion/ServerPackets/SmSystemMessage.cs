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

	public static SmSystemMessage NotEnoughKinah(long kinah)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA.
		return new SmSystemMessage(901285, kinah.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage UseAbyssPoint(long abyssPoints)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_USE_ABYSSPOINT.
		return new SmSystemMessage(1300965, abyssPoints.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ItemChargeSuccess(string itemName, int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_SUCCESS.
		return new SmSystemMessage(1400887, itemName, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ItemCharge2Success(string itemName, int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_SUCCESS.
		return new SmSystemMessage(1401335, itemName, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ItemChargeAllComplete()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_ALL_COMPLETE.
		return new SmSystemMessage(1400892);
	}

	public static SmSystemMessage ItemCharge2AllComplete()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_ALL_COMPLETE.
		return new SmSystemMessage(1401340);
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

	public static SmSystemMessage HousingBidSuccess(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_SUCCESS.
		return new SmSystemMessage(1401265, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingBidFail()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_FAIL.
		return new SmSystemMessage(1401348);
	}

	public static SmSystemMessage HousingCantOwnNotCompleteQuest(int questId)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_OWN_NOT_COMPLETE_QUEST.
		return new SmSystemMessage(1401277, questId.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingCantBidTimeout()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_TIMEOUT.
		return new SmSystemMessage(1401274);
	}

	public static SmSystemMessage HousingCantBidMyHouse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_MY_HOUSE.
		return new SmSystemMessage(1401221);
	}

	public static SmSystemMessage HousingCantBidGraceHouse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_GRACE_HOUSE.
		return new SmSystemMessage(1401224);
	}

	public static SmSystemMessage HousingCantBidOverdue()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_OVERDUE.
		return new SmSystemMessage(1401349);
	}

	public static SmSystemMessage HousingCantBidLowLevel(int minLevel)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_LOW_LEVEL.
		return new SmSystemMessage(1401225, minLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingOverdue()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OVERDUE.
		return new SmSystemMessage(1401226);
	}

	public static SmSystemMessage HousingSequestrate()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_SEQUESTRATE.
		return new SmSystemMessage(1401227);
	}

	public static SmSystemMessage HousingCantBidAlreadyHighest()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_SUCC_BID_HOUSE.
		return new SmSystemMessage(1401222);
	}

	public static SmSystemMessage HousingCantBidOtherHouse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_OTHER_HOUSE.
		return new SmSystemMessage(1401223);
	}

	public static SmSystemMessage HousingCantBidNotEnoughMoney(long kinah)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_NOT_ENOUGH_MONEY.
		return new SmSystemMessage(1401283, kinah.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingCantBidExcessAccount()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_EXCESS_ACCOUNT.
		return new SmSystemMessage(1401497);
	}

	public static SmSystemMessage HousingCantBidLower()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_LOWER.
		return new SmSystemMessage(1401307);
	}

	public static SmSystemMessage HousingPriceChange(long kinah)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_PRICE_CHANGE.
		return new SmSystemMessage(1401324, kinah.ToString(System.Globalization.CultureInfo.InvariantCulture));
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

	public static SmSystemMessage HousingCantAuctionTimeout()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_AUCTION_TIMEOUT.
		return new SmSystemMessage(1401308);
	}

	public static SmSystemMessage HousingCantAuctionOverdue()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_AUCTION_OVERDUE.
		return new SmSystemMessage(1401317);
	}

	public static SmSystemMessage HousingAuctionMyHouse(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_MY_HOUSE.
		return new SmSystemMessage(1401268, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingAuctionAlreadyRegistered()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_FAIL_ALREADY_REGISTED.
		return new SmSystemMessage(1401372);
	}

	public static SmSystemMessage HousingFeeFree()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_F2P_CASH_HOUSE_FEE_FREE.
		return new SmSystemMessage(1401445);
	}

	public static SmSystemMessage HousingOrderOpenDoor()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_OPEN_DOOR.
		return new SmSystemMessage(1401379);
	}

	public static SmSystemMessage HousingOrderCloseDoorWithoutFriends()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_CLOSE_DOOR_WITHOUT_FRIENDS.
		return new SmSystemMessage(1401380);
	}

	public static SmSystemMessage HousingOrderCloseDoorAll()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_CLOSE_DOOR_ALL.
		return new SmSystemMessage(1401381);
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

	public static SmSystemMessage AccuseInfoNormal()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_INFO_NORMAL.
		return new SmSystemMessage(1400076);
	}

	public static SmSystemMessage DoNotAccuse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DO_NOT_ACCUSE.
		return new SmSystemMessage(1400020);
	}

	public static SmSystemMessage InvalidTarget()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_INVALID_TARGET.
		return new SmSystemMessage(1300823);
	}

	public static SmSystemMessage AccuseSubmit(string playerName, string remainingReports)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_SUBMIT.
		return new SmSystemMessage(1390258, playerName, remainingReports);
	}

	public static SmSystemMessage AccuseCountInfo(string remainingReports)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_COUNT_INFO.
		return new SmSystemMessage(1400091, remainingReports);
	}

	public static SmSystemMessage DiceCustomMe(int roll, int maxRoll)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_CUSTOM_ME.
		return new SmSystemMessage(
			1400126,
			roll.ToString(System.Globalization.CultureInfo.InvariantCulture),
			maxRoll.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage DiceCustomOther(string playerName, int roll, int maxRoll)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_CUSTOM_OTHER.
		return new SmSystemMessage(
			1400127,
			playerName,
			roll.ToString(System.Globalization.CultureInfo.InvariantCulture),
			maxRoll.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
