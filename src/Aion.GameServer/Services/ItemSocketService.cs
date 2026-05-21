using System.Globalization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemSocketService
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const int ManastoneRemovalPrice = 650;

	public static ManastoneRemovalPlan CreateRemoveManastonePlan(
		Player player,
		int itemObjectId,
		int slotNumber,
		bool isFusionSocket,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: services/item/ItemSocketService.removeManastone.
		var inventoryItems = player.InventoryItems.ToList();
		var item = inventoryItems.FirstOrDefault(candidate =>
			candidate.ObjectId == itemObjectId
			&& candidate.Location == CubeStorageId
			&& !candidate.IsEquipped);
		if (item == null)
			return ManastoneRemovalPlan.Failed(ManastoneRemovalFailure.NoTargetItem);

		var itemName = GetItemName(item, itemTemplates);
		var stones = isFusionSocket ? item.FusionStones : item.ManaStones;
		if (stones.Count == 0)
			return ManastoneRemovalPlan.Failed(ManastoneRemovalFailure.NoOptionToRemove, itemName);

		var stoneToRemove = stones.FirstOrDefault(stone => stone.Slot == slotNumber);
		if (stoneToRemove == null)
			return ManastoneRemovalPlan.Failed(ManastoneRemovalFailure.InvalidSlot, itemName);

		var kinahItem = inventoryItems.FirstOrDefault(candidate =>
			candidate.ItemId == KinahItemId
			&& candidate.Location == CubeStorageId
			&& !candidate.IsEquipped);
		if (kinahItem == null || kinahItem.Count < ManastoneRemovalPrice)
			return ManastoneRemovalPlan.Failed(ManastoneRemovalFailure.NotEnoughKinah, itemName);

		var itemUpdate = CopyInventoryItem(item);
		if (isFusionSocket)
			itemUpdate.FusionStones = item.FusionStones.Where(stone => stone.Slot != slotNumber).OrderBy(stone => stone.Slot).ToArray();
		else
			itemUpdate.ManaStones = item.ManaStones.Where(stone => stone.Slot != slotNumber).OrderBy(stone => stone.Slot).ToArray();

		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - ManastoneRemovalPrice);
		ReplaceInventoryItem(inventoryItems, itemUpdate);
		ReplaceInventoryItem(inventoryItems, kinahUpdate);

		return new ManastoneRemovalPlan(
			ManastoneRemovalFailure.None,
			itemName,
			inventoryItems,
			itemUpdate,
			kinahUpdate,
			slotNumber,
			isFusionSocket ? 2 : 0);
	}

	public static GodstoneSocketPlan CreateSocketGodstonePlan(
		Player player,
		int targetItemObjectId,
		int godstoneObjectId,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: services/item/ItemSocketService.socketGodstone.
		var inventoryItems = player.InventoryItems.ToList();
		var targetItem = inventoryItems.FirstOrDefault(candidate =>
			candidate.ObjectId == targetItemObjectId
			&& candidate.Location == CubeStorageId
			&& !candidate.IsEquipped);
		if (targetItem == null)
		{
			var equippedTarget = inventoryItems.Any(candidate =>
				candidate.ObjectId == targetItemObjectId
				&& candidate.Location == CubeStorageId
				&& candidate.IsEquipped);
			return GodstoneSocketPlan.Failed(
				equippedTarget ? GodstoneSocketFailure.TargetItemEquipped : GodstoneSocketFailure.NoTargetItem);
		}

		var targetTemplate = itemTemplates.GetItemTemplate(targetItem.ItemId);
		var targetName = GetItemName(targetItem, itemTemplates);
		if (targetTemplate?.CanSocketGodstone != true)
			return GodstoneSocketPlan.Failed(GodstoneSocketFailure.TargetNotProcGivable, targetName);

		var godstoneItem = inventoryItems.FirstOrDefault(candidate =>
			candidate.ObjectId == godstoneObjectId
			&& candidate.Location == CubeStorageId
			&& !candidate.IsEquipped);
		if (godstoneItem == null)
			return GodstoneSocketPlan.Failed(GodstoneSocketFailure.NoGodstoneItem, targetName);

		var godstoneTemplate = itemTemplates.GetItemTemplate(godstoneItem.ItemId);
		if (godstoneTemplate?.GodstoneInfo == null)
			return GodstoneSocketPlan.Failed(GodstoneSocketFailure.NoGodstoneItem, targetName);

		var sourceMutation = DecreaseItemCount(godstoneItem);
		var targetUpdate = CopyInventoryItem(targetItem);
		targetUpdate.Godstone = new PlayerGodstone(godstoneItem.ItemId, ProcCount: 0);
		ReplaceInventoryItem(inventoryItems, targetUpdate);
		if (sourceMutation.UpdatedItem != null)
			ReplaceInventoryItem(inventoryItems, sourceMutation.UpdatedItem);
		else if (sourceMutation.DeletedObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == sourceMutation.DeletedObjectId);

		return new GodstoneSocketPlan(
			GodstoneSocketFailure.None,
			targetName,
			inventoryItems,
			targetUpdate,
			sourceMutation.UpdatedItem,
			sourceMutation.DeletedObjectId);
	}

	private static string GetItemName(InventoryItem item, ItemTemplateTable itemTemplates)
	{
		var template = itemTemplates.GetItemTemplate(item.ItemId);
		return template?.GetClientName()
			?? template?.Name
			?? item.ItemId.ToString(CultureInfo.InvariantCulture);
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem update)
	{
		var index = items.FindIndex(item => item.ObjectId == update.ObjectId);
		if (index >= 0)
			items[index] = update;
		else
			items.Add(update);
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

public enum ManastoneRemovalFailure
{
	None,
	NoTargetItem,
	NoOptionToRemove,
	InvalidSlot,
	NotEnoughKinah,
}

public sealed record ManastoneRemovalPlan(
	ManastoneRemovalFailure Failure,
	string ItemName,
	IReadOnlyList<InventoryItem> InventoryItems,
	InventoryItem? ItemUpdate,
	InventoryItem? KinahItemUpdate,
	int RemovedSlot,
	int RemovedCategory)
{
	public bool Succeeded => Failure == ManastoneRemovalFailure.None;

	public static ManastoneRemovalPlan Failed(ManastoneRemovalFailure failure, string itemName = "")
	{
		return new ManastoneRemovalPlan(
			failure,
			itemName,
			Array.Empty<InventoryItem>(),
			null,
			null,
			RemovedSlot: 0,
			RemovedCategory: 0);
	}
}

public enum GodstoneSocketFailure
{
	None,
	NoTargetItem,
	TargetItemEquipped,
	TargetNotProcGivable,
	NoGodstoneItem,
}

public sealed record GodstoneSocketPlan(
	GodstoneSocketFailure Failure,
	string ItemName,
	IReadOnlyList<InventoryItem> InventoryItems,
	InventoryItem? TargetItemUpdate,
	InventoryItem? SourceItemUpdate,
	int? DeletedSourceItemObjectId)
{
	public bool Succeeded => Failure == GodstoneSocketFailure.None;

	public static GodstoneSocketPlan Failed(GodstoneSocketFailure failure, string itemName = "")
	{
		return new GodstoneSocketPlan(
			failure,
			itemName,
			Array.Empty<InventoryItem>(),
			TargetItemUpdate: null,
			SourceItemUpdate: null,
			DeletedSourceItemObjectId: null);
	}
}
