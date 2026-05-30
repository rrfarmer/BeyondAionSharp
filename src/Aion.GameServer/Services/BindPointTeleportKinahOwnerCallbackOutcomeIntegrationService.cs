using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed record BindPointTeleportKinahOwnerCallbackOutcomeIntegrationPlan(
	BindPointTeleportKinahInventoryOwnerMutationResult OwnerMutationResult,
	BindPointTeleportKinahInventoryOwnerCallbackBridgePlan OwnerBridgePlan,
	BindPointTeleportScheduledCallbackPlan CallbackPlan,
	BindPointTeleportKinahPersistenceResult? PersistenceResult,
	BindPointTeleportKinahPersistenceDecision PersistenceDecision,
	BindPointTeleportKinahInventoryUpdatePacketPlan PacketPlan,
	BindPointTeleportKinahCallbackComposition CallbackComposition,
	BindPointTeleportKinahInventorySendAdapterPlan? SendAdapterPlan,
	BindPointTeleportKinahInventorySendDecision? SendDecision,
	BindPointTeleportKinahOwnerRollbackPlan OwnerRollbackPlan,
	BindPointTeleportKinahInventoryOwnerRollbackResult? OwnerRollbackResult,
	BindPointTeleportKinahCallbackOutcomePlan OutcomePlan,
	bool DidRollbackOwnerMutation,
	bool DidCommitOwnerMutation,
	string JavaSource,
	bool IsLive
);

public sealed class BindPointTeleportKinahOwnerCallbackOutcomeIntegrationService
{
	private readonly BindPointTeleportKinahInventoryOwnerService _ownerService;

	public BindPointTeleportKinahOwnerCallbackOutcomeIntegrationService(BindPointTeleportKinahInventoryOwnerService? ownerService = null)
	{
		_ownerService = ownerService ?? new BindPointTeleportKinahInventoryOwnerService();
	}

	public BindPointTeleportKinahOwnerCallbackOutcomeIntegrationPlan CreatePlan(
		Player player,
		long requiredPrice,
		BindPointTeleportCooldownPlan cooldownPlan,
		BindPointTeleportFanoutPlan cooldownFanoutPlan,
		BindPointTeleportFinalMovementPlan finalMovementPlan,
		ItemTemplateSummary? kinahTemplate,
		int? persistenceAffectedRows = null,
		Exception? persistenceException = null,
		BindPointTeleportKinahInventorySendResult? suppliedSendResult = null,
		bool useDisabledSendAdapter = false,
		BindPointTeleportRuntimeCallbackExecutionResult? runtimeResult = null,
		BindPointTeleportTeleportToSideEffectPlan? teleportSideEffectPlan = null
	)
	{
		// Java parity: BindPointTeleportService.teleport scheduled task orders
		// tryDecreaseKinah -> SM_INVENTORY_UPDATE_ITEM -> addCooldown/action 3 -> final movement.
		// This integration composes metadata only; live SQL, sends, fanout, dispatch, and movement remain disabled.
		var ownerResult = _ownerService.TryApplyScheduledDecrease(player, requiredPrice);
		var ownerBridge = BindPointTeleportKinahInventoryOwnerCallbackBridgeService.CreatePlan(ownerResult);
		var callbackPlan = CreateCallbackPlan(ownerBridge, cooldownPlan, cooldownFanoutPlan, finalMovementPlan, teleportSideEffectPlan);

		var persistenceResult =
			ownerBridge.ShouldCreatePersistenceDecision && persistenceAffectedRows != null
				? BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(
					ownerBridge.PersistenceOperationPlan,
					persistenceAffectedRows.Value,
					persistenceException
				)
				: null;
		var persistenceDecision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(callbackPlan, persistenceResult);
		var packetPlan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(persistenceDecision, kinahTemplate);
		var callbackComposition = BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			persistenceDecision,
			packetPlan,
			runtimeResult
		);

		BindPointTeleportKinahInventorySendAdapterPlan? sendAdapterPlan = null;
		BindPointTeleportKinahInventorySendDecision? sendDecision = null;
		if (persistenceDecision.Status == BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence)
		{
			sendAdapterPlan =
				useDisabledSendAdapter ? BindPointTeleportKinahInventorySendAdapterPlanService.CreateDisabledPlan(packetPlan, ownerResult.PlayerObjectId)
				: suppliedSendResult == null ? null
				: CreateSuppliedSendAdapterPlan(packetPlan, suppliedSendResult);
			sendDecision = BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
				callbackComposition,
				sendAdapterPlan?.SendResult ?? suppliedSendResult
			);
		}

		var ownerRollbackPlan = BindPointTeleportKinahOwnerRollbackPlanService.CreatePlan(
			CreateOriginalPlayerSnapshot(ownerResult),
			ownerBridge.MutationPlan,
			persistenceDecision,
			sendDecision
		);
		var outcomePlan = BindPointTeleportKinahCallbackOutcomePlanService.CreatePlan(
			ownerBridge.MutationPlan,
			ownerBridge.PersistenceOperationPlan,
			persistenceDecision,
			sendAdapterPlan,
			sendDecision,
			ownerRollbackPlan
		);
		var rollbackResult = outcomePlan.ShouldRollbackInMemoryMutation ? _ownerService.RollbackScheduledDecrease(player, ownerResult) : null;

		return new BindPointTeleportKinahOwnerCallbackOutcomeIntegrationPlan(
			ownerResult,
			ownerBridge,
			callbackPlan,
			persistenceResult,
			persistenceDecision,
			packetPlan,
			callbackComposition,
			sendAdapterPlan,
			sendDecision,
			ownerRollbackPlan,
			rollbackResult,
			outcomePlan,
			DidRollbackOwnerMutation: rollbackResult?.RestoredOriginalKinah == true,
			DidCommitOwnerMutation: outcomePlan.ShouldCommitInMemoryMutation,
			outcomePlan.JavaSource,
			IsLive: false
		);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(
		BindPointTeleportKinahInventoryOwnerCallbackBridgePlan ownerBridge,
		BindPointTeleportCooldownPlan cooldownPlan,
		BindPointTeleportFanoutPlan cooldownFanoutPlan,
		BindPointTeleportFinalMovementPlan finalMovementPlan,
		BindPointTeleportTeleportToSideEffectPlan? teleportSideEffectPlan
	)
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			ownerBridge.MutationPlan.RequiredPrice,
			ownerBridge.MutationPlan.CurrentKinah
		);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			cooldownFanoutPlan,
			finalMovementPlan,
			teleportSideEffectPlan,
			ownerBridge.MutationPlan
		);
	}

	private static BindPointTeleportKinahInventorySendAdapterPlan CreateSuppliedSendAdapterPlan(
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		BindPointTeleportKinahInventorySendResult suppliedSendResult
	)
	{
		return new BindPointTeleportKinahInventorySendAdapterPlan(
			BindPointTeleportKinahInventorySendAdapterStatus.DisabledNoSend,
			packetPlan,
			suppliedSendResult,
			WouldCallSendPacketAsync: packetPlan.ShouldSendPacket,
			DidCallSendPacketAsync: false,
			"Supplied non-live send-result metadata for owner callback outcome integration",
			IsLive: false
		);
	}

	private static Player CreateOriginalPlayerSnapshot(BindPointTeleportKinahInventoryOwnerMutationResult ownerResult)
	{
		return new Player { ObjectId = ownerResult.PlayerObjectId, InventoryItems = ownerResult.InventoryBeforeMutation.ToArray() };
	}
}
