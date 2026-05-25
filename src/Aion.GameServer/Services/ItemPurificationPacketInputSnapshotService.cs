using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationPacketInputSnapshotService
{
	private const int KinahItemId = 182400001;
	private const int CubeStorageId = 0;

	public static ItemPurificationPacketInputSnapshotResult CreateInputs(
		ItemPurificationApplicationPlan? applicationPlan,
		IReadOnlyList<InventoryItem> postMutationInventoryItems,
		ItemTemplateTable itemTemplates,
		IReadOnlyDictionary<int, ItemPurificationCubeSnapshot> cubeSnapshotsByPacketOperationIndex)
	{
		// Java parity: this mirrors the already-mutated item objects that ItemPacketService consumes
		// after Storage.decreaseItemCount/delete/add have run, without performing those mutations here.
		if (applicationPlan == null)
			return ItemPurificationPacketInputSnapshotResult.Failed(ItemPurificationPacketInputSnapshotStatus.MissingApplicationPlan);
		if (applicationPlan.Operations.Count == 0)
			return ItemPurificationPacketInputSnapshotResult.Failed(ItemPurificationPacketInputSnapshotStatus.ApplicationPlanUnavailable);
		if (!applicationPlan.Succeeded)
			return ItemPurificationPacketInputSnapshotResult.Failed(ItemPurificationPacketInputSnapshotStatus.ApplicationPlanNotReady);

		var inventoryItemsByObjectId = postMutationInventoryItems
			.GroupBy(item => item.ObjectId)
			.ToDictionary(group => group.Key, group => group.First());
		var inventoryInputs = new Dictionary<int, ItemPurificationInventoryPacketInput>();
		var cubeInputs = new Dictionary<int, ItemPurificationCubePacketInput>();
		var missingTemplateIds = new HashSet<int>();
		var missingInventoryObjectIds = new HashSet<int>();
		var mismatchedInventoryObjectIds = new HashSet<int>();
		var missingCubePacketOperationIndexes = new HashSet<int>();
		var invalidCubePacketOperationIndexes = new HashSet<int>();
		var packetOperationIndex = 1;

		foreach (var operation in applicationPlan.Operations)
		{
			switch (operation.Type)
			{
				case ItemPurificationApplicationOperationType.UpdateMaterialItemCount:
				case ItemPurificationApplicationOperationType.UpdateBaseItemCount:
					AddInventoryInput(
						operation,
						inventoryItemsByObjectId,
						itemTemplates,
						inventoryInputs,
						missingInventoryObjectIds,
						mismatchedInventoryObjectIds,
						missingTemplateIds);
					packetOperationIndex++;
					break;
				case ItemPurificationApplicationOperationType.DeleteMaterialItem:
				case ItemPurificationApplicationOperationType.DeleteBaseItem:
					packetOperationIndex++;
					AddCubeInput(
						operation,
						packetOperationIndex,
						cubeSnapshotsByPacketOperationIndex,
						cubeInputs,
						missingCubePacketOperationIndexes,
						invalidCubePacketOperationIndexes);
					packetOperationIndex++;
					break;
				case ItemPurificationApplicationOperationType.SpendAbyssPoints:
				case ItemPurificationApplicationOperationType.PreserveKinahNoOp:
					packetOperationIndex++;
					break;
				case ItemPurificationApplicationOperationType.AddTargetItem:
					AddInventoryInput(
						operation,
						inventoryItemsByObjectId,
						itemTemplates,
						inventoryInputs,
						missingInventoryObjectIds,
						mismatchedInventoryObjectIds,
						missingTemplateIds);
					packetOperationIndex++;
					AddCubeInput(
						operation,
						packetOperationIndex,
						cubeSnapshotsByPacketOperationIndex,
						cubeInputs,
						missingCubePacketOperationIndexes,
						invalidCubePacketOperationIndexes);
					packetOperationIndex++;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(applicationPlan), operation.Type, "Unsupported item purification operation.");
			}
		}

		var status = missingTemplateIds.Count > 0
			? ItemPurificationPacketInputSnapshotStatus.MissingTemplates
			: mismatchedInventoryObjectIds.Count > 0
				? ItemPurificationPacketInputSnapshotStatus.MismatchedInventorySnapshots
				: missingInventoryObjectIds.Count > 0
				? ItemPurificationPacketInputSnapshotStatus.MissingInventorySnapshots
				: invalidCubePacketOperationIndexes.Count > 0
					? ItemPurificationPacketInputSnapshotStatus.InvalidCubeSnapshots
					: missingCubePacketOperationIndexes.Count > 0
						? ItemPurificationPacketInputSnapshotStatus.MissingCubeSnapshots
						: ItemPurificationPacketInputSnapshotStatus.Ready;
		return new ItemPurificationPacketInputSnapshotResult(
			status,
			inventoryInputs,
			cubeInputs,
			missingTemplateIds.Order().ToArray(),
			missingInventoryObjectIds.Order().ToArray(),
			mismatchedInventoryObjectIds.Order().ToArray(),
			missingCubePacketOperationIndexes.Order().ToArray(),
			invalidCubePacketOperationIndexes.Order().ToArray());
	}

	private static void AddInventoryInput(
		ItemPurificationApplicationOperation operation,
		IReadOnlyDictionary<int, InventoryItem> inventoryItemsByObjectId,
		ItemTemplateTable itemTemplates,
		Dictionary<int, ItemPurificationInventoryPacketInput> inventoryInputs,
		HashSet<int> missingInventoryObjectIds,
		HashSet<int> mismatchedInventoryObjectIds,
		HashSet<int> missingTemplateIds)
	{
		if (operation.ObjectId <= 0 || !inventoryItemsByObjectId.TryGetValue(operation.ObjectId, out var item))
		{
			missingInventoryObjectIds.Add(operation.ObjectId);
			return;
		}

		var template = itemTemplates.GetItemTemplate(operation.ItemId);
		if (template == null)
		{
			missingTemplateIds.Add(operation.ItemId);
			return;
		}
		var expectedCount = operation.Type == ItemPurificationApplicationOperationType.AddTargetItem
			? operation.Count
			: operation.NewCount;
		if (item.ObjectId != operation.ObjectId || item.ItemId != operation.ItemId || item.Count != expectedCount)
		{
			mismatchedInventoryObjectIds.Add(operation.ObjectId);
			return;
		}

		inventoryInputs[operation.ObjectId] = new ItemPurificationInventoryPacketInput(item, template);
	}

	private static void AddCubeInput(
		ItemPurificationApplicationOperation operation,
		int packetOperationIndex,
		IReadOnlyDictionary<int, ItemPurificationCubeSnapshot> cubeSnapshotsByPacketOperationIndex,
		Dictionary<int, ItemPurificationCubePacketInput> cubeInputs,
		HashSet<int> missingCubePacketOperationIndexes,
		HashSet<int> invalidCubePacketOperationIndexes)
	{
		if (!cubeSnapshotsByPacketOperationIndex.TryGetValue(packetOperationIndex, out var cubeSnapshot))
		{
			missingCubePacketOperationIndexes.Add(packetOperationIndex);
			return;
		}
		if (cubeSnapshot.ItemsCount < 0
			|| cubeSnapshot.NpcExpands is < 0 or > byte.MaxValue
			|| cubeSnapshot.QuestExpands is < 0 or > byte.MaxValue
			|| cubeSnapshot.ItemExpands is < 0 or > byte.MaxValue)
		{
			invalidCubePacketOperationIndexes.Add(packetOperationIndex);
			return;
		}

		cubeInputs[packetOperationIndex] = CreateCubeInput(operation, cubeSnapshot);
	}

	private static ItemPurificationCubePacketInput CreateCubeInput(
		ItemPurificationApplicationOperation operation,
		ItemPurificationCubeSnapshot cubeSnapshot)
	{
		return new ItemPurificationCubePacketInput(
			ItemPurificationPacketPlanService.CubeStorageTypeId,
			ItemPurificationPacketPlanService.CubeStorageTypeOrdinal,
			operation.Type,
			operation.ObjectId,
			operation.ItemId,
			cubeSnapshot.ItemsCount,
			cubeSnapshot.NpcExpands,
			cubeSnapshot.QuestExpands,
			cubeSnapshot.ItemExpands);
	}

	public static ItemPurificationCubeSnapshot CreateCubeSnapshot(
		IReadOnlyList<InventoryItem> postMutationInventoryItems,
		int npcExpands,
		int questExpands,
		int itemExpands)
	{
		// Java parity: player.getInventory().size() delegates to ItemStorage.size(); kinah is stored
		// separately in Java Storage, so this excludes C#'s synthetic kinah InventoryItem.
		var itemsCount = postMutationInventoryItems.Count(item => item.Location == CubeStorageId && item.ItemId != KinahItemId);
		return new ItemPurificationCubeSnapshot(itemsCount, npcExpands, questExpands, itemExpands);
	}
}

