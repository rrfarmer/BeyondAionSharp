using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationApplicationPlanService
{
	private const int KinahItemId = 182400001;

	public static ItemPurificationApplicationPlan CreateApplicationPlan(ItemPurificationWorkflowPlan? workflow)
	{
		// Java parity: network/aion/clientpackets/CM_ITEM_PURIFICATION calls
		// ItemPurificationService.decreaseMaterials before upgradeItem. The operations below preserve that
		// runtime/persistence/packet order without executing any of the side effects.
		if (workflow == null)
			return ItemPurificationApplicationPlan.Failed(ItemPurificationApplicationPlanStatus.MissingWorkflow);
		if (!workflow.Succeeded)
			return ItemPurificationApplicationPlan.Failed(ItemPurificationApplicationPlanStatus.WorkflowNotPlanned);
		if (workflow.MaterialMutation == null)
			return ItemPurificationApplicationPlan.Failed(ItemPurificationApplicationPlanStatus.MissingMaterialMutation);
		if (workflow.Inheritance == null)
			return ItemPurificationApplicationPlan.Failed(ItemPurificationApplicationPlanStatus.MissingInheritance);
		if (workflow.Inheritance.TargetItem == null)
			return ItemPurificationApplicationPlan.Failed(ItemPurificationApplicationPlanStatus.MissingTargetItem);

		var materialMutation = workflow.MaterialMutation;
		var inheritance = workflow.Inheritance;
		var operations = new List<ItemPurificationApplicationOperation>();
		var steps = materialMutation.MutationSteps;
		var baseStep = materialMutation.BaseItemDeleteAttempted && steps.Count > 0
			? steps[^1]
			: null;
		var materialSteps = baseStep == null ? steps : steps.Take(steps.Count - 1);

		foreach (var step in materialSteps)
			operations.Add(ToItemOperation(step, isBaseItem: false, materialMutation.DeletedObjectIds));

		if (materialMutation.AbyssPointsToSpend > 0)
		{
			operations.Add(new ItemPurificationApplicationOperation(
				ItemPurificationApplicationOperationType.SpendAbyssPoints,
				ItemPurificationApplicationEffect.RuntimeState
					| ItemPurificationApplicationEffect.Persistence
					| ItemPurificationApplicationEffect.Packet
					| ItemPurificationApplicationEffect.RankSideEffects,
				ObjectId: 0,
				ItemId: 0,
				Count: materialMutation.AbyssPointsToSpend,
				NewCount: 0));
		}

		if (materialMutation.NecessaryKinah > 0)
		{
			operations.Add(new ItemPurificationApplicationOperation(
				ItemPurificationApplicationOperationType.PreserveKinahNoOp,
				ItemPurificationApplicationEffect.None,
				ObjectId: 0,
				ItemId: KinahItemId,
				Count: materialMutation.NecessaryKinah,
				NewCount: 0));
		}

		if (baseStep != null)
			operations.Add(ToItemOperation(baseStep, isBaseItem: true, materialMutation.DeletedObjectIds));

		var targetItem = inheritance.TargetItem;
		operations.Add(new ItemPurificationApplicationOperation(
			ItemPurificationApplicationOperationType.AddTargetItem,
			ItemPurificationApplicationEffect.RuntimeState
				| ItemPurificationApplicationEffect.Persistence
				| ItemPurificationApplicationEffect.Packet
				| ItemPurificationApplicationEffect.QuestNotification,
			targetItem.ObjectId,
			targetItem.ItemId,
			targetItem.Count,
			targetItem.Count));

		var requiresTargetObjectIdAllocation = targetItem.ObjectId <= 0;
		var requiresRandomBonusSelection = inheritance.RandomBonusWasRerolled && targetItem.RandomBonus <= 0;
		var status = DetermineStatus(
			requiresTargetObjectIdAllocation,
			requiresRandomBonusSelection,
			materialMutation.BaseItemDeleteAttempted && !materialMutation.BaseItemDeleted);

		return new ItemPurificationApplicationPlan(
			status,
			operations,
			requiresTargetObjectIdAllocation,
			requiresRandomBonusSelection,
			KinahMutationApplied: materialMutation.KinahSpendApplied,
			materialMutation.AbyssPointsToSpend,
			materialMutation.NecessaryKinah,
			targetItem);
	}

	public static IReadOnlyList<ItemPurificationQuestNotificationCandidate> ProjectQuestNotifications(
		ItemPurificationApplicationPlan? plan)
	{
		// Java parity: Storage.delete notifies QuestEngine.onItemRemoved only for actual
		// non-Kinah deletes, while Storage.add notifies QuestEngine.onItemGet for actor-backed
		// CUBE additions after storage update. This projection records intent only; it does not
		// execute quest handlers or nearby-quest refresh.
		if (plan == null)
			return Array.Empty<ItemPurificationQuestNotificationCandidate>();

		var notifications = new List<ItemPurificationQuestNotificationCandidate>();
		foreach (var operation in plan.Operations)
		{
			if (!operation.Effects.HasFlag(ItemPurificationApplicationEffect.QuestNotification))
				continue;

			var notificationType = operation.Type switch
			{
				ItemPurificationApplicationOperationType.DeleteMaterialItem
					or ItemPurificationApplicationOperationType.DeleteBaseItem
					=> ItemPurificationQuestNotificationType.ItemRemoved,
				ItemPurificationApplicationOperationType.AddTargetItem
					=> ItemPurificationQuestNotificationType.ItemGet,
				_ => (ItemPurificationQuestNotificationType?)null,
			};
			if (notificationType == null)
				continue;

			notifications.Add(new ItemPurificationQuestNotificationCandidate(
				notificationType.Value,
				operation.Type,
				operation.ObjectId,
				operation.ItemId));
		}

		return notifications;
	}

	private static ItemPurificationApplicationPlanStatus DetermineStatus(
		bool requiresTargetObjectIdAllocation,
		bool requiresRandomBonusSelection,
		bool baseItemDeleteNeedsVerification)
	{
		if (requiresTargetObjectIdAllocation)
			return ItemPurificationApplicationPlanStatus.NeedsTargetObjectIdAllocation;
		if (requiresRandomBonusSelection)
			return ItemPurificationApplicationPlanStatus.NeedsRandomBonusSelection;
		if (baseItemDeleteNeedsVerification)
			return ItemPurificationApplicationPlanStatus.NeedsBaseItemDeleteVerification;
		return ItemPurificationApplicationPlanStatus.Ready;
	}

	private static ItemPurificationApplicationOperation ToItemOperation(
		ItemPurificationMutationStep step,
		bool isBaseItem,
		IReadOnlyList<int> deletedObjectIds)
	{
		var deletesItem = step.NewCount == 0 && deletedObjectIds.Contains(step.ObjectId);
		var operationType = (isBaseItem, deletesItem) switch
		{
			(true, true) => ItemPurificationApplicationOperationType.DeleteBaseItem,
			(true, false) => ItemPurificationApplicationOperationType.UpdateBaseItemCount,
			(false, true) => ItemPurificationApplicationOperationType.DeleteMaterialItem,
			_ => ItemPurificationApplicationOperationType.UpdateMaterialItemCount,
		};
		var effects = ItemPurificationApplicationEffect.RuntimeState
			| ItemPurificationApplicationEffect.Persistence
			| ItemPurificationApplicationEffect.Packet;
		if (deletesItem)
			effects |= ItemPurificationApplicationEffect.QuestNotification;

		return new ItemPurificationApplicationOperation(
			operationType,
			effects,
			step.ObjectId,
			step.ItemId,
			step.ConsumedCount,
			step.NewCount);
	}
}

