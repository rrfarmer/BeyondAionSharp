namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/house/HouseBids plus SM_HOUSE_BIDS row/header fields.
public sealed record HouseAuctionBidPage(
	IReadOnlyList<HouseAuctionBidSummary> Bids,
	int LastBidListIndex,
	long LastBidKinah,
	int RegisteredHouseListIndex,
	long RegisteredHouseStartingPrice)
{
	public static HouseAuctionBidPage Empty { get; } = new(
		Array.Empty<HouseAuctionBidSummary>(),
		LastBidListIndex: 0,
		LastBidKinah: 0,
		RegisteredHouseListIndex: 0,
		RegisteredHouseStartingPrice: 0);
}

public sealed record HouseAuctionBidSummary(
	int ListIndex,
	int HouseObjectId,
	int LandId,
	int AddressId,
	int BuildingId,
	int HouseTypeId,
	long HighestBidKinah,
	int BidCount,
	int RemainingAuctionSeconds);
