using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseOwnerInfo : GameServerPacket
{
	public const int PacketOpCode = 263;
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
		_townLevel = 0;
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
		var questId = player.Race == "ELYOS" ? 18802 : 28802;
		return player.Quests.Any(quest => quest.QuestId == questId && quest.IsComplete);
	}
}
