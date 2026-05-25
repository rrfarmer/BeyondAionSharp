using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ApExtractService
{
	private const int CubeStorageId = 0;

	private static readonly HashSet<string> WeaponTargets = new(StringComparer.Ordinal)
	{
		"SWORD", "DAGGER", "MACE", "ORB", "SPELLBOOK", "BOW", "GREATSWORD", "POLEARM", "STAFF",
		"HARP", "GUN", "KEYBLADE", "CANNON",
	};

	private static readonly HashSet<string> ArmorTargets = new(StringComparer.Ordinal)
	{
		"RB_TORSO", "RB_PANTS", "RB_SHOULDER", "RB_GLOVE", "RB_SHOES",
		"CL_TORSO", "CL_PANTS", "CL_SHOULDER", "CL_GLOVE", "CL_SHOES",
		"CH_TORSO", "CH_PANTS", "CH_SHOULDER", "CH_GLOVE", "CH_SHOES",
		"LT_TORSO", "LT_PANTS", "LT_SHOULDER", "LT_GLOVE", "LT_SHOES",
		"PL_TORSO", "PL_PANTS", "PL_SHOULDER", "PL_GLOVE", "PL_SHOES",
		"SHIELD",
	};

	private static readonly HashSet<string> AccessoryTargets = new(StringComparer.Ordinal)
	{
		"NECKLACE", "EARRING", "RING", "BELT", "HEAD",
	};

	public static ApExtractPlan CreateMutationPlan(
		Player player,
		int extractionToolObjectId,
		int targetItemObjectId,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: model/templates/item/actions/ApExtractAction.canAct + act.
		var inventoryItems = player.InventoryItems.ToList();
		var extractionToolItem = FindCubeItem(inventoryItems, extractionToolObjectId);
		var targetItem = FindCubeItem(inventoryItems, targetItemObjectId);
		if (extractionToolItem == null || targetItem == null)
			return ApExtractPlan.Failed(ApExtractFailure.MissingItem);

		var sourceTemplate = itemTemplates.GetItemTemplate(extractionToolItem.ItemId);
		var targetTemplate = itemTemplates.GetItemTemplate(targetItem.ItemId);
		var action = sourceTemplate?.ApExtractAction;
		if (sourceTemplate == null || targetTemplate == null || action == null)
			return ApExtractPlan.Failed(ApExtractFailure.MissingTemplate);

		if (!CanAct(action, sourceTemplate, targetTemplate))
			return ApExtractPlan.Failed(ApExtractFailure.CannotAct);

		if (targetTemplate.RequiredAbyssPoints == 0)
			return ApExtractPlan.Failed(ApExtractFailure.NoAbyssPointValue);

		var abyssPoints = (int)(targetTemplate.RequiredAbyssPoints * action.Rate);
		var abyssPointsPlan = AbyssPointsService.CreateAddApPlan(player, abyssPoints);
		if (!abyssPointsPlan.Applied || abyssPointsPlan.UpdatedRank == null)
			return ApExtractPlan.Failed(ApExtractFailure.AbyssPointsFailed);

		inventoryItems.RemoveAll(item => item.ObjectId == targetItem.ObjectId);
		var sourceMutation = DecreaseItemCount(extractionToolItem);
		if (sourceMutation.UpdatedItem != null)
			ReplaceInventoryItem(inventoryItems, sourceMutation.UpdatedItem);
		else if (sourceMutation.DeletedObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == sourceMutation.DeletedObjectId.Value);

		return new ApExtractPlan(
			ApExtractFailure.None,
			inventoryItems,
			targetItem.ObjectId,
			sourceMutation.UpdatedItem,
			sourceMutation.DeletedObjectId,
			abyssPointsPlan,
			abyssPoints);
	}

	private static bool CanAct(ItemApExtractActionInfo action, ItemTemplateSummary sourceTemplate, ItemTemplateSummary targetTemplate)
	{
		if (!targetTemplate.CanApExtract)
			return false;
		if (sourceTemplate.Level < targetTemplate.Level)
			return false;
		if (!string.Equals(sourceTemplate.Quality, targetTemplate.Quality, StringComparison.Ordinal))
			return false;

		var targetType = GetTargetType(targetTemplate);
		return targetType != null
			&& (string.Equals(action.Target, "EQUIPMENT", StringComparison.Ordinal)
				|| string.Equals(action.Target, targetType, StringComparison.Ordinal));
	}

	private static string? GetTargetType(ItemTemplateSummary targetTemplate)
	{
		if (WeaponTargets.Contains(targetTemplate.ItemGroup))
			return "WEAPON";
		if (ArmorTargets.Contains(targetTemplate.ItemGroup))
			return "ARMOR";
		if (AccessoryTargets.Contains(targetTemplate.ItemGroup))
			return "ACCESSORY";
		return targetTemplate.IsWing ? "WING" : null;
	}

	private static InventoryItem? FindCubeItem(IReadOnlyList<InventoryItem> inventoryItems, int objectId)
	{
		return inventoryItems.FirstOrDefault(item =>
			item.ObjectId == objectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
	}

	private static ItemCountMutation DecreaseItemCount(InventoryItem item)
	{
		return item.Count > 1
			? new ItemCountMutation(CopyInventoryItem(item, item.Count - 1), null)
			: new ItemCountMutation(null, item.ObjectId);
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem update)
	{
		var index = items.FindIndex(item => item.ObjectId == update.ObjectId);
		if (index >= 0)
			items[index] = update;
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
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
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}

	private sealed record ItemCountMutation(InventoryItem? UpdatedItem, int? DeletedObjectId);
}

public enum ApExtractFailure
{
	None,
	MissingItem,
	MissingTemplate,
	CannotAct,
	NoAbyssPointValue,
	AbyssPointsFailed,
}

public sealed record ApExtractPlan(
	ApExtractFailure Failure,
	IReadOnlyList<InventoryItem> InventoryItems,
	int DeletedTargetItemObjectId,
	InventoryItem? SourceItemUpdate,
	int? DeletedSourceItemObjectId,
	AbyssPointsAddPlan? AbyssPointsPlan,
	int AbyssPoints)
{
	public bool Succeeded => Failure == ApExtractFailure.None;
	public PlayerAbyssRank? AbyssRankUpdate => AbyssPointsPlan?.UpdatedRank;

	public static ApExtractPlan Failed(ApExtractFailure failure)
	{
		return new ApExtractPlan(
			failure,
			InventoryItems: Array.Empty<InventoryItem>(),
			DeletedTargetItemObjectId: 0,
			SourceItemUpdate: null,
			DeletedSourceItemObjectId: null,
			AbyssPointsPlan: null,
			AbyssPoints: 0);
	}
}
