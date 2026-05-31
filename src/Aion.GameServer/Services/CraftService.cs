using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class CraftService
{
	private readonly WorldNpcResourceStatsService _resourceStats;
	private readonly ItemTemplateTable? _itemTemplates;

	public CraftService(WorldNpcResourceStatsService resourceStats, ItemTemplateTable? itemTemplates = null)
	{
		_resourceStats = resourceStats;
		_itemTemplates = itemTemplates;
	}

	public async ValueTask<CraftStartDpCostResult> SpendRecipeDpForCraftStartAsync(
		Player? player,
		RecipeTemplateSummary? recipeTemplate,
		int? maxDp = null)
	{
		// Java parity: services/craft/CraftService.checkCraft + startCrafting recipe DP branch.
		if (player == null)
			return CraftStartDpCostResult.MissingPlayer(recipeTemplate?.RecipeId ?? 0);
		if (recipeTemplate == null)
			return CraftStartDpCostResult.MissingRecipe(player.ObjectId, player.Dp);

		var requiredDp = recipeTemplate.Dp;
		if (player.Dp < requiredDp)
			return CraftStartDpCostResult.NotEnoughDp(player.ObjectId, recipeTemplate.RecipeId, requiredDp, player.Dp);

		var previousDp = player.Dp;
		var change = await _resourceStats.AddPlayerDpAsync(player, -requiredDp, maxDp);
		return CraftStartDpCostResult.FromDpChange(change, recipeTemplate.RecipeId, requiredDp, previousDp);
	}

	public CraftFinishProductPlan CreateFinishProductPlan(Player? player, RecipeTemplateSummary? recipeTemplate, int critCount)
	{
		// Java parity: services/craft/CraftService.finishCrafting product-selection branch.
		if (player == null)
			return CraftFinishProductPlan.MissingPlayer(recipeTemplate?.RecipeId ?? 0, critCount);
		if (recipeTemplate == null)
			return CraftFinishProductPlan.MissingRecipe(player.ObjectId, critCount);

		var usesComboProduct = critCount > 0;
		var productItemId = usesComboProduct
			? recipeTemplate.GetComboProduct(critCount)
			: recipeTemplate.ProductId;
		if (!productItemId.HasValue)
			return CraftFinishProductPlan.MissingComboProduct(player.ObjectId, recipeTemplate.RecipeId, critCount, recipeTemplate.Quantity);

		var productTemplate = _itemTemplates?.GetItemTemplate(productItemId.Value);
		var marksCreatorOnEquipment = productTemplate is { IsWeapon: true } or { IsArmor: true };
		return CraftFinishProductPlan.Planned(
			player.ObjectId,
			recipeTemplate.RecipeId,
			critCount,
			productItemId.Value,
			recipeTemplate.Quantity,
			usesComboProduct,
			marksCreatorOnEquipment ? player.Name : null,
			marksCreatorOnEquipment);
	}

	public CraftFinishRewardPlan CreateFinishRewardPlan(
		Player? player,
		IReadOnlyList<InventoryItem>? inventoryItems,
		RecipeTemplateSummary? recipeTemplate,
		int critCount,
		Func<int> nextObjectId,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		// Java parity: services/craft/CraftService.finishCrafting -> ItemService.addItem(..., ItemAddType.CRAFTED_ITEM, ItemUpdateType.INC_ITEM_COLLECT).
		var productPlan = CreateFinishProductPlan(player, recipeTemplate, critCount);
		if (productPlan.Status != CraftFinishProductStatus.Planned)
			return CraftFinishRewardPlan.FromProductFailure(productPlan);
		if (player == null)
			return CraftFinishRewardPlan.FromProductFailure(productPlan);

		var itemTemplate = _itemTemplates?.GetItemTemplate(productPlan.ProductItemId);
		if (itemTemplate == null)
			return CraftFinishRewardPlan.MissingItemTemplate(productPlan);

		var addPlan = InventoryAddService.CreateAddItemPlan(
			player,
			inventoryItems ?? Array.Empty<InventoryItem>(),
			itemTemplate,
			productPlan.Quantity,
			nextObjectId,
			allowInventoryOverflow: false,
			itemTemplates: _itemTemplates);

		var addedItems = productPlan.MarksCreatorOnEquipment && !string.IsNullOrEmpty(productPlan.CreatorName)
			? addPlan.AddedItems.Select(item => CopyInventoryItem(item, productPlan.CreatorName!)).ToArray()
			: addPlan.AddedItems;

		var packets = new List<GameServerPacket>();
		foreach (var updatedItem in addPlan.UpdatedItems)
		{
			packets.Add(new SmInventoryUpdateItem(
				updatedItem,
				itemTemplate,
				SmInventoryUpdateItem.IncreaseItemCollect,
				GetGeneralInfoWarehouseRestrictionFlag(updatedItem.ItemId, itemRestrictionCleanups)));
		}

		foreach (var addedItem in addedItems)
		{
			packets.Add(SmInventoryAddItem.CreateCraftedItem(
				addedItem,
				itemTemplate,
				GetGeneralInfoWarehouseRestrictionFlag(addedItem.ItemId, itemRestrictionCleanups)));
		}

		return CraftFinishRewardPlan.Success(
			productPlan,
			itemTemplate,
			addPlan.UpdatedItems,
			addedItems,
			addPlan.RemainingCount,
			addPlan.InventoryFull,
			packets,
			shouldSendInventoryFullMessage: addPlan.RemainingCount > 0 && addPlan.InventoryFull);
	}

	private static int GetGeneralInfoWarehouseRestrictionFlag(int itemId, ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		return itemRestrictionCleanups?.HasAccountOrLegionWarehouseStorabilityDisabled(itemId) == true ? 3 : 0;
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, string creatorName)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = creatorName,
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
			PendingTuneResult = item.PendingTuneResult,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}
}

