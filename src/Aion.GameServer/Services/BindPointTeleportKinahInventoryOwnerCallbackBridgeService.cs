namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus
{
	StoppedNotEnoughKinah,
	ContinueWithoutMutation,
	MutationReady,
}

public sealed record BindPointTeleportKinahInventoryOwnerCallbackBridgePlan(
	BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus Status,
	BindPointTeleportKinahInventoryOwnerMutationResult OwnerMutationResult,
	BindPointTeleportScheduledKinahMutationPlan MutationPlan,
	BindPointTeleportKinahPersistenceOperationPlan PersistenceOperationPlan,
	bool ShouldCreatePersistenceDecision,
	bool ShouldCreateInventoryPacketIntent,
	bool ShouldContinueScheduledTeleport,
	bool ShouldSendNotEnoughFee,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahInventoryOwnerCallbackBridgeService
{
	public static BindPointTeleportKinahInventoryOwnerCallbackBridgePlan CreatePlan(
		BindPointTeleportKinahInventoryOwnerMutationResult ownerMutationResult)
	{
		// Java parity: this bridge adapts the in-memory owner result back into the scheduled
		// callback metadata chain. It does not persist, send, broadcast, or move.
		var mutationPlan = CreateMutationPlan(ownerMutationResult);
		var persistenceOperationPlan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(mutationPlan);
		var status = ownerMutationResult.Status switch
		{
			BindPointTeleportKinahInventoryOwnerMutationStatus.NotEnoughKinah =>
				BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.StoppedNotEnoughKinah,
			BindPointTeleportKinahInventoryOwnerMutationStatus.ContinueWithoutMutation =>
				BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.ContinueWithoutMutation,
			_ => BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.MutationReady,
		};

		return new BindPointTeleportKinahInventoryOwnerCallbackBridgePlan(
			status,
			ownerMutationResult,
			mutationPlan,
			persistenceOperationPlan,
			ShouldCreatePersistenceDecision: status == BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.MutationReady,
			ShouldCreateInventoryPacketIntent: false,
			ownerMutationResult.ShouldContinueScheduledTeleport,
			ownerMutationResult.ShouldSendNotEnoughFee,
			status switch
			{
				BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.StoppedNotEnoughKinah =>
					"BindPointTeleportService scheduled callback owner result failed Kinah decrement and stops before persistence, packet send, cooldown, fanout, and movement",
				BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.ContinueWithoutMutation =>
					"Storage.decreaseKinah amount > 0 guard avoided mutation; callback metadata may continue without Kinah persistence or inventory packet",
				_ =>
					"Scheduled bind-point Kinah owner result applied in-memory decrement and is ready for persistence decision metadata before packet send",
			},
			IsLive: false);
	}

	private static BindPointTeleportScheduledKinahMutationPlan CreateMutationPlan(
		BindPointTeleportKinahInventoryOwnerMutationResult ownerMutationResult)
	{
		return new BindPointTeleportScheduledKinahMutationPlan(
			ownerMutationResult.Status == BindPointTeleportKinahInventoryOwnerMutationStatus.NotEnoughKinah
				? BindPointTeleportScheduledKinahMutationPlanStatus.NotEnoughKinah
				: BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady,
			ownerMutationResult.RequiredPrice,
			ownerMutationResult.OriginalKinah,
			ownerMutationResult.RemainingKinah,
			ownerMutationResult.UpdatedKinahItem,
			ownerMutationResult.InventoryAfterMutation,
			ownerMutationResult.ShouldSendNotEnoughFee,
			ownerMutationResult.ShouldEmitInventoryUpdatePacket,
			ownerMutationResult.InventoryUpdateType,
			ownerMutationResult.Status switch
			{
				BindPointTeleportKinahInventoryOwnerMutationStatus.NotEnoughKinah =>
				[
					BindPointTeleportScheduledKinahMutationPlanStep.FindCubeKinahItem,
					BindPointTeleportScheduledKinahMutationPlanStep.CheckEnoughKinah,
					BindPointTeleportScheduledKinahMutationPlanStep.SendNotEnoughFee,
				],
				BindPointTeleportKinahInventoryOwnerMutationStatus.ContinueWithoutMutation =>
				[
					BindPointTeleportScheduledKinahMutationPlanStep.FindCubeKinahItem,
					BindPointTeleportScheduledKinahMutationPlanStep.CheckEnoughKinah,
					BindPointTeleportScheduledKinahMutationPlanStep.ContinueWithoutMutation,
				],
				_ =>
				[
					BindPointTeleportScheduledKinahMutationPlanStep.FindCubeKinahItem,
					BindPointTeleportScheduledKinahMutationPlanStep.CheckEnoughKinah,
					BindPointTeleportScheduledKinahMutationPlanStep.PrepareKinahItemUpdate,
					BindPointTeleportScheduledKinahMutationPlanStep.PrepareInventoryUpdatePacket,
				],
			},
			ownerMutationResult.JavaSource,
			IsLive: false);
	}
}
