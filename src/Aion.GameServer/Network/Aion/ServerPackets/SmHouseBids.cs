using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseBids : GameServerPacket
{
	public const int PacketOpCode = 256;

	private readonly bool _isFirst;
	private readonly bool _isLast;
	private readonly int _lastBidListIndex;
	private readonly long _lastBidKinah;
	private readonly int _registeredHouseListIndex;
	private readonly long _registeredHouseStartingPrice;

	private SmHouseBids(
		bool isFirst,
		bool isLast,
		int lastBidListIndex,
		long lastBidKinah,
		int registeredHouseListIndex,
		long registeredHouseStartingPrice)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_BIDS header fields.
		_isFirst = isFirst;
		_isLast = isLast;
		_lastBidListIndex = lastBidListIndex;
		_lastBidKinah = lastBidKinah;
		_registeredHouseListIndex = registeredHouseListIndex;
		_registeredHouseStartingPrice = registeredHouseStartingPrice;
	}

	public static SmHouseBids CreateEmpty()
	{
		// Java parity: CM_GET_HOUSE_BIDS with an empty HousingBidService.getBidInfo result.
		return new SmHouseBids(
			isFirst: true,
			isLast: true,
			lastBidListIndex: 0,
			lastBidKinah: 0,
			registeredHouseListIndex: 0,
			registeredHouseStartingPrice: 0);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_isFirst ? 1 : 0);
		buffer.WriteC(_isLast ? 1 : 0);
		buffer.WriteD(_lastBidListIndex);
		buffer.WriteQ(_lastBidKinah);
		buffer.WriteD(_registeredHouseListIndex);
		buffer.WriteQ(_registeredHouseStartingPrice);
		buffer.WriteH(0);
	}
}
