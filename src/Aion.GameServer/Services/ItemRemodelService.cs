using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemRemodelService
{
	public const int PatternReshaperItemId = 168100000;
	public const long RemodelCost = 1000;

	public static ItemRemodelPlan CreateRemodelPlan(
		Player player,
		InventoryItem keepItem,
		ItemTemplateSummary keepTemplate,
		InventoryItem extractItem,
		ItemTemplateSummary extractTemplate,
		ItemTemplateSummary extractSkinTemplate,
		InventoryItem? kinahItem,
		int playerLevel)
	{
		// Java parity: services/item/ItemRemodelService.remodelItem.
		if (playerLevel < 10)
			return ItemRemodelPlan.Failed(ItemRemodelFailure.LevelLimit, keepTemplate, extractSkinTemplate);

		if (HasOppositeGenderRequirement(keepTemplate, extractTemplate))
			return ItemRemodelPlan.Failed(ItemRemodelFailure.OppositeRequirement, keepTemplate, extractTemplate);

		if (kinahItem == null || kinahItem.Count < RemodelCost)
			return ItemRemodelPlan.Failed(ItemRemodelFailure.NotEnoughKinah, keepTemplate, extractSkinTemplate);

		if (extractTemplate.TemplateId == PatternReshaperItemId)
			return CreatePatternReshaperPlan(keepItem, keepTemplate, extractItem, kinahItem);

		if (!IsCompatible(keepTemplate, extractSkinTemplate))
			return ItemRemodelPlan.Failed(ItemRemodelFailure.NotCompatible, keepTemplate, extractSkinTemplate);

		if (!keepTemplate.IsRemodelable)
			return ItemRemodelPlan.Failed(ItemRemodelFailure.NotSkinChangeable, keepTemplate, extractSkinTemplate);

		if (!extractTemplate.IsRemodelable)
			return ItemRemodelPlan.Failed(ItemRemodelFailure.CannotRemoveSkinItem, extractTemplate, extractSkinTemplate);

		if (extractItem.ItemSkin != 0
			&& extractSkinTemplate.RemodelAction?.ExtractType == 2)
		{
			return ItemRemodelPlan.Failed(ItemRemodelFailure.CannotRemoveSkinItem, extractTemplate, extractSkinTemplate);
		}

		var targetUpdate = CopyInventoryItem(
			keepItem,
			itemSkin: extractSkinTemplate.TemplateId,
			color: extractItem.Color,
			setColor: true);
		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - RemodelCost);
		var extractUpdate = extractItem.Count > 1 ? CopyInventoryItem(extractItem, count: extractItem.Count - 1) : null;
		int? deletedExtractObjectId = extractItem.Count <= 1 ? extractItem.ObjectId : null;
		return ItemRemodelPlan.Success(targetUpdate, kinahUpdate, extractUpdate, deletedExtractObjectId, keepTemplate);
	}

	private static ItemRemodelPlan CreatePatternReshaperPlan(
		InventoryItem keepItem,
		ItemTemplateSummary keepTemplate,
		InventoryItem extractItem,
		InventoryItem kinahItem)
	{
		if (keepItem.ItemSkin == 0)
			return ItemRemodelPlan.Failed(ItemRemodelFailure.NotSkinnedItem, keepTemplate, keepTemplate);

		var color = keepTemplate.IsItemDyePermitted ? keepItem.Color : null;
		var targetUpdate = CopyInventoryItem(
			keepItem,
			itemSkin: 0,
			color: color,
			setColor: true);
		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - RemodelCost);
		var extractUpdate = extractItem.Count > 1 ? CopyInventoryItem(extractItem, count: extractItem.Count - 1) : null;
		int? deletedExtractObjectId = extractItem.Count <= 1 ? extractItem.ObjectId : null;
		return ItemRemodelPlan.Success(targetUpdate, kinahUpdate, extractUpdate, deletedExtractObjectId, keepTemplate);
	}

	private static bool HasOppositeGenderRequirement(ItemTemplateSummary keepTemplate, ItemTemplateSummary extractTemplate)
	{
		return !string.IsNullOrWhiteSpace(keepTemplate.GenderPermitted)
			&& !string.IsNullOrWhiteSpace(extractTemplate.GenderPermitted)
			&& !string.Equals(keepTemplate.GenderPermitted, extractTemplate.GenderPermitted, StringComparison.Ordinal);
	}

	private static bool IsCompatible(ItemTemplateSummary keepTemplate, ItemTemplateSummary extractSkinTemplate)
	{
		if (string.Equals(keepTemplate.ItemGroup, extractSkinTemplate.ItemGroup, StringComparison.Ordinal))
			return true;

		if (IsClothesGroup(keepTemplate.ItemGroup))
			return false;

		return IsClothesGroup(extractSkinTemplate.ItemGroup)
			|| IsAllArmorGroup(extractSkinTemplate.ItemGroup)
			&& keepTemplate.ValidEquipmentSlots == extractSkinTemplate.ValidEquipmentSlots;
	}

	private static bool IsClothesGroup(string itemGroup)
	{
		return itemGroup is "CL_TORSO" or "CL_GLOVE" or "CL_SHOULDER" or "CL_PANTS" or "CL_SHOES" or "CL_HEADS" or "CL_MULTISLOT";
	}

	private static bool IsAllArmorGroup(string itemGroup)
	{
		return itemGroup is "TORSO" or "GLOVE" or "SHOULDER" or "PANTS" or "SHOES";
	}

	private static InventoryItem CopyInventoryItem(
		InventoryItem item,
		long? count = null,
		int? itemSkin = null,
		int? color = null,
		bool setColor = false)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count ?? item.Count,
			Color = setColor ? color : item.Color,
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
			ItemSkin = itemSkin ?? item.ItemSkin,
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
}

public sealed record ItemRemodelPlan(
	bool Succeeded,
	ItemRemodelFailure Failure,
	ItemTemplateSummary? FailureItem,
	ItemTemplateSummary? FailureOtherItem,
	InventoryItem? TargetItemUpdate,
	InventoryItem? KinahItemUpdate,
	InventoryItem? ExtractItemUpdate,
	int? DeletedExtractItemObjectId,
	ItemTemplateSummary? SuccessItem)
{
	public static ItemRemodelPlan Failed(ItemRemodelFailure failure, ItemTemplateSummary failureItem, ItemTemplateSummary failureOtherItem)
	{
		return new ItemRemodelPlan(false, failure, failureItem, failureOtherItem, null, null, null, null, null);
	}

	public static ItemRemodelPlan Success(
		InventoryItem targetItemUpdate,
		InventoryItem kinahItemUpdate,
		InventoryItem? extractItemUpdate,
		int? deletedExtractItemObjectId,
		ItemTemplateSummary successItem)
	{
		return new ItemRemodelPlan(
			true,
			ItemRemodelFailure.None,
			null,
			null,
			targetItemUpdate,
			kinahItemUpdate,
			extractItemUpdate,
			deletedExtractItemObjectId,
			successItem);
	}
}

public enum ItemRemodelFailure
{
	None,
	LevelLimit,
	OppositeRequirement,
	NotEnoughKinah,
	NotSkinnedItem,
	NotCompatible,
	NotSkinChangeable,
	CannotRemoveSkinItem,
}
