using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseBids : GameServerPacket
{
	public const int PacketOpCode = 256;
	private const int StaticBodySize = 28;
	private const int DynamicBodyPartSize = 44;

	private readonly bool _isFirst;
	private readonly bool _isLast;
	private readonly int _lastBidListIndex;
	private readonly long _lastBidKinah;
	private readonly int _registeredHouseListIndex;
	private readonly long _registeredHouseStartingPrice;
	private readonly IReadOnlyList<HouseAuctionBidSummary> _bids;

	private SmHouseBids(
		bool isFirst,
		bool isLast,
		int lastBidListIndex,
		long lastBidKinah,
		int registeredHouseListIndex,
		long registeredHouseStartingPrice,
		IReadOnlyList<HouseAuctionBidSummary> bids)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_BIDS header fields.
		_isFirst = isFirst;
		_isLast = isLast;
		_lastBidListIndex = lastBidListIndex;
		_lastBidKinah = lastBidKinah;
		_registeredHouseListIndex = registeredHouseListIndex;
		_registeredHouseStartingPrice = registeredHouseStartingPrice;
		_bids = bids;
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
			registeredHouseStartingPrice: 0,
			bids: Array.Empty<HouseAuctionBidSummary>());
	}

	public static IReadOnlyList<SmHouseBids> CreatePackets(HouseAuctionBidPage bidPage)
	{
		// Java parity: CM_GET_HOUSE_BIDS DynamicServerPacketBodySplitList<HouseBids>.
		if (bidPage.Bids.Count == 0)
		{
			return
			[
				new SmHouseBids(
					isFirst: true,
					isLast: true,
					lastBidListIndex: bidPage.LastBidListIndex,
					lastBidKinah: bidPage.LastBidKinah,
					registeredHouseListIndex: bidPage.RegisteredHouseListIndex,
					registeredHouseStartingPrice: bidPage.RegisteredHouseStartingPrice,
					bids: Array.Empty<HouseAuctionBidSummary>()),
			];
		}

		var maxRows = Math.Max(1, (MaxUsablePacketBodySize - StaticBodySize) / DynamicBodyPartSize);
		var packets = new List<SmHouseBids>();
		for (var offset = 0; offset < bidPage.Bids.Count; offset += maxRows)
		{
			var isLast = offset + maxRows >= bidPage.Bids.Count;
			var rows = bidPage.Bids.Skip(offset).Take(maxRows).ToArray();
			packets.Add(
				new SmHouseBids(
					isFirst: offset == 0,
					isLast,
					lastBidListIndex: isLast ? bidPage.LastBidListIndex : 0,
					lastBidKinah: isLast ? bidPage.LastBidKinah : 0,
					registeredHouseListIndex: isLast ? bidPage.RegisteredHouseListIndex : 0,
					registeredHouseStartingPrice: isLast ? bidPage.RegisteredHouseStartingPrice : 0,
					bids: rows));
		}

		return packets;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_BIDS.writeImpl.
		buffer.WriteC(_isFirst ? 1 : 0);
		buffer.WriteC(_isLast ? 1 : 0);
		buffer.WriteD(_lastBidListIndex);
		buffer.WriteQ(_lastBidKinah);
		buffer.WriteD(_registeredHouseListIndex);
		buffer.WriteQ(_registeredHouseStartingPrice);
		buffer.WriteH(_bids.Count);
		foreach (var bid in _bids)
		{
			buffer.WriteD(bid.ListIndex);
			buffer.WriteD(bid.LandId);
			buffer.WriteD(bid.AddressId);
			buffer.WriteD(bid.BuildingId);
			buffer.WriteD(bid.HouseTypeId);
			buffer.WriteQ(bid.HighestBidKinah);
			buffer.WriteQ(100000);
			buffer.WriteD(bid.BidCount);
			buffer.WriteD(bid.RemainingAuctionSeconds);
		}
	}
}
