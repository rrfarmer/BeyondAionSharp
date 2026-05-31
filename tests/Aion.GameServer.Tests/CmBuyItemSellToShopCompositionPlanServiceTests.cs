using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemSellToShopCompositionPlanServiceTests
{
	[Fact]
	public void CreatePlan_ChainsParsedActionOneItemsToSellToShopDispatch()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1), new CmBuyItemEntry(201, 3)]);
		var sellPlan = CreateSellPlan([new TradeSellToShopItemRequest(200, 1), new TradeSellToShopItemRequest(201, 3)]);

		var plan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
			new CmBuyItemSellToShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanBuy: true,
				NpcCanPurchase: false,
				PurchaseTemplate: null,
				SellToShopPlan: sellPlan));

		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellToShop, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal([200, 201], plan.TradeItems.Select(item => item.ItemObjectId).ToArray());
		Assert.Contains(CmBuyItemSellToShopCompositionStep.AttachSellToShopPlan, plan.Steps);

		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.Dispatch);
		Assert.False(dispatch.IsLive);
		Assert.False(dispatch.DispatchesAbyssApSell);
		Assert.Same(sellPlan, dispatch.SellToShopPlan);
		Assert.Equal([200, 201], dispatch.TradeItems.Select(item => item.ItemObjectId).ToArray());
	}

	[Fact]
	public void CreatePlan_AbyssPurchaseTemplateDispatchesApSellWithoutSellToShopPlan()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1)]);
		var purchaseTemplate = new TradeListTemplateSummary(203060, [129], NpcType: "ABYSS", BuyPriceRate: 35);
		var sellPlan = CreateSellPlan([new TradeSellToShopItemRequest(200, 1)]);

		var plan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
			new CmBuyItemSellToShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanBuy: false,
				NpcCanPurchase: true,
				PurchaseTemplate: purchaseTemplate,
				SellToShopPlan: sellPlan));

		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellForApToShop, plan.Status);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.Dispatch);
		Assert.True(dispatch.DispatchesAbyssApSell);
		Assert.Same(purchaseTemplate, dispatch.PurchaseTemplate);
		Assert.Null(dispatch.SellToShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreatePlan_ParserAuditStopsBeforeRunDispatch()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1), new CmBuyItemEntry(0, 1)]);

		var plan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
			new CmBuyItemSellToShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc));

		Assert.True(packet.IsAudit);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.ReadAudit, plan.Status);
		Assert.Equal([200], plan.TradeItems.Select(item => item.ItemObjectId).ToArray());
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_NonSellActionSkipsBeforeTargetDispatch()
	{
		var packet = CreatePacket(2, [new CmBuyItemEntry(200, 1)]);

		var plan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
			new CmBuyItemSellToShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc));

		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.SkippedNonSellAction, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Theory]
	[InlineData(CmBuyItemRunTargetKind.Unknown, CmBuyItemSellToShopCompositionPlanStatus.SkippedUnknownTarget)]
	[InlineData(CmBuyItemRunTargetKind.Player, CmBuyItemSellToShopCompositionPlanStatus.SkippedNonNpcTarget)]
	[InlineData(CmBuyItemRunTargetKind.Pet, CmBuyItemSellToShopCompositionPlanStatus.SkippedNonNpcTarget)]
	public void CreatePlan_AppliesTargetBranchBeforeNpcGates(
		CmBuyItemRunTargetKind targetKind,
		CmBuyItemSellToShopCompositionPlanStatus expectedStatus)
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1)]);

		var plan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
			new CmBuyItemSellToShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: targetKind,
				InteractionAllowed: false,
				NpcCanBuy: false,
				NpcCanPurchase: false));

		Assert.Equal(expectedStatus, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_InteractionAuditWinsBeforeCanBuyOrPurchase()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1)]);

		var plan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
			new CmBuyItemSellToShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				InteractionAllowed: false,
				NpcCanBuy: false,
				NpcCanPurchase: false));

		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.RunAudit, plan.Status);
		Assert.Equal("might be abusing CM_BUY_ITEM: no right trading with npc", plan.AuditReason);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_SkipsNpcWithoutBuyOrPurchaseSupport()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1)]);

		var plan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
			new CmBuyItemSellToShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanBuy: false,
				NpcCanPurchase: false));

		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.SkippedNpcCannotBuyOrPurchase, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_MissingPlayerSkipsLikeJavaRunImpl()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1)]);

		var plan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
			new CmBuyItemSellToShopCompositionInput(
				packet,
				PlayerPresent: false,
				TargetKind: CmBuyItemRunTargetKind.Npc));

		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.SkippedMissingPlayer, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	private static CmBuyItem CreatePacket(int tradeActionId, IReadOnlyList<CmBuyItemEntry> entries)
	{
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(entries.Count);
		foreach (var entry in entries)
		{
			buffer.WriteD(entry.ItemObjectId);
			buffer.WriteQ(entry.Count);
		}

		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static TradeSellToShopPlan CreateSellPlan(IReadOnlyList<TradeSellToShopItemRequest> tradeItems)
	{
		var player = new Player { ObjectId = 1001 };
		var nextObjectId = 99;
		return TradeSellToShopPlanService.CreatePlan(
			canTrade: true,
			player,
			inventoryItems:
			[
				Item(99, KinahItemId, 1_000, player.ObjectId),
				Item(200, SwordItemId, 1, player.ObjectId),
				Item(201, StackableItemId, 5, player.ObjectId),
			],
			tradeItems,
			CreateTemplates(),
			purchaseTemplate: null,
			goodsLists: null,
			sellModifier: 20,
			() => ++nextObjectId);
	}

	private static InventoryItem Item(int objectId, int itemId, long count, int ownerId)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			OwnerId = ownerId,
			Location = 0,
			Slot = 65535,
		};
	}

	private static ItemTemplateTable CreateTemplates()
	{
		return new ItemTemplateTable(
		[
			Template(KinahItemId, price: 0, maxStackCount: 1),
			Template(SwordItemId, price: 1_000, maxStackCount: 1),
			Template(StackableItemId, price: 100, maxStackCount: 10),
		]);
	}

	private static ItemTemplateSummary Template(int itemId, long price, int maxStackCount)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			DescriptionId: 1,
			Mask: 0,
			Level: 1,
			ItemGroup: "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: maxStackCount,
			Price: price,
			ValidEquipmentSlots: 0);
	}

	private const int SellerObjectId = 7001;
	private const int KinahItemId = 182400001;
	private const int SwordItemId = 100000001;
	private const int StackableItemId = 182003001;
}
