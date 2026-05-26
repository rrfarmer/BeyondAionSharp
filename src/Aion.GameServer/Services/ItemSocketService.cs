using System.Globalization;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemSocketService
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const int ManastoneRemovalPrice = 650;
	private const int MaxBasicManastones = 6;
	private const string SpecialManastoneGroup = "SPECIAL_MANASTONE";

	public static ManastoneRemovalPlan CreateRemoveManastonePlan(
		Player player,
		int itemObjectId,
		int slotNumber,
		bool isFusionSocket,
		ItemTemplateTable itemTemplates,
		GameServerPriceOptions? priceOptions = null,
		PriceInfluenceRates? influenceRates = null)
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
		var removalPrice = PricesService.GetPriceForService(
			ManastoneRemovalPrice,
			player.Race,
			priceOptions ?? new GameServerPriceOptions(),
			influenceRates ?? new PriceInfluenceRates());
		if (kinahItem == null || kinahItem.Count < removalPrice)
			return ManastoneRemovalPlan.Failed(ManastoneRemovalFailure.NotEnoughKinah, itemName);

		var itemUpdate = CopyInventoryItem(item);
		if (isFusionSocket)
			itemUpdate.FusionStones = item.FusionStones.Where(stone => stone.Slot != slotNumber).OrderBy(stone => stone.Slot).ToArray();
		else
			itemUpdate.ManaStones = item.ManaStones.Where(stone => stone.Slot != slotNumber).OrderBy(stone => stone.Slot).ToArray();

		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - removalPrice);
		ReplaceInventoryItem(inventoryItems, itemUpdate);
		ReplaceInventoryItem(inventoryItems, kinahUpdate);

		return new ManastoneRemovalPlan(
			ManastoneRemovalFailure.None,
			itemName,
			inventoryItems,
			itemUpdate,
			kinahUpdate,
			slotNumber,
			isFusionSocket ? 2 : 0,
			removalPrice);
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

	public static ManastoneAddPlan CreateAddManastonePlan(
		InventoryItem item,
		int manaStoneItemId,
		bool useFusionSlots,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: services/item/ItemSocketService.addManaStone(Item, int, boolean).
		var targetTemplate = itemTemplates.GetItemTemplate(item.ItemId);
		if (targetTemplate == null)
			return ManastoneAddPlan.Failed(ManastoneAddFailure.NoTargetTemplate);

		var maxSlots = GetSocketCount(item, useFusionSlots, itemTemplates, targetTemplate);
		var manaStones = useFusionSlots ? item.FusionStones : item.ManaStones;
		if (manaStones.Count > maxSlots)
			return ManastoneAddPlan.Failed(ManastoneAddFailure.NoAvailableSlot);

		var manaStoneTemplate = itemTemplates.GetItemTemplate(manaStoneItemId);
		if (manaStoneTemplate == null)
			return ManastoneAddPlan.Failed(ManastoneAddFailure.NoManastoneTemplate);

		var specialSlotCount = GetSpecialSlotCount(item, useFusionSlots, itemTemplates, targetTemplate, maxSlots);
		if (IsSpecialManastone(manaStoneTemplate) && specialSlotCount == 0)
			return ManastoneAddPlan.Failed(ManastoneAddFailure.NoAvailableSlot);

		var specialSlotsOccupied = 0;
		var normalSlotsOccupied = 0;
		var occupiedSlots = new HashSet<int>();
		foreach (var stone in manaStones)
		{
			var stoneTemplate = itemTemplates.GetItemTemplate(stone.ItemId);
			if (stoneTemplate != null && IsSpecialManastone(stoneTemplate))
				specialSlotsOccupied++;
			else
				normalSlotsOccupied++;
			occupiedSlots.Add(stone.Slot);
		}

		if (IsSpecialManastone(manaStoneTemplate))
		{
			if (specialSlotsOccupied >= specialSlotCount)
				return ManastoneAddPlan.Failed(ManastoneAddFailure.NoAvailableSlot);

			return InsertManastoneIntoNextSlot(
				item,
				targetTemplate,
				manaStoneItemId,
				useFusionSlots,
				manaStones,
				occupiedSlots,
				startSlot: 0,
				endSlot: specialSlotCount);
		}

		var normalSlotCount = maxSlots - specialSlotCount;
		if (normalSlotsOccupied >= normalSlotCount)
			return ManastoneAddPlan.Failed(ManastoneAddFailure.NoAvailableSlot);

		return InsertManastoneIntoNextSlot(
			item,
			targetTemplate,
			manaStoneItemId,
			useFusionSlots,
			manaStones,
			occupiedSlots,
			startSlot: specialSlotCount,
			endSlot: maxSlots);
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

	private static InventoryItem CopyInventoryItem(InventoryItem item, long? count = null, int? tuneCount = null)
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
			TuneCount = tuneCount ?? item.TuneCount,
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

	private static int GetSocketCount(
		InventoryItem item,
		bool useFusionSlots,
		ItemTemplateTable itemTemplates,
		ItemTemplateSummary targetTemplate)
	{
		// Java parity: model/gameobjects/Item.getSockets.
		if (!targetTemplate.IsWeapon && !targetTemplate.IsArmor)
			return 0;

		if (useFusionSlots)
		{
			var fusionTemplate = item.FusionedItem == 0 ? null : itemTemplates.GetItemTemplate(item.FusionedItem);
			if (fusionTemplate == null)
				return 0;

			return Math.Min(fusionTemplate.ManastoneSlots + item.OptionalFusionSocket, MaxBasicManastones);
		}

		return Math.Min(targetTemplate.ManastoneSlots + item.OptionalSocket, MaxBasicManastones);
	}

	private static int GetSpecialSlotCount(
		InventoryItem item,
		bool useFusionSlots,
		ItemTemplateTable itemTemplates,
		ItemTemplateSummary targetTemplate,
		int maxSlots)
	{
		var specialSlotCount = targetTemplate.SpecialManastoneSlots;
		if (useFusionSlots)
		{
			var fusionTemplate = item.FusionedItem == 0 ? null : itemTemplates.GetItemTemplate(item.FusionedItem);
			specialSlotCount = fusionTemplate?.SpecialManastoneSlots ?? 0;
		}

		return Math.Min(specialSlotCount, maxSlots);
	}

	private static bool IsSpecialManastone(ItemTemplateSummary template)
	{
		return string.Equals(template.ItemGroup, SpecialManastoneGroup, StringComparison.Ordinal);
	}

	private static ManastoneAddPlan InsertManastoneIntoNextSlot(
		InventoryItem item,
		ItemTemplateSummary targetTemplate,
		int manaStoneItemId,
		bool useFusionSlots,
		IReadOnlyList<ItemStoneSocket> manaStones,
		IReadOnlySet<int> occupiedSlots,
		int startSlot,
		int endSlot)
	{
		for (var slot = startSlot; slot < endSlot; slot++)
		{
			if (occupiedSlots.Contains(slot))
				continue;

			var itemUpdate = RemoveRemainingTuningCountIfPossible(item, targetTemplate);
			var addedStone = new ItemStoneSocket(manaStoneItemId, slot);
			var updatedStones = manaStones
				.Append(addedStone)
				.OrderBy(stone => stone.Slot)
				.ToArray();
			if (useFusionSlots)
				itemUpdate.FusionStones = updatedStones;
			else
				itemUpdate.ManaStones = updatedStones;

			return new ManastoneAddPlan(ManastoneAddFailure.None, itemUpdate, addedStone, useFusionSlots ? 2 : 0);
		}

		return ManastoneAddPlan.Failed(ManastoneAddFailure.NoAvailableSlot);
	}

	private static InventoryItem RemoveRemainingTuningCountIfPossible(InventoryItem item, ItemTemplateSummary targetTemplate)
	{
		// Java parity: model/gameobjects/Item.removeRemainingTuningCountIfPossible during addManaStone.
		if (item.IsIdentified && targetTemplate.MaxTuneCount > 0 && item.TuneCount != targetTemplate.MaxTuneCount)
			return CopyInventoryItem(item, tuneCount: targetTemplate.MaxTuneCount);

		return CopyInventoryItem(item);
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
	int RemovedCategory,
	long RemovalPrice)
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
			RemovedCategory: 0,
			RemovalPrice: 0);
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

public enum ManastoneAddFailure
{
	None,
	NoTargetTemplate,
	NoManastoneTemplate,
	NoAvailableSlot,
}

public sealed record ManastoneAddPlan(
	ManastoneAddFailure Failure,
	InventoryItem? ItemUpdate,
	ItemStoneSocket? AddedStone,
	int AddedCategory)
{
	public bool Succeeded => Failure == ManastoneAddFailure.None;

	public static ManastoneAddPlan Failed(ManastoneAddFailure failure)
	{
		return new ManastoneAddPlan(
			failure,
			ItemUpdate: null,
			AddedStone: null,
			AddedCategory: 0);
	}
}
