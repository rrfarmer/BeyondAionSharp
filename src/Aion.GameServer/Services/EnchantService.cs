using System.Globalization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class EnchantService
{
	private const int CubeStorageId = 0;
	private const int ExceedEnchantMaterialItemId = 166500002;
	private const int ExceedEnchantAlternateMaterialItemId = 166500005;

	public static AmplificationPlan CreateAmplificationPlan(
		Player player,
		int targetItemObjectId,
		int materialObjectId,
		int toolObjectId,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: services/EnchantService.amplifyItem.
		var inventoryItems = player.InventoryItems.ToList();
		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == targetItemObjectId);
		var materialItem = FindCubeItem(inventoryItems, materialObjectId);
		var toolItem = FindCubeItem(inventoryItems, toolObjectId);
		if (targetItem == null
			|| materialItem == null
			|| toolItem == null
			|| materialItem.ObjectId == toolItem.ObjectId
			|| materialItem.ObjectId == targetItem.ObjectId
			|| toolItem.ObjectId == targetItem.ObjectId)
		{
			return AmplificationPlan.Failed(AmplificationFailure.NoTargetItem);
		}

		var targetTemplate = itemTemplates.GetItemTemplate(targetItem.ItemId);
		if (targetTemplate == null)
			return AmplificationPlan.Failed(AmplificationFailure.NoTargetItem);

		var targetName = GetItemName(targetItem, itemTemplates);
		if (targetItem.IsAmplified)
			return AmplificationPlan.Failed(AmplificationFailure.AlreadyAmplified, targetName);

		if (!targetTemplate.CanExceedEnchant)
			return AmplificationPlan.Failed(AmplificationFailure.CannotAmplify, targetName);

		var maxEnchantLevel = targetTemplate.MaxEnchantLevel + targetItem.EnchantBonus;
		if (targetItem.Enchant < maxEnchantLevel)
			return AmplificationPlan.Failed(AmplificationFailure.NeedsMaxEnchant, targetName);

		if (targetItem.ItemId != materialItem.ItemId
			&& materialItem.ItemId != ExceedEnchantMaterialItemId
			&& materialItem.ItemId != ExceedEnchantAlternateMaterialItemId)
		{
			return AmplificationPlan.Failed(AmplificationFailure.NoTargetItem);
		}

		var targetUpdate = CopyInventoryItem(targetItem, isAmplified: true);
		var materialMutation = DecreaseItemCount(materialItem);
		var toolMutation = DecreaseItemCount(toolItem);

		ReplaceInventoryItem(inventoryItems, targetUpdate);
		ApplySourceMutation(inventoryItems, materialMutation);
		ApplySourceMutation(inventoryItems, toolMutation);

		return new AmplificationPlan(
			AmplificationFailure.None,
			targetName,
			inventoryItems,
			targetUpdate,
			materialMutation.UpdatedItem,
			materialMutation.DeletedObjectId,
			toolMutation.UpdatedItem,
			toolMutation.DeletedObjectId);
	}

	private static InventoryItem? FindCubeItem(IReadOnlyList<InventoryItem> inventoryItems, int objectId)
	{
		return inventoryItems.FirstOrDefault(item =>
			item.ObjectId == objectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
	}

	private static string GetItemName(InventoryItem item, ItemTemplateTable itemTemplates)
	{
		var template = itemTemplates.GetItemTemplate(item.ItemId);
		return template?.GetClientName()
			?? template?.Name
			?? item.ItemId.ToString(CultureInfo.InvariantCulture);
	}

	private static void ApplySourceMutation(List<InventoryItem> inventoryItems, ItemCountMutation mutation)
	{
		if (mutation.UpdatedItem != null)
			ReplaceInventoryItem(inventoryItems, mutation.UpdatedItem);
		else if (mutation.DeletedObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == mutation.DeletedObjectId);
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem update)
	{
		var index = items.FindIndex(item => item.ObjectId == update.ObjectId);
		if (index >= 0)
			items[index] = update;
		else
			items.Add(update);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long? count = null, bool? isAmplified = null)
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
			IsAmplified = isAmplified ?? item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}

	private static ItemCountMutation DecreaseItemCount(InventoryItem item)
	{
		return item.Count > 1
			? new ItemCountMutation(CopyInventoryItem(item, count: item.Count - 1), null)
			: new ItemCountMutation(null, item.ObjectId);
	}

	private sealed record ItemCountMutation(InventoryItem? UpdatedItem, int? DeletedObjectId);
}

public enum AmplificationFailure
{
	None,
	NoTargetItem,
	AlreadyAmplified,
	CannotAmplify,
	NeedsMaxEnchant,
}

public sealed record AmplificationPlan(
	AmplificationFailure Failure,
	string ItemName,
	IReadOnlyList<InventoryItem> InventoryItems,
	InventoryItem? TargetItemUpdate,
	InventoryItem? MaterialItemUpdate,
	int? DeletedMaterialItemObjectId,
	InventoryItem? ToolItemUpdate,
	int? DeletedToolItemObjectId)
{
	public bool Succeeded => Failure == AmplificationFailure.None;

	public static AmplificationPlan Failed(AmplificationFailure failure, string itemName = "")
	{
		return new AmplificationPlan(
			failure,
			itemName,
			Array.Empty<InventoryItem>(),
			TargetItemUpdate: null,
			MaterialItemUpdate: null,
			DeletedMaterialItemObjectId: null,
			ToolItemUpdate: null,
			DeletedToolItemObjectId: null);
	}
}
