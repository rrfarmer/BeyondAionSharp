using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreBoughtItemsPlanServiceTests
{
	[Fact]
	public void CreatePlan_MapsActionZeroTradeItemIndicesToPrivateStoreRequests()
	{
		var packet = CreatePacket(new CmBuyItemEntry(1, 3));
		var storeItems = new[]
		{
			new PrivateStoreListedItemSummary(0, ItemObjectId: 3001, ItemId: 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword"),
			new PrivateStoreListedItemSummary(1, ItemObjectId: 3002, ItemId: 182003001, Count: 5, PricePerItem: 300, ItemName: "Practice Bundle"),
		};

		var plan = PrivateStoreBoughtItemsPlanService.CreatePlan(packet.Items, storeItems);

		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		var boughtItem = Assert.Single(plan.BoughtItems);
		Assert.Equal((1, 3002, 182003001, 3L, 300L, "Practice Bundle"),
			(boughtItem.StoreIndex, boughtItem.ItemObjectId, boughtItem.ItemId, boughtItem.Count, boughtItem.PricePerItem, boughtItem.ItemName));
	}

	[Fact]
	public void CreatePlan_AllowsEmptyTradeListSoSellStoreItemCanApplyNoBoughtItemsGuard()
	{
		var packet = CreatePacket([]);

		var plan = PrivateStoreBoughtItemsPlanService.CreatePlan(packet.Items, []);

		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.PlanCreated, plan.Status);
		Assert.Empty(plan.BoughtItems);
		Assert.Contains("getBoughtItems", plan.JavaSource, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(2)]
	public void CreatePlan_InvalidStoreIndexReturnsNullEquivalent(int storeIndex)
	{
		var packet = CreatePacket(new CmBuyItemEntry(storeIndex, 1));
		var storeItems = new[]
		{
			new PrivateStoreListedItemSummary(0, ItemObjectId: 3001, ItemId: 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword"),
			new PrivateStoreListedItemSummary(1, ItemObjectId: 3002, ItemId: 182003001, Count: 5, PricePerItem: 300, ItemName: "Practice Bundle"),
		};

		var plan = PrivateStoreBoughtItemsPlanService.CreatePlan(packet.Items, storeItems);

		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.BlockedInvalidStoreIndex, plan.Status);
		Assert.Equal(storeIndex, plan.InvalidStoreIndex);
		Assert.Empty(plan.BoughtItems);
	}

	[Fact]
	public void CreatePlan_CountGreaterThanStoreItemCountReturnsNullEquivalent()
	{
		var packet = CreatePacket(new CmBuyItemEntry(0, 2));
		var storeItems = new[]
		{
			new PrivateStoreListedItemSummary(0, ItemObjectId: 3001, ItemId: 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword"),
		};

		var plan = PrivateStoreBoughtItemsPlanService.CreatePlan(packet.Items, storeItems);

		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.BlockedCountExceedsStoreItemCount, plan.Status);
		Assert.Equal(0, plan.InvalidStoreIndex);
		Assert.Equal(2, plan.RequestedCount);
		Assert.Equal(1, plan.AvailableCount);
		Assert.Empty(plan.BoughtItems);
	}

	[Fact]
	public void CreatePlan_LaterInvalidIndexDropsMappedItemsLikeJavaNullReturn()
	{
		var packet = CreatePacket(new CmBuyItemEntry(0, 1), new CmBuyItemEntry(7, 1));
		var storeItems = new[]
		{
			new PrivateStoreListedItemSummary(0, ItemObjectId: 3001, ItemId: 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword"),
		};

		var plan = PrivateStoreBoughtItemsPlanService.CreatePlan(packet.Items, storeItems);

		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.BlockedInvalidStoreIndex, plan.Status);
		Assert.Empty(plan.BoughtItems);
	}

	private static CmBuyItem CreatePacket(params CmBuyItemEntry[] entries)
	{
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(TradeActionId);
		buffer.WriteH(entries.Length);
		foreach (var entry in entries)
		{
			buffer.WriteD(entry.ItemObjectId);
			buffer.WriteQ(entry.Count);
		}

		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private const int SellerObjectId = 7001;
	private const int TradeActionId = 0;
}
