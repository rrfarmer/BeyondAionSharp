using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum TradeSellToShopPlanStatus
{
	PlanCreated,
	BlockedCannotTrade,
	BlockedMissingItem,
	BlockedMissingTemplate,
	BlockedInvalidPurchaseItem,
	BlockedNotSellable,
	BlockedCountExceedsAvailable,
	BlockedRepurchaseItemCreateFailed,
	BlockedKinahAddFailed,
}

public sealed record TradeSellToShopItemRequest(
	int ItemObjectId,
	long Count,
	bool IsSellable = true,
	long? SellLimitAdjustedCount = null);

public sealed record TradeSellToShopPlan(
	TradeSellToShopPlanStatus Status,
	IReadOnlyList<int> SellerDeletedItemObjectIds,
	IReadOnlyList<InventoryItem> SellerItemUpdates,
	IReadOnlyList<RepurchaseSourceItem> RepurchaseItems,
	InventoryItem? KinahUpdate,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class TradeSellToShopPlanService
{
	private const int CubeStorageId = 0;
	private const long FirstAvailableSlot = 65535;

	public static TradeSellToShopPlan CreatePlan(
		bool canTrade,
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<TradeSellToShopItemRequest> tradeItems,
		ItemTemplateTable itemTemplates,
		TradeListTemplateSummary? purchaseTemplate,
		GoodsListTable? goodsLists,
		int sellModifier,
		Func<int> nextObjectId)
	{
		// Java parity: services/TradeService.performSellToShop.
		if (!canTrade)
			return Block(
				TradeSellToShopPlanStatus.BlockedCannotTrade,
				"TradeService.performSellToShop -> !PlayerRestrictions.canTrade(player) -> false");

		var sellerDeletes = new List<int>();
		var sellerUpdates = new List<InventoryItem>();
		var repurchaseItems = new List<RepurchaseSourceItem>();
		var kinahReward = 0L;

		foreach (var tradeItem in tradeItems)
		{
			var item = inventoryItems.FirstOrDefault(candidate => candidate.ObjectId == tradeItem.ItemObjectId);
			if (item == null)
				return Block(
					TradeSellToShopPlanStatus.BlockedMissingItem,
					"TradeService.performSellToShop -> inventory.getItemByObjId(...) == null -> false");

			var template = itemTemplates.GetItemTemplate(item.ItemId);
			if (template == null)
				return Block(
					TradeSellToShopPlanStatus.BlockedMissingTemplate,
					"TradeService.performSellToShop -> item.getItemTemplate() missing; non-live planner blocks before mutation");

			var sellReward = CalculateSellReward(template, purchaseTemplate, goodsLists, item.ItemId, sellModifier, tradeItem.IsSellable, out var blockedStatus);
			if (blockedStatus != null)
				return Block(
					blockedStatus.Value,
					blockedStatus == TradeSellToShopPlanStatus.BlockedInvalidPurchaseItem
						? "TradeService.performSellToShop -> item id not found in purchase template goods list -> false"
						: "TradeService.performSellToShop -> !item.isSellable() -> STR_BUY_SELL_ITEM_CAN_NOT_BE_SELLED_TO_NPC and false");

			var count = tradeItem.SellLimitAdjustedCount ?? tradeItem.Count;
			if (count == 0)
				break;

			var remainingCount = item.Count - count;
			if (remainingCount < 0)
				return Block(
					TradeSellToShopPlanStatus.BlockedCountExceedsAvailable,
					"TradeService.performSellToShop -> item.getItemCount() - count < 0 -> audit and false");

			var repurchasePrice = sellReward * count;
			if (remainingCount == 0)
			{
				sellerDeletes.Add(item.ObjectId);
				repurchaseItems.Add(new RepurchaseSourceItem(CopyInventoryItem(item), repurchasePrice));
			}
			else
			{
				sellerUpdates.Add(CopyInventoryItem(item, remainingCount));
				var repurchaseObjectId = nextObjectId();
				if (repurchaseObjectId == 0)
					return Block(
						TradeSellToShopPlanStatus.BlockedRepurchaseItemCreateFailed,
						"ItemFactory.newItem source object id allocation failed in non-live planner");
				var repurchaseItem = InventoryItemFactory.CreateNewItem(
					repurchaseObjectId,
					template,
					count,
					player.ObjectId,
					CubeStorageId,
					FirstAvailableSlot);
				repurchaseItems.Add(new RepurchaseSourceItem(repurchaseItem, repurchasePrice));
			}

			kinahReward += repurchasePrice;
		}

		var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == InventoryItemFactory.KinahItemId && item.Location == CubeStorageId);
		InventoryItem? kinahUpdate;
		if (kinahItem == null)
		{
			var kinahObjectId = nextObjectId();
			if (kinahObjectId == 0)
				return Block(
					TradeSellToShopPlanStatus.BlockedKinahAddFailed,
					"Storage.increaseKinah would create a Kinah row; non-live object id allocation failed");
			var kinahTemplate = itemTemplates.GetItemTemplate(InventoryItemFactory.KinahItemId);
			kinahUpdate = kinahTemplate == null
				? new InventoryItem
				{
					ObjectId = kinahObjectId,
					ItemId = InventoryItemFactory.KinahItemId,
					Count = kinahReward,
					OwnerId = player.ObjectId,
					Location = CubeStorageId,
					Slot = FirstAvailableSlot,
				}
				: InventoryItemFactory.CreateNewItem(kinahObjectId, kinahTemplate, kinahReward, player.ObjectId, CubeStorageId, FirstAvailableSlot);
		}
		else
		{
			kinahUpdate = CopyInventoryItem(kinahItem, kinahItem.Count + kinahReward);
		}

		return new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			sellerDeletes,
			sellerUpdates,
			repurchaseItems,
			kinahUpdate,
			"TradeService.performSellToShop -> delete/decrease sold item -> setRepurchasePrice -> RepurchaseService.addRepurchaseItems -> inventory.increaseKinah");
	}

	private static long CalculateSellReward(
		ItemTemplateSummary template,
		TradeListTemplateSummary? purchaseTemplate,
		GoodsListTable? goodsLists,
		int itemId,
		int sellModifier,
		bool isSellable,
		out TradeSellToShopPlanStatus? blockedStatus)
	{
		blockedStatus = null;
		if (purchaseTemplate != null)
		{
			var valid = purchaseTemplate.GoodsListIds
				.Select(goodsListId => goodsLists?.GetGoodsPurchaseListById(goodsListId))
				.Where(goodsList => goodsList != null)
				.Any(goodsList => goodsList!.ItemSummaries.Any(item => item.Id == itemId));
			if (!valid)
			{
				blockedStatus = TradeSellToShopPlanStatus.BlockedInvalidPurchaseItem;
				return 0;
			}
			return (long)(template.Price * purchaseTemplate.BuyPriceRate / 100D);
		}

		if (!isSellable)
		{
			blockedStatus = TradeSellToShopPlanStatus.BlockedNotSellable;
			return 0;
		}

		return PricesService.GetSellReward(template.Price, sellModifier);
	}

	private static TradeSellToShopPlan Block(TradeSellToShopPlanStatus status, string javaSource)
	{
		return new TradeSellToShopPlan(
			status,
			SellerDeletedItemObjectIds: Array.Empty<int>(),
			SellerItemUpdates: Array.Empty<InventoryItem>(),
			RepurchaseItems: Array.Empty<RepurchaseSourceItem>(),
			KinahUpdate: null,
			javaSource);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long? count = null)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count ?? item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PersistentState = item.PersistentState,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}
}
