using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationPersistencePlanService
{
	public static ItemPurificationPersistencePlan CreatePersistencePlan(
		ItemPurificationApplicationPlan? applicationPlan,
		ItemPurificationMutationSnapshotPlan? mutationPreview,
		AbyssPointsAddPlan? abyssPointsPlan)
	{
		// Java parity: services/item/ItemPurificationService.decreaseMaterials plus upgradeItem
		// marks material/base/target inventory rows dirty and AbyssPointsService.addAp marks the
		// rank dirty. This service projects those live mutation results into repository payloads.
		if (applicationPlan == null)
			return ItemPurificationPersistencePlan.Failed(ItemPurificationPersistencePlanStatus.MissingApplicationPlan);
		if (!applicationPlan.Succeeded)
			return ItemPurificationPersistencePlan.Failed(ItemPurificationPersistencePlanStatus.ApplicationPlanNotReady);
		if (mutationPreview == null)
			return ItemPurificationPersistencePlan.Failed(ItemPurificationPersistencePlanStatus.MissingMutationPreview);
		if (!mutationPreview.Succeeded)
			return ItemPurificationPersistencePlan.Failed(ItemPurificationPersistencePlanStatus.MutationPreviewNotReady);

		var postItemsByObjectId = mutationPreview.PostMutationInventoryItems.ToDictionary(item => item.ObjectId);
		var materialItemUpdates = new List<InventoryItem>();
		var deletedMaterialItemObjectIds = new List<int>();
		InventoryItem? baseItemUpdate = null;
		int? deletedBaseItemObjectId = null;
		var updatedTargetItems = new List<InventoryItem>();
		var addedTargetItems = new List<InventoryItem>();

		foreach (var operation in applicationPlan.Operations)
		{
			switch (operation.Type)
			{
				case ItemPurificationApplicationOperationType.UpdateMaterialItemCount:
					if (!TryGetPostItem(postItemsByObjectId, operation, out var materialItem))
						return ItemPurificationPersistencePlan.Failed(ItemPurificationPersistencePlanStatus.MissingUpdatedItemSnapshot);
					materialItemUpdates.Add(materialItem);
					break;
				case ItemPurificationApplicationOperationType.DeleteMaterialItem:
					deletedMaterialItemObjectIds.Add(operation.ObjectId);
					break;
				case ItemPurificationApplicationOperationType.UpdateBaseItemCount:
					if (!TryGetPostItem(postItemsByObjectId, operation, out var baseItem))
						return ItemPurificationPersistencePlan.Failed(ItemPurificationPersistencePlanStatus.MissingUpdatedItemSnapshot);
					baseItemUpdate = baseItem;
					break;
				case ItemPurificationApplicationOperationType.DeleteBaseItem:
					deletedBaseItemObjectId = operation.ObjectId;
					break;
				case ItemPurificationApplicationOperationType.AddTargetItem:
					if (!TryGetPostItem(postItemsByObjectId, operation, out var targetItem))
						return ItemPurificationPersistencePlan.Failed(ItemPurificationPersistencePlanStatus.MissingTargetItemSnapshot);
					addedTargetItems.Add(targetItem);
					break;
				case ItemPurificationApplicationOperationType.SpendAbyssPoints:
				case ItemPurificationApplicationOperationType.PreserveKinahNoOp:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(applicationPlan), operation.Type, "Unsupported item purification operation.");
			}
		}

		return new ItemPurificationPersistencePlan(
			ItemPurificationPersistencePlanStatus.Ready,
			materialItemUpdates,
			deletedMaterialItemObjectIds,
			baseItemUpdate,
			deletedBaseItemObjectId,
			updatedTargetItems,
			addedTargetItems,
			abyssPointsPlan?.UpdatedRank);
	}

	private static bool TryGetPostItem(
		IReadOnlyDictionary<int, InventoryItem> postItemsByObjectId,
		ItemPurificationApplicationOperation operation,
		out InventoryItem item)
	{
		if (postItemsByObjectId.TryGetValue(operation.ObjectId, out item!)
			&& item.ItemId == operation.ItemId
			&& item.Count == operation.NewCount)
			return true;

		item = null!;
		return false;
	}
}

public sealed record ItemPurificationPersistencePlan(
	ItemPurificationPersistencePlanStatus Status,
	IReadOnlyList<InventoryItem> MaterialItemUpdates,
	IReadOnlyList<int> DeletedMaterialItemObjectIds,
	InventoryItem? BaseItemUpdate,
	int? DeletedBaseItemObjectId,
	IReadOnlyList<InventoryItem> UpdatedTargetItems,
	IReadOnlyList<InventoryItem> AddedTargetItems,
	PlayerAbyssRank? AbyssRank)
{
	public bool Succeeded => Status == ItemPurificationPersistencePlanStatus.Ready;

	public static ItemPurificationPersistencePlan Failed(ItemPurificationPersistencePlanStatus status)
	{
		return new ItemPurificationPersistencePlan(
			status,
			Array.Empty<InventoryItem>(),
			Array.Empty<int>(),
			BaseItemUpdate: null,
			DeletedBaseItemObjectId: null,
			Array.Empty<InventoryItem>(),
			Array.Empty<InventoryItem>(),
			AbyssRank: null);
	}
}

public enum ItemPurificationPersistencePlanStatus
{
	Ready,
	MissingApplicationPlan,
	ApplicationPlanNotReady,
	MissingMutationPreview,
	MutationPreviewNotReady,
	MissingUpdatedItemSnapshot,
	MissingTargetItemSnapshot,
}

