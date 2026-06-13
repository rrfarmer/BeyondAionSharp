using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseOwnerInfo : GameServerPacket
{
	public const int PacketOpCode = 263;
	private const string HousingOverdueMailSenderPrefix = "$$HS_OVERDUE_";
	private const int HasOwner = 1 << 0;
	private const int SingleHouse = 1 << 1;
	private const int BiddingAllowed = 1 << 2;

	private readonly Func<DateTime> _clock;
	private readonly JavaCronSchedule? _auctionEndSchedule;
	private readonly PlayerHouse? _activeHouse;
	private readonly PlayerHouse? _inactiveHouse;
	private readonly int _ownerState;
	private readonly int _townLevel;

	public SmHouseOwnerInfo(Player player, Func<DateTime>? clock = null, JavaCronSchedule? auctionEndSchedule = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_OWNER_INFO(Player).
		_clock = clock ?? (() => DateTime.Now);
		_auctionEndSchedule = auctionEndSchedule;
		_activeHouse = player.Houses.FirstOrDefault(house => !house.IsInactive);
		_inactiveHouse = player.Houses.FirstOrDefault(house => house.IsInactive);
		_ownerState = _activeHouse == null
			? SingleHouse | (CanBidForHouse(player) ? BiddingAllowed : 0)
			: HasOwner | BiddingAllowed;
		// Java parity: model/house/House.getTownLevel written by SM_HOUSE_OWNER_INFO.writeImpl.
		_townLevel = _activeHouse?.TownLevel ?? 0;
	}

	public static IReadOnlyList<SmSystemMessage> CreateLoginSystemMessages(
		Player player,
		bool payEnabled = true,
		Func<DateTime>? clock = null)
	{
		// Java parity: services/HousingService.onPlayerLogin maintenance/sequestration notices.
		var activeHouse = player.Houses.FirstOrDefault(house => !house.IsInactive);
		if (activeHouse != null)
		{
			var now = clock?.Invoke() ?? DateTime.Now;
			if (payEnabled && activeHouse.NextPay != null && activeHouse.NextPay.Value <= now)
				return [SmSystemMessage.HousingOverdue()];
			return Array.Empty<SmSystemMessage>();
		}

		return player.Mailbox.Any(IsFinalOverdueLetter)
			? [SmSystemMessage.HousingSequestrate()]
			: Array.Empty<SmSystemMessage>();
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_HOUSE_OWNER_INFO.writeImpl.
		buffer.WriteD(_activeHouse?.AddressId ?? 0);
		buffer.WriteD(_activeHouse?.BuildingId ?? 0);
		buffer.WriteC(_ownerState);
		buffer.WriteC(_townLevel);
		buffer.WriteD(CalculateWeeksUntilNextPay());
		buffer.WriteD(_inactiveHouse?.AddressId ?? 0);
		buffer.WriteD(_inactiveHouse?.BuildingId ?? 0);
		buffer.WriteD(_inactiveHouse?.GetGraceSeconds(_clock, _auctionEndSchedule) ?? 0);
	}

	private int CalculateWeeksUntilNextPay()
	{
		if (_activeHouse == null)
			return 0;
		if (_activeHouse.NextPay == null)
			return 1;

		var now = _clock();
		var isSundayAfterAuction = now.DayOfWeek == DayOfWeek.Sunday && now.Hour >= 12;
		var days = (long)Math.Floor((_activeHouse.NextPay.Value - now).TotalDays);
		var weeks = (int)(days / 7);
		if (days < 0 && isSundayAfterAuction)
			weeks--;
		else if (days >= 0 && !isSundayAfterAuction)
			weeks++;
		return weeks;
	}

	private static bool CanBidForHouse(Player player)
	{
		// Java parity: services/HousingService.canOwnHouse quest gate.
		var questId = player.Race == global::Aion.GameServer.Model.Race.ELYOS ? 18802 : 28802;
		return player.Quests.Any(quest => quest.QuestId == questId && quest.IsComplete);
	}

	private static bool IsFinalOverdueLetter(PlayerMail mail)
	{
		// Java parity: Mailbox.getNewSystemLetters("$$HS_OVERDUE_") then sender suffix FINAL/3RD.
		return mail.IsUnread
			&& mail.SenderName.StartsWith(HousingOverdueMailSenderPrefix, StringComparison.Ordinal)
			&& (mail.SenderName.EndsWith("FINAL", StringComparison.Ordinal) || mail.SenderName.EndsWith("3RD", StringComparison.Ordinal));
	}
}
