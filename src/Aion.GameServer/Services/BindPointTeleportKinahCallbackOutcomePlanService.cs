namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahCallbackOutcomeStatus
{
	StoppedNotEnoughKinah,
	ContinueWithoutMutation,
	AwaitingPersistenceResult,
	RollbackAfterPersistenceFailure,
	AwaitingSendResult,
	RollbackAfterSendFailure,
	ReadyForCooldownFanout,
}

public enum BindPointTeleportKinahCallbackOutcomeStep
{
	ReviewMutationPlan,
	ReviewPersistenceOperation,
	ReviewPersistenceDecision,
	ReviewSendDecision,
	ReviewOwnerRollbackPlan,
	StopBeforePersistence,
	RollbackMutation,
	CommitMutation,
	ContinueWithoutMutation,
	ContinueToCooldownFanout,
}

public sealed record BindPointTeleportKinahCallbackOutcomePlan(
	BindPointTeleportKinahCallbackOutcomeStatus Status,
	BindPointTeleportScheduledKinahMutationPlan MutationPlan,
	BindPointTeleportKinahPersistenceOperationPlan PersistenceOperationPlan,
	BindPointTeleportKinahPersistenceDecision PersistenceDecision,
	BindPointTeleportKinahInventorySendAdapterPlan? SendAdapterPlan,
	BindPointTeleportKinahInventorySendDecision? SendDecision,
	BindPointTeleportKinahOwnerRollbackPlan OwnerRollbackPlan,
	IReadOnlyList<BindPointTeleportKinahCallbackOutcomeStep> Steps,
	bool ShouldExecuteSql,
	bool ShouldSendInventoryUpdatePacket,
	bool ShouldRollbackInMemoryMutation,
	bool ShouldCommitInMemoryMutation,
	bool ShouldContinueToCooldownFanout,
	bool ShouldScheduleFinalTeleport,
	bool ShouldTeleport,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahCallbackOutcomePlanService
{
	public static BindPointTeleportKinahCallbackOutcomePlan CreatePlan(
		BindPointTeleportScheduledKinahMutationPlan mutationPlan,
		BindPointTeleportKinahPersistenceOperationPlan persistenceOperationPlan,
		BindPointTeleportKinahPersistenceDecision persistenceDecision,
		BindPointTeleportKinahInventorySendAdapterPlan? sendAdapterPlan,
		BindPointTeleportKinahInventorySendDecision? sendDecision,
		BindPointTeleportKinahOwnerRollbackPlan ownerRollbackPlan)
	{
		// Java parity: the scheduled callback's successful side effects are ordered as
		// tryDecreaseKinah -> inventory update packet -> addCooldown/action 3 -> final movement.
		// This C# outcome is a metadata verdict only; it does not execute any side effect.
		if (mutationPlan.Status == BindPointTeleportScheduledKinahMutationPlanStatus.NotEnoughKinah
			|| persistenceDecision.Status == BindPointTeleportKinahPersistenceDecisionStatus.StoppedNotEnoughKinah)
		{
			return Stop(
				BindPointTeleportKinahCallbackOutcomeStatus.StoppedNotEnoughKinah,
				mutationPlan,
				persistenceOperationPlan,
				persistenceDecision,
				sendAdapterPlan,
				sendDecision,
				ownerRollbackPlan,
				[
					BindPointTeleportKinahCallbackOutcomeStep.ReviewMutationPlan,
					BindPointTeleportKinahCallbackOutcomeStep.StopBeforePersistence,
				],
				shouldRollback: false,
				"BindPointTeleportService scheduled callback failed tryDecreaseKinah and stops before persistence, packet send, cooldown, fanout, and movement");
		}

		if (!mutationPlan.ShouldEmitInventoryUpdatePacket
			&& persistenceDecision.Status == BindPointTeleportKinahPersistenceDecisionStatus.ContinueWithoutPersistence
			&& ownerRollbackPlan.Status == BindPointTeleportKinahOwnerRollbackPlanStatus.ContinueWithoutMutation)
		{
			return new BindPointTeleportKinahCallbackOutcomePlan(
				BindPointTeleportKinahCallbackOutcomeStatus.ContinueWithoutMutation,
				mutationPlan,
				persistenceOperationPlan,
				persistenceDecision,
				sendAdapterPlan,
				sendDecision,
				ownerRollbackPlan,
				[
					BindPointTeleportKinahCallbackOutcomeStep.ReviewMutationPlan,
					BindPointTeleportKinahCallbackOutcomeStep.ContinueWithoutMutation,
					BindPointTeleportKinahCallbackOutcomeStep.ContinueToCooldownFanout,
				],
				ShouldExecuteSql: false,
				ShouldSendInventoryUpdatePacket: false,
				ShouldRollbackInMemoryMutation: false,
				ShouldCommitInMemoryMutation: false,
				persistenceDecision.ShouldContinueToCooldownFanout,
				persistenceDecision.ShouldScheduleFinalTeleport,
				persistenceDecision.ShouldTeleport,
				"Storage.decreaseKinah amount > 0 guard avoided Kinah mutation; metadata may continue without persistence or inventory packet send",
				IsLive: false);
		}

		if (persistenceDecision.Status == BindPointTeleportKinahPersistenceDecisionStatus.StoppedMissingPersistenceResult)
		{
			return Stop(
				BindPointTeleportKinahCallbackOutcomeStatus.AwaitingPersistenceResult,
				mutationPlan,
				persistenceOperationPlan,
				persistenceDecision,
				sendAdapterPlan,
				sendDecision,
				ownerRollbackPlan,
				[
					BindPointTeleportKinahCallbackOutcomeStep.ReviewMutationPlan,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewPersistenceOperation,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewPersistenceDecision,
				],
				shouldRollback: ownerRollbackPlan.ShouldRollbackInMemoryMutation,
				"C# scheduled Kinah callback metadata is waiting for an owner-checked persistence result before packet send can be considered");
		}

		if (persistenceDecision.Status is BindPointTeleportKinahPersistenceDecisionStatus.StoppedMissingRow
			or BindPointTeleportKinahPersistenceDecisionStatus.StoppedFailed)
		{
			return Stop(
				BindPointTeleportKinahCallbackOutcomeStatus.RollbackAfterPersistenceFailure,
				mutationPlan,
				persistenceOperationPlan,
				persistenceDecision,
				sendAdapterPlan,
				sendDecision,
				ownerRollbackPlan,
				[
					BindPointTeleportKinahCallbackOutcomeStep.ReviewMutationPlan,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewPersistenceOperation,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewPersistenceDecision,
					BindPointTeleportKinahCallbackOutcomeStep.RollbackMutation,
				],
				shouldRollback: true,
				"Owner-checked scheduled Kinah persistence failed or found no row; rollback is required before packet send, cooldown, fanout, or movement");
		}

		if (persistenceDecision.Status != BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence
			|| sendDecision == null
			|| sendAdapterPlan == null)
		{
			return Stop(
				BindPointTeleportKinahCallbackOutcomeStatus.AwaitingSendResult,
				mutationPlan,
				persistenceOperationPlan,
				persistenceDecision,
				sendAdapterPlan,
				sendDecision,
				ownerRollbackPlan,
				[
					BindPointTeleportKinahCallbackOutcomeStep.ReviewMutationPlan,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewPersistenceDecision,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewSendDecision,
				],
				shouldRollback: ownerRollbackPlan.ShouldRollbackInMemoryMutation,
				"C# scheduled Kinah callback metadata has not reached a send decision; cooldown/action 3 fanout remains blocked");
		}

		if (sendDecision.Status != BindPointTeleportKinahInventorySendDecisionStatus.ReadyForCooldownFanout
			|| ownerRollbackPlan.Status == BindPointTeleportKinahOwnerRollbackPlanStatus.RollbackRequired)
		{
			return Stop(
				BindPointTeleportKinahCallbackOutcomeStatus.RollbackAfterSendFailure,
				mutationPlan,
				persistenceOperationPlan,
				persistenceDecision,
				sendAdapterPlan,
				sendDecision,
				ownerRollbackPlan,
				[
					BindPointTeleportKinahCallbackOutcomeStep.ReviewMutationPlan,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewPersistenceDecision,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewSendDecision,
					BindPointTeleportKinahCallbackOutcomeStep.ReviewOwnerRollbackPlan,
					BindPointTeleportKinahCallbackOutcomeStep.RollbackMutation,
				],
				shouldRollback: true,
				"Scheduled bind-point Kinah inventory update packet did not reach the ready gate; rollback is required before cooldown/action 3 fanout");
		}

		return new BindPointTeleportKinahCallbackOutcomePlan(
			BindPointTeleportKinahCallbackOutcomeStatus.ReadyForCooldownFanout,
			mutationPlan,
			persistenceOperationPlan,
			persistenceDecision,
			sendAdapterPlan,
			sendDecision,
			ownerRollbackPlan,
			[
				BindPointTeleportKinahCallbackOutcomeStep.ReviewMutationPlan,
				BindPointTeleportKinahCallbackOutcomeStep.ReviewPersistenceDecision,
				BindPointTeleportKinahCallbackOutcomeStep.ReviewSendDecision,
				BindPointTeleportKinahCallbackOutcomeStep.ReviewOwnerRollbackPlan,
				BindPointTeleportKinahCallbackOutcomeStep.CommitMutation,
				BindPointTeleportKinahCallbackOutcomeStep.ContinueToCooldownFanout,
			],
			ShouldExecuteSql: persistenceOperationPlan.ShouldExecuteSql,
			ShouldSendInventoryUpdatePacket: true,
			ShouldRollbackInMemoryMutation: false,
			ShouldCommitInMemoryMutation: ownerRollbackPlan.Status == BindPointTeleportKinahOwnerRollbackPlanStatus.CommitReady,
			ShouldContinueToCooldownFanout: true,
			sendDecision.ShouldScheduleFinalTeleport,
			sendDecision.ShouldTeleport,
			"Scheduled bind-point Kinah metadata reached saved persistence, sent packet, owner commit, cooldown/action 3 fanout readiness",
			IsLive: false);
	}

	private static BindPointTeleportKinahCallbackOutcomePlan Stop(
		BindPointTeleportKinahCallbackOutcomeStatus status,
		BindPointTeleportScheduledKinahMutationPlan mutationPlan,
		BindPointTeleportKinahPersistenceOperationPlan persistenceOperationPlan,
		BindPointTeleportKinahPersistenceDecision persistenceDecision,
		BindPointTeleportKinahInventorySendAdapterPlan? sendAdapterPlan,
		BindPointTeleportKinahInventorySendDecision? sendDecision,
		BindPointTeleportKinahOwnerRollbackPlan ownerRollbackPlan,
		IReadOnlyList<BindPointTeleportKinahCallbackOutcomeStep> steps,
		bool shouldRollback,
		string javaSource)
	{
		return new BindPointTeleportKinahCallbackOutcomePlan(
			status,
			mutationPlan,
			persistenceOperationPlan,
			persistenceDecision,
			sendAdapterPlan,
			sendDecision,
			ownerRollbackPlan,
			steps,
			ShouldExecuteSql: persistenceOperationPlan.ShouldExecuteSql,
			ShouldSendInventoryUpdatePacket: false,
			ShouldRollbackInMemoryMutation: shouldRollback,
			ShouldCommitInMemoryMutation: false,
			ShouldContinueToCooldownFanout: false,
			ShouldScheduleFinalTeleport: false,
			ShouldTeleport: false,
			javaSource,
			IsLive: false);
	}
}