public sealed record ItemPurificationApplicationPlan(
	ItemPurificationApplicationPlanStatus Status,
	IReadOnlyList<ItemPurificationApplicationOperation> Operations,
	bool RequiresTargetObjectIdAllocation,
	bool RequiresRandomBonusSelection,
	bool KinahMutationApplied,
	int AbyssPointsToSpend,
	long NecessaryKinah,
	InventoryItem? TargetItem)
{
	public bool Succeeded => Status == ItemPurificationApplicationPlanStatus.Ready;

	public static ItemPurificationApplicationPlan Failed(ItemPurificationApplicationPlanStatus status)
	{
		return new ItemPurificationApplicationPlan(
			status,
			Array.Empty<ItemPurificationApplicationOperation>(),
			RequiresTargetObjectIdAllocation: false,
			RequiresRandomBonusSelection: false,
			KinahMutationApplied: false,
			AbyssPointsToSpend: 0,
			NecessaryKinah: 0,
			TargetItem: null);
	}
}

public sealed record ItemPurificationApplicationOperation(
	ItemPurificationApplicationOperationType Type,
	ItemPurificationApplicationEffect Effects,
	int ObjectId,
	int ItemId,
	long Count,
	long NewCount);

public sealed record ItemPurificationQuestNotificationCandidate(
	ItemPurificationQuestNotificationType Type,
	ItemPurificationApplicationOperationType SourceOperation,
	int ObjectId,
	int ItemId);

public enum ItemPurificationApplicationPlanStatus
{
	Ready,
	MissingWorkflow,
	WorkflowNotPlanned,
	MissingMaterialMutation,
	MissingInheritance,
	MissingTargetItem,
	NeedsTargetObjectIdAllocation,
	NeedsRandomBonusSelection,
	NeedsBaseItemDeleteVerification,
}

public enum ItemPurificationApplicationOperationType
{
	UpdateMaterialItemCount,
	DeleteMaterialItem,
	SpendAbyssPoints,
	PreserveKinahNoOp,
	UpdateBaseItemCount,
	DeleteBaseItem,
	AddTargetItem,
}

public enum ItemPurificationQuestNotificationType
{
	ItemRemoved,
	ItemGet,
}

[Flags]
public enum ItemPurificationApplicationEffect
{
	None = 0,
	RuntimeState = 1,
	Persistence = 2,
	Packet = 4,
	QuestNotification = 8,
	RankSideEffects = 16,
}
