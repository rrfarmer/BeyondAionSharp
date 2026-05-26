using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahOwnerRollbackPlanStatus
{
	StoppedNotEnoughKinah,
	ContinueWithoutMutation,
	AwaitingPersistenceOrSendResult,
	RollbackRequired,
	CommitReady,
}

public enum BindPointTeleportKinahOwnerRollbackPlanStep
{
	CaptureOriginalKinah,
	ApplyUpdatedKinah,
	WaitForPersistenceResult,
	WaitForSendResult,
	RollbackToOriginalKinah,
	CommitUpdatedKinah,
	ContinueWithoutMutation,
}

public sealed record BindPointTeleportKinahOwnerRollbackPlan(
	BindPointTeleportKinahOwnerRollbackPlanStatus Status,
	InventoryItem? OriginalKinahItem,
	InventoryItem? UpdatedKinahItem,
	IReadOnlyList<InventoryItem> InventoryAfterMutation,
	IReadOnlyList<InventoryItem> InventoryAfterRollback,
	IReadOnlyList<BindPointTeleportKinahOwnerRollbackPlanStep> Steps,
	bool ShouldApplyInMemoryMutation,
	bool ShouldRollbackInMemoryMutation,
	bool ShouldContinueToCooldownFanout,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahOwnerRollbackPlanService
{
	public static BindPointTeleportKinahOwnerRollbackPlan CreatePlan(
		Player originalPlayer,
		BindPointTeleportScheduledKinahMutationPlan mutationPlan,
		BindPointTeleportKinahPersistenceDecision? persistenceDecision = null,
		BindPointTeleportKinahInventorySendDecision? sendDecision = null)
	{
		// Java parity: Storage.tryDecreaseKinah mutates the player's storage directly and dirty persistence is later.
		// This C# owner plan is non-live; it records how a future owner can restore the original snapshot when the
		// staged persist-before-send policy fails.
		var originalInventory = originalPlayer.InventoryItems.ToList();
		var originalKinah = originalInventory.FirstOrDefault(item =>
			item.ItemId == BindPointTeleportScheduledKinahMutationPlanService.KinahItemId
			&& item.Location == BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId);

		if (mutationPlan.Status == BindPointTeleportScheduledKinahMutationPlanStatus.NotEnoughKinah)
		{
			return new BindPointTeleportKinahOwnerRollbackPlan(
				BindPointTeleportKinahOwnerRollbackPlanStatus.StoppedNotEnoughKinah,
				originalKinah,
				UpdatedKinahItem: null,
				InventoryAfterMutation: originalInventory,
				InventoryAfterRollback: originalInventory,
				[
					BindPointTeleportKinahOwnerRollbackPlanStep.CaptureOriginalKinah,
				],
				ShouldApplyInMemoryMutation: false,
				ShouldRollbackInMemoryMutation: false,
				ShouldContinueToCooldownFanout: false,
				"BindPointTeleportService.teleport scheduled task -> tryDecreaseKinah failed -> send fee message and return before mutation",
				IsLive: false);
		}

		if (mutationPlan.KinahItemUpdate == null || !mutationPlan.ShouldEmitInventoryUpdatePacket)
		{
			return new BindPointTeleportKinahOwnerRollbackPlan(
				BindPointTeleportKinahOwnerRollbackPlanStatus.ContinueWithoutMutation,
				originalKinah,
				UpdatedKinahItem: null,
				InventoryAfterMutation: originalInventory,
				InventoryAfterRollback: originalInventory,
				[
					BindPointTeleportKinahOwnerRollbackPlanStep.CaptureOriginalKinah,
					BindPointTeleportKinahOwnerRollbackPlanStep.ContinueWithoutMutation,
				],
				ShouldApplyInMemoryMutation: false,
				ShouldRollbackInMemoryMutation: false,
				ShouldContinueToCooldownFanout: true,
				"Storage.decreaseKinah amount > 0 guard prevents item mutation for non-positive scheduled payment",
				IsLive: false);
		}

		if (persistenceDecision == null
			|| persistenceDecision.Status != BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence
			|| sendDecision == null)
		{
			return AwaitingOrRollback(
				BindPointTeleportKinahOwnerRollbackPlanStatus.AwaitingPersistenceOrSendResult,
				originalKinah,
				mutationPlan.KinahItemUpdate,
				mutationPlan.InventoryItems,
				originalInventory,
				[
					BindPointTeleportKinahOwnerRollbackPlanStep.CaptureOriginalKinah,
					BindPointTeleportKinahOwnerRollbackPlanStep.ApplyUpdatedKinah,
					BindPointTeleportKinahOwnerRollbackPlanStep.WaitForPersistenceResult,
					BindPointTeleportKinahOwnerRollbackPlanStep.WaitForSendResult,
				],
				"C# staging guard: future owner must wait for saved persistence and sent packet result before committing the in-memory Kinah mutation");
		}

		if (sendDecision.Status != BindPointTeleportKinahInventorySendDecisionStatus.ReadyForCooldownFanout)
		{
			return AwaitingOrRollback(
				BindPointTeleportKinahOwnerRollbackPlanStatus.RollbackRequired,
				originalKinah,
				mutationPlan.KinahItemUpdate,
				mutationPlan.InventoryItems,
				originalInventory,
				[
					BindPointTeleportKinahOwnerRollbackPlanStep.CaptureOriginalKinah,
					BindPointTeleportKinahOwnerRollbackPlanStep.ApplyUpdatedKinah,
					BindPointTeleportKinahOwnerRollbackPlanStep.RollbackToOriginalKinah,
				],
				"Scheduled bind-point Kinah packet send did not reach the ready-for-fanout gate; future owner must restore the original Kinah item before stopping");
		}

		return new BindPointTeleportKinahOwnerRollbackPlan(
			BindPointTeleportKinahOwnerRollbackPlanStatus.CommitReady,
			originalKinah,
			mutationPlan.KinahItemUpdate,
			mutationPlan.InventoryItems,
			InventoryAfterRollback: originalInventory,
			[
				BindPointTeleportKinahOwnerRollbackPlanStep.CaptureOriginalKinah,
				BindPointTeleportKinahOwnerRollbackPlanStep.ApplyUpdatedKinah,
				BindPointTeleportKinahOwnerRollbackPlanStep.CommitUpdatedKinah,
			],
			ShouldApplyInMemoryMutation: true,
			ShouldRollbackInMemoryMutation: false,
			ShouldContinueToCooldownFanout: true,
			"Scheduled bind-point Kinah persistence and inventory packet send reached the ready gate; future owner may commit the updated Kinah snapshot and continue",
			IsLive: false);
	}

	private static BindPointTeleportKinahOwnerRollbackPlan AwaitingOrRollback(
		BindPointTeleportKinahOwnerRollbackPlanStatus status,
		InventoryItem? originalKinah,
		InventoryItem updatedKinah,
		IReadOnlyList<InventoryItem> inventoryAfterMutation,
		IReadOnlyList<InventoryItem> inventoryAfterRollback,
		IReadOnlyList<BindPointTeleportKinahOwnerRollbackPlanStep> steps,
		string javaSource)
	{
		return new BindPointTeleportKinahOwnerRollbackPlan(
			status,
			originalKinah,
			updatedKinah,
			inventoryAfterMutation,
			inventoryAfterRollback,
			steps,
			ShouldApplyInMemoryMutation: true,
			ShouldRollbackInMemoryMutation: true,
			ShouldContinueToCooldownFanout: false,
			javaSource,
			IsLive: false);
	}
}
