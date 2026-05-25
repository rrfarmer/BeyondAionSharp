using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationMutationSnapshotService
{
	public static ItemPurificationMutationSnapshotPlan CreatePreview(
		IReadOnlyList<InventoryItem> currentInventoryItems,
		ItemPurificationApplicationPlan? applicationPlan,
		int npcExpands,
		int questExpands,
		int itemExpands)
	{
		// Java parity: services/item/ItemPurificationService.decreaseMaterials mutates Storage
		// before upgradeItem adds the target item. This preview produces the same post-storage
		// packet snapshots without mutating the live C# inventory or persistence state.
		if (applicationPlan == null)
			return ItemPurificationMutationSnapshotPlan.Failed(ItemPurificationMutationSnapshotStatus.MissingApplicationPlan);
		if (applicationPlan.Operations.Count == 0)
			return ItemPurificationMutationSnapshotPlan.Failed(ItemPurificationMutationSnapshotStatus.ApplicationPlanUnavailable);
		if (!applicationPlan.Succeeded)
			return ItemPurificationMutationSnapshotPlan.Failed(ItemPurificationMutationSnapshotStatus.ApplicationPlanNotReady);
		if (currentInventoryItems.Count == 0)
			return ItemPurificationMutationSnapshotPlan.Failed(ItemPurificationMutationSnapshotStatus.MissingCurrentInventoryItems);

		var workingItems = currentInventoryItems.Select(item => CopyInventoryItem(item, item.Count)).ToList();
		var cubeSnapshotsByPacketOperationIndex = new Dictionary<int, ItemPurificationCubeSnapshot>();
		var missingObjectIds = new HashSet<int>();
		var mismatchedObjectIds = new HashSet<int>();
		var packetOperationIndex = 1;
		var missingTargetItem = false;

		foreach (var operation in applicationPlan.Operations)
		{
			switch (operation.Type)
			{
				case ItemPurificationApplicationOperationType.UpdateMaterialItemCount:
				case ItemPurificationApplicationOperationType.UpdateBaseItemCount:
					ApplyUpdate(operation, workingItems, missingObjectIds, mismatchedObjectIds);
					packetOperationIndex++;
					break;
				case ItemPurificationApplicationOperationType.DeleteMaterialItem:
				case ItemPurificationApplicationOperationType.DeleteBaseItem:
					ApplyDelete(operation, workingItems, missingObjectIds, mismatchedObjectIds);
					packetOperationIndex++;
					cubeSnapshotsByPacketOperationIndex[packetOperationIndex] =
						ItemPurificationPacketInputSnapshotService.CreateCubeSnapshot(
							workingItems,
							npcExpands,
							questExpands,
							itemExpands);
					packetOperationIndex++;
					break;
				case ItemPurificationApplicationOperationType.SpendAbyssPoints:
				case ItemPurificationApplicationOperationType.PreserveKinahNoOp:
					packetOperationIndex++;
					break;
				case ItemPurificationApplicationOperationType.AddTargetItem:
					if (!ApplyAddTarget(
						operation,
						applicationPlan.TargetItem,
						workingItems,
						mismatchedObjectIds))
						missingTargetItem = true;
					packetOperationIndex++;
					cubeSnapshotsByPacketOperationIndex[packetOperationIndex] =
						ItemPurificationPacketInputSnapshotService.CreateCubeSnapshot(
							workingItems,
							npcExpands,
							questExpands,
							itemExpands);
					packetOperationIndex++;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(applicationPlan), operation.Type, "Unsupported item purification operation.");
			}
		}

		var status = missingTargetItem
			? ItemPurificationMutationSnapshotStatus.MissingTargetItem
			: mismatchedObjectIds.Count > 0
				? ItemPurificationMutationSnapshotStatus.MismatchedCurrentInventoryItems
				: missingObjectIds.Count > 0
					? ItemPurificationMutationSnapshotStatus.MissingCurrentInventoryItems
					: ItemPurificationMutationSnapshotStatus.Ready;

		return new ItemPurificationMutationSnapshotPlan(
			status,
			workingItems,
			cubeSnapshotsByPacketOperationIndex,
			missingObjectIds.Order().ToArray(),
			mismatchedObjectIds.Order().ToArray());
	}

	private static void ApplyUpdate(
		ItemPurificationApplicationOperation operation,
		List<InventoryItem> workingItems,
		HashSet<int> missingObjectIds,
		HashSet<int> mismatchedObjectIds)
	{
		var index = workingItems.FindIndex(item => item.ObjectId == operation.ObjectId);
		if (index < 0)
		{
			missingObjectIds.Add(operation.ObjectId);
			return;
		}

		var item = workingItems[index];
		if (item.ItemId != operation.ItemId || item.Count < operation.Count)
		{
			mismatchedObjectIds.Add(operation.ObjectId);
			return;
		}

		workingItems[index] = CopyInventoryItem(item, operation.NewCount);
	}

	private static void ApplyDelete(
		ItemPurificationApplicationOperation operation,
		List<InventoryItem> workingItems,
		HashSet<int> missingObjectIds,
		HashSet<int> mismatchedObjectIds)
	{
		var index = workingItems.FindIndex(item => item.ObjectId == operation.ObjectId);
		if (index < 0)
		{
			missingObjectIds.Add(operation.ObjectId);
			return;
		}

		var item = workingItems[index];
		if (item.ItemId != operation.ItemId || item.Count < operation.Count)
		{
			mismatchedObjectIds.Add(operation.ObjectId);
			return;
		}

		workingItems.RemoveAt(index);
	}

	private static bool ApplyAddTarget(
		ItemPurificationApplicationOperation operation,
		InventoryItem? targetItem,
		List<InventoryItem> workingItems,
		HashSet<int> mismatchedObjectIds)
	{
		if (targetItem == null)
			return false;
		if (targetItem.ObjectId != operation.ObjectId
			|| targetItem.ItemId != operation.ItemId
			|| targetItem.Count != operation.Count)
		{
			mismatchedObjectIds.Add(operation.ObjectId);
			return true;
		}

		workingItems.RemoveAll(item => item.ObjectId == targetItem.ObjectId);
		workingItems.Add(CopyInventoryItem(targetItem, targetItem.Count));
		return true;
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
}

public sealed record ItemPurificationMutationSnapshotPlan(
	ItemPurificationMutationSnapshotStatus Status,
	IReadOnlyList<InventoryItem> PostMutationInventoryItems,
	IReadOnlyDictionary<int, ItemPurificationCubeSnapshot> CubeSnapshotsByPacketOperationIndex,
	IReadOnlyList<int> MissingObjectIds,
	IReadOnlyList<int> MismatchedObjectIds)
{
	public bool Succeeded => Status == ItemPurificationMutationSnapshotStatus.Ready;

	public static ItemPurificationMutationSnapshotPlan Failed(ItemPurificationMutationSnapshotStatus status)
	{
		return new ItemPurificationMutationSnapshotPlan(
			status,
			Array.Empty<InventoryItem>(),
			new Dictionary<int, ItemPurificationCubeSnapshot>(),
			Array.Empty<int>(),
			Array.Empty<int>());
	}
}

public enum ItemPurificationMutationSnapshotStatus
{
	Ready,
	MissingApplicationPlan,
	ApplicationPlanUnavailable,
	ApplicationPlanNotReady,
	MissingCurrentInventoryItems,
	MismatchedCurrentInventoryItems,
	MissingTargetItem,
}
