namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/gameobjects/BrokerItem restored by dao/BrokerDAO.loadBroker.
public sealed record PlayerBrokerItem(
	int ItemObjectId,
	int ItemId,
	long ItemCount,
	string ItemCreator,
	long Price,
	int SellerId,
	string SellerName,
	string BrokerRace,
	bool IsSold,
	bool IsSettled,
	DateTime ExpireTime,
	DateTime SettleTime,
	bool SplittingAvailable,
	InventoryItem? Item)
{
	public long AveragePrice { get; init; }
}

public sealed record PlayerBrokerItemPage(
	IReadOnlyList<PlayerBrokerItem> Items,
	int TotalItemCount,
	int PageIndex,
	long SettledKinah);

public sealed record PlayerBrokerPriceRange(long LowestPrice, long HighestPrice);

public sealed record PlayerBrokerReturnedItem(
	PlayerBrokerItem BrokerItem,
	InventoryItem ReturnedItem);

public sealed record PlayerBrokerAccountSettlement(
	IReadOnlyList<PlayerBrokerItem> CollectedBrokerItems,
	IReadOnlyList<PlayerBrokerReturnedItem> ReturnedItems,
	InventoryItem? KinahItem);