public sealed record CraftStartDpCostResult(
	CraftStartDpCostStatus Status,
	int ObjectId,
	int RecipeId,
	int RequiredDp,
	int PreviousDp,
	int CurrentDp,
	WorldNpcResourceChangeResult? Change = null)
{
	public static CraftStartDpCostResult MissingPlayer(int recipeId)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.MissingPlayer,
			0,
			recipeId,
			0,
			0,
			0);
	}

	public static CraftStartDpCostResult MissingRecipe(int objectId, int currentDp)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.MissingRecipe,
			objectId,
			0,
			0,
			currentDp,
			currentDp);
	}

	public static CraftStartDpCostResult NotEnoughDp(int objectId, int recipeId, int requiredDp, int currentDp)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.NotEnoughDp,
			objectId,
			recipeId,
			requiredDp,
			currentDp,
			currentDp);
	}

	public static CraftStartDpCostResult FromDpChange(
		WorldNpcResourceChangeResult change,
		int recipeId,
		int requiredDp,
		int previousDp)
	{
		var status = change.Status is WorldNpcResourceChangeStatus.StartingClass
			or WorldNpcResourceChangeStatus.MissingTarget
			or WorldNpcResourceChangeStatus.MissingMaxResource
			? CraftStartDpCostStatus.DpBoundarySkipped
			: CraftStartDpCostStatus.Applied;
		return new CraftStartDpCostResult(
			status,
			change.ObjectId,
			recipeId,
			requiredDp,
			previousDp,
			change.CurrentValue,
			change);
	}
}

public enum CraftStartDpCostStatus
{
	Applied,
	MissingPlayer,
	MissingRecipe,
	NotEnoughDp,
	DpBoundarySkipped,
}