public sealed record ItemPurificationPacketInputSnapshotResult(
	ItemPurificationPacketInputSnapshotStatus Status,
	IReadOnlyDictionary<int, ItemPurificationInventoryPacketInput> InventoryPacketInputs,
	IReadOnlyDictionary<int, ItemPurificationCubePacketInput> CubePacketInputsByPacketOperationIndex,
	IReadOnlyList<int> MissingTemplateIds,
	IReadOnlyList<int> MissingInventoryObjectIds,
	IReadOnlyList<int> MismatchedInventoryObjectIds,
	IReadOnlyList<int> MissingCubePacketOperationIndexes,
	IReadOnlyList<int> InvalidCubePacketOperationIndexes)
{
	public bool Succeeded => Status == ItemPurificationPacketInputSnapshotStatus.Ready;

	public static ItemPurificationPacketInputSnapshotResult Failed(ItemPurificationPacketInputSnapshotStatus status)
	{
		return new ItemPurificationPacketInputSnapshotResult(
			status,
			new Dictionary<int, ItemPurificationInventoryPacketInput>(),
			new Dictionary<int, ItemPurificationCubePacketInput>(),
			Array.Empty<int>(),
			Array.Empty<int>(),
			Array.Empty<int>(),
			Array.Empty<int>(),
			Array.Empty<int>());
	}
}

public sealed record ItemPurificationCubeSnapshot(
	int ItemsCount,
	int NpcExpands,
	int QuestExpands,
	int ItemExpands);

public enum ItemPurificationPacketInputSnapshotStatus
{
	Ready,
	MissingApplicationPlan,
	ApplicationPlanUnavailable,
	ApplicationPlanNotReady,
	MissingInventorySnapshots,
	MismatchedInventorySnapshots,
	MissingTemplates,
	MissingCubeSnapshots,
	InvalidCubeSnapshots,
}