public sealed record CraftFinishProductPlan(
	CraftFinishProductStatus Status,
	int ObjectId,
	int RecipeId,
	int CritCount,
	int ProductItemId,
	int Quantity,
	bool UsesComboProduct,
	string? CreatorName,
	bool MarksCreatorOnEquipment)
{
	public static CraftFinishProductPlan MissingPlayer(int recipeId, int critCount)
	{
		return new CraftFinishProductPlan(
			CraftFinishProductStatus.MissingPlayer,
			0,
			recipeId,
			critCount,
			0,
			0,
			false,
			null,
			false);
	}

	public static CraftFinishProductPlan MissingRecipe(int objectId, int critCount)
	{
		return new CraftFinishProductPlan(
			CraftFinishProductStatus.MissingRecipe,
			objectId,
			0,
			critCount,
			0,
			0,
			false,
			null,
			false);
	}

	public static CraftFinishProductPlan MissingComboProduct(int objectId, int recipeId, int critCount, int quantity)
	{
		return new CraftFinishProductPlan(
			CraftFinishProductStatus.MissingComboProduct,
			objectId,
			recipeId,
			critCount,
			0,
			quantity,
			true,
			null,
			false);
	}

	public static CraftFinishProductPlan Planned(
		int objectId,
		int recipeId,
		int critCount,
		int productItemId,
		int quantity,
		bool usesComboProduct,
		string? creatorName,
		bool marksCreatorOnEquipment)
	{
		return new CraftFinishProductPlan(
			CraftFinishProductStatus.Planned,
			objectId,
			recipeId,
			critCount,
			productItemId,
			quantity,
			usesComboProduct,
			creatorName,
			marksCreatorOnEquipment);
	}
}

public enum CraftFinishProductStatus
{
	Planned,
	MissingPlayer,
	MissingRecipe,
	MissingComboProduct,
}

public sealed record CraftFinishRewardPlan(
	CraftFinishRewardStatus Status,
	CraftFinishProductPlan ProductPlan,
	ItemTemplateSummary? ItemTemplate,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<InventoryItem> AddedItems,
	long RemainingCount,
	bool InventoryFull,
	IReadOnlyList<GameServerPacket> Packets,
	bool ShouldSendInventoryFullMessage)
{
	public static CraftFinishRewardPlan FromProductFailure(CraftFinishProductPlan productPlan)
	{
		return new CraftFinishRewardPlan(
			MapProductStatus(productPlan.Status),
			productPlan,
			null,
			Array.Empty<InventoryItem>(),
			Array.Empty<InventoryItem>(),
			0,
			false,
			Array.Empty<GameServerPacket>(),
			false);
	}

	private static CraftFinishRewardStatus MapProductStatus(CraftFinishProductStatus status)
	{
		return status switch
		{
			CraftFinishProductStatus.MissingPlayer => CraftFinishRewardStatus.MissingPlayer,
			CraftFinishProductStatus.MissingRecipe => CraftFinishRewardStatus.MissingRecipe,
			CraftFinishProductStatus.MissingComboProduct => CraftFinishRewardStatus.MissingComboProduct,
			_ => CraftFinishRewardStatus.Planned,
		};
	}

	public static CraftFinishRewardPlan MissingItemTemplate(CraftFinishProductPlan productPlan)
	{
		return new CraftFinishRewardPlan(
			CraftFinishRewardStatus.MissingItemTemplate,
			productPlan,
			null,
			Array.Empty<InventoryItem>(),
			Array.Empty<InventoryItem>(),
			productPlan.Quantity,
			false,
			Array.Empty<GameServerPacket>(),
			false);
	}

	public static CraftFinishRewardPlan Success(
		CraftFinishProductPlan productPlan,
		ItemTemplateSummary itemTemplate,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		long remainingCount,
		bool inventoryFull,
		IReadOnlyList<GameServerPacket> packets,
		bool shouldSendInventoryFullMessage)
	{
		var status = remainingCount == 0
			? CraftFinishRewardStatus.Planned
			: inventoryFull
				? CraftFinishRewardStatus.InventoryFull
				: CraftFinishRewardStatus.PartialOverflow;
		return new CraftFinishRewardPlan(
			status,
			productPlan,
			itemTemplate,
			updatedItems,
			addedItems,
			remainingCount,
			inventoryFull,
			packets,
			shouldSendInventoryFullMessage);
	}
}

public enum CraftFinishRewardStatus
{
	Planned,
	MissingPlayer,
	MissingRecipe,
	MissingComboProduct,
	MissingItemTemplate,
	InventoryFull,
	PartialOverflow,
}
