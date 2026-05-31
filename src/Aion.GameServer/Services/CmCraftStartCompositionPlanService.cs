using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum CmCraftStartCompositionPlanStatus
{
	MissingRuntimePlan,
	MissingCraftService,
	RuntimeBlocked,
	MissingStartIntent,
	ValidationFailed,
	ReadyForDpSpendAndTaskStart,
	ReadyWithPlannerGaps,
}

public enum CmCraftStartCompositionPlanStep
{
	UseRuntimeGuardPlan,
	CreateStartValidationPlan,
	CreateCancelPacketPlan,
	CreateFailureOrchestrationPlan,
	CreateConsumptionPlan,
	CreateInventoryMutationPlan,
	CreateInventoryPersistencePlan,
	CreateInventoryPacketPlan,
	CreateTaskPlan,
}

public enum CraftStartSideEffectBoundaryStatus
{
	NotPlanned,
	ValidationFailed,
	PlannerGaps,
	Planned,
}

public enum CraftStartSideEffectBoundaryStep
{
	ApplyCheckCraftInventoryMutation,
	SendCheckCraftInventoryPackets,
	SpendRecipeDp,
	CreateCraftingTask,
	StartCraftingTask,
}

public sealed record CraftStartSideEffectBoundaryPlan(
	CraftStartSideEffectBoundaryStatus Status,
	CraftStartInventoryMutationPlan? InventoryMutationPlan,
	CraftStartInventoryPacketPlan? InventoryPacketPlan,
	CraftStartTaskPlan? TaskPlan,
	IReadOnlyList<CraftStartSideEffectBoundaryStep> Steps,
	bool RequiresDpSpend,
	int RequiredDp,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive)
{
	public bool IsPlanned => Status == CraftStartSideEffectBoundaryStatus.Planned;
}

public enum CraftStartLiveExecutorFacadeStatus
{
	MissingCompositionPlan,
	CompositionNotReady,
	DisabledNoSideEffects,
}

public enum CraftStartLiveExecutorOperationKind
{
	ApplyInventoryMutation,
	MarkInventoryPersistenceState,
	SendInventoryPackets,
	SpendRecipeDp,
	CreateCraftingTask,
	StartCraftingTask,
}

public enum CraftStartLiveExecutorOperationStatus
{
	NotAttemptedMissingPlan,
	NotAttemptedCompositionNotReady,
	NotAttemptedDisabled,
}

public sealed record CraftStartLiveExecutorOperation(
	CraftStartLiveExecutorOperationKind Kind,
	CraftStartLiveExecutorOperationStatus Status,
	string JavaSource);

public sealed record CraftStartLiveExecutorFacadePlan(
	CraftStartLiveExecutorFacadeStatus Status,
	CmCraftStartCompositionPlan? CompositionPlan,
	IReadOnlyList<CraftStartLiveExecutorOperation> Operations,
	CraftStartInventoryPersistenceAdapterPlan? InventoryPersistenceAdapterPlan,
	CraftStartInventoryPacketSendAdapterPlan? InventoryPacketSendAdapterPlan,
	bool WouldMutateInventory,
	bool DidMutateInventory,
	bool WouldWriteInventoryPersistence,
	bool DidWriteInventoryPersistence,
	bool WouldSendInventoryPackets,
	bool DidSendInventoryPackets,
	bool WouldSpendDp,
	bool DidSpendDp,
	bool WouldCreateCraftingTask,
	bool DidCreateCraftingTask,
	bool WouldStartCraftingTask,
	bool DidStartCraftingTask,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive)
{
	public bool IsDisabled => Status == CraftStartLiveExecutorFacadeStatus.DisabledNoSideEffects;
}

public sealed record CmCraftStartCompositionPlan(
	CmCraftStartCompositionPlanStatus Status,
	CmCraftRuntimePlan? RuntimePlan,
	CraftStartValidationPlan? ValidationPlan,
	CraftStartCancelPacketPlan? CancelPacketPlan,
	CraftStartFailureOrchestrationPlan? FailurePlan,
	CraftStartConsumptionPlan? ConsumptionPlan,
	CraftStartInventoryMutationPlan? InventoryMutationPlan,
	CraftStartInventoryPersistencePlan? InventoryPersistencePlan,
	CraftStartInventoryPacketPlan? InventoryPacketPlan,
	CraftStartTaskPlan? TaskPlan,
	CraftStartSideEffectBoundaryPlan SideEffectBoundaryPlan,
	IReadOnlyList<CmCraftStartCompositionPlanStep> Steps,
	bool RequiresDpSpend,
	int RequiredDp,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public static class CmCraftStartCompositionPlanService
{
	public static CmCraftStartCompositionPlan CreatePlan(
		CmCraftRuntimePlan? runtimePlan,
		CraftService? craftService,
		Player? player,
		RecipeTemplateSummary? recipeTemplate,
		ItemTemplateSummary? productTemplate,
		IWorldNpcObject? target,
		bool targetIsStaticObject,
		bool targetIsWithinToolRange,
		bool hasCraftingTaskInProgress)
	{
		// Java parity: CM_CRAFT.runImpl forwards parsed recipeId, targetObjId, craftType,
		// and materialsData into CraftService.startCrafting after the runtime guard shell.
		// This bridge composes existing non-live planners only; it does not spend DP,
		// mutate inventory, send packets, create CraftingTask, or start scheduler work.
		if (runtimePlan == null)
			return CreateTerminalPlan(CmCraftStartCompositionPlanStatus.MissingRuntimePlan, null, "CM_CRAFT adapter requires the runtime guard plan");
		if (craftService == null)
			return CreateTerminalPlan(CmCraftStartCompositionPlanStatus.MissingCraftService, runtimePlan, "CM_CRAFT adapter requires CraftService planner access");
		if (runtimePlan.Status != CmCraftRuntimePlanStatus.StartCrafting)
		{
			return new CmCraftStartCompositionPlan(
				CmCraftStartCompositionPlanStatus.RuntimeBlocked,
				runtimePlan,
				ValidationPlan: null,
				CancelPacketPlan: null,
				FailurePlan: null,
				ConsumptionPlan: null,
				InventoryMutationPlan: null,
				InventoryPersistencePlan: null,
				InventoryPacketPlan: null,
				TaskPlan: null,
				CreateSideEffectBoundaryPlan(
					CmCraftStartCompositionPlanStatus.RuntimeBlocked,
					inventoryMutationPlan: null,
					inventoryPacketPlan: null,
					taskPlan: null,
					requiresDpSpend: false,
					requiredDp: 0),
				[CmCraftStartCompositionPlanStep.UseRuntimeGuardPlan],
				RequiresDpSpend: false,
				RequiredDp: 0,
				ShouldDispatchLiveSideEffects: false,
				runtimePlan.JavaSource,
				IsLive: false);
		}

		var startIntent = runtimePlan.StartIntent;
		if (startIntent == null)
			return CreateTerminalPlan(CmCraftStartCompositionPlanStatus.MissingStartIntent, runtimePlan, "CM_CRAFT adapter requires StartCrafting intent values");

		var steps = new List<CmCraftStartCompositionPlanStep>
		{
			CmCraftStartCompositionPlanStep.UseRuntimeGuardPlan,
			CmCraftStartCompositionPlanStep.CreateStartValidationPlan,
		};
		var validationPlan = craftService.CreateStartCraftingValidationPlan(
			player,
			recipeTemplate,
			productTemplate,
			target,
			targetIsStaticObject,
			targetIsWithinToolRange,
			hasCraftingTaskInProgress,
			startIntent.MaterialsData,
			startIntent.CraftType);

		if (!validationPlan.IsReadyForNextValidation)
		{
			steps.Add(CmCraftStartCompositionPlanStep.CreateCancelPacketPlan);
			var cancelPlan = craftService.CreateStartCancelPacketPlan(
				player,
				recipeTemplate,
				productTemplate,
				startIntent.TargetObjectId);
			steps.Add(CmCraftStartCompositionPlanStep.CreateFailureOrchestrationPlan);
			var failurePlan = craftService.CreateStartFailureOrchestrationPlan(validationPlan, cancelPlan);

			return new CmCraftStartCompositionPlan(
				CmCraftStartCompositionPlanStatus.ValidationFailed,
				runtimePlan,
				validationPlan,
				cancelPlan,
				failurePlan,
				ConsumptionPlan: null,
				InventoryMutationPlan: null,
				InventoryPersistencePlan: null,
				InventoryPacketPlan: null,
				TaskPlan: null,
				CreateSideEffectBoundaryPlan(
					CmCraftStartCompositionPlanStatus.ValidationFailed,
					inventoryMutationPlan: null,
					inventoryPacketPlan: null,
					taskPlan: null,
					requiresDpSpend: false,
					requiredDp: 0),
				steps,
				RequiresDpSpend: false,
				RequiredDp: 0,
				ShouldDispatchLiveSideEffects: false,
				"CM_CRAFT.runImpl -> CraftService.startCrafting -> checkCraft false, then sendCancelCraft",
				IsLive: false);
		}

		steps.Add(CmCraftStartCompositionPlanStep.CreateConsumptionPlan);
		var consumptionPlan = craftService.CreateStartConsumptionPlan(
			validationPlan,
			recipeTemplate,
			startIntent.MaterialsData,
			startIntent.CraftType);
		steps.Add(CmCraftStartCompositionPlanStep.CreateInventoryMutationPlan);
		var inventoryMutationPlan = craftService.CreateStartInventoryMutationPlan(
			consumptionPlan,
			player?.InventoryItems);
		steps.Add(CmCraftStartCompositionPlanStep.CreateInventoryPersistencePlan);
		var inventoryPersistencePlan = craftService.CreateStartInventoryPersistencePlan(inventoryMutationPlan);
		steps.Add(CmCraftStartCompositionPlanStep.CreateInventoryPacketPlan);
		var inventoryPacketPlan = craftService.CreateStartInventoryPacketPlan(inventoryMutationPlan, player);
		steps.Add(CmCraftStartCompositionPlanStep.CreateTaskPlan);
		var taskPlan = craftService.CreateStartTaskPlan(
			validationPlan,
			productTemplate,
			startIntent.CraftType);
		var isReady = consumptionPlan.Status == CraftStartConsumptionStatus.Planned
			&& inventoryMutationPlan.Status == CraftStartInventoryMutationStatus.Planned
			&& inventoryPersistencePlan.Status == CraftStartInventoryPersistenceStatus.Planned
			&& inventoryPacketPlan.Status == CraftStartInventoryPacketStatus.Planned
			&& taskPlan.Status == CraftStartTaskPlanStatus.Planned;
		var planStatus = isReady
			? CmCraftStartCompositionPlanStatus.ReadyForDpSpendAndTaskStart
			: CmCraftStartCompositionPlanStatus.ReadyWithPlannerGaps;
		var requiresDpSpend = recipeTemplate?.Dp > 0;
		var requiredDp = recipeTemplate?.Dp ?? 0;

		return new CmCraftStartCompositionPlan(
			planStatus,
			runtimePlan,
			validationPlan,
			CancelPacketPlan: null,
			FailurePlan: null,
			consumptionPlan,
			inventoryMutationPlan,
			inventoryPersistencePlan,
			inventoryPacketPlan,
			taskPlan,
			CreateSideEffectBoundaryPlan(
				planStatus,
				inventoryMutationPlan,
				inventoryPacketPlan,
				taskPlan,
				requiresDpSpend,
				requiredDp),
			steps,
			RequiresDpSpend: requiresDpSpend,
			RequiredDp: requiredDp,
			ShouldDispatchLiveSideEffects: false,
			"CM_CRAFT.runImpl -> CraftService.startCrafting successful checkCraft path composed through consumption/task planners",
			IsLive: false);
	}

	private static CmCraftStartCompositionPlan CreateTerminalPlan(
		CmCraftStartCompositionPlanStatus status,
		CmCraftRuntimePlan? runtimePlan,
		string javaSource)
	{
		return new CmCraftStartCompositionPlan(
			status,
			runtimePlan,
			ValidationPlan: null,
			CancelPacketPlan: null,
			FailurePlan: null,
			ConsumptionPlan: null,
			InventoryMutationPlan: null,
			InventoryPersistencePlan: null,
			InventoryPacketPlan: null,
			TaskPlan: null,
			CreateSideEffectBoundaryPlan(
				status,
				inventoryMutationPlan: null,
				inventoryPacketPlan: null,
				taskPlan: null,
				requiresDpSpend: false,
				requiredDp: 0),
			runtimePlan == null ? [] : [CmCraftStartCompositionPlanStep.UseRuntimeGuardPlan],
			RequiresDpSpend: false,
			RequiredDp: 0,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);
	}

	private static CraftStartSideEffectBoundaryPlan CreateSideEffectBoundaryPlan(
		CmCraftStartCompositionPlanStatus status,
		CraftStartInventoryMutationPlan? inventoryMutationPlan,
		CraftStartInventoryPacketPlan? inventoryPacketPlan,
		CraftStartTaskPlan? taskPlan,
		bool requiresDpSpend,
		int requiredDp)
	{
		// Java parity: CraftService.checkCraft performs inventory decrease packet side effects
		// before startCrafting spends recipe DP, creates CraftingTask, and starts it.
		if (status == CmCraftStartCompositionPlanStatus.ValidationFailed)
		{
			return new CraftStartSideEffectBoundaryPlan(
				CraftStartSideEffectBoundaryStatus.ValidationFailed,
				inventoryMutationPlan,
				inventoryPacketPlan,
				taskPlan,
				[],
				RequiresDpSpend: false,
				RequiredDp: 0,
				ShouldDispatchLiveSideEffects: false,
				"CraftService.startCrafting stops after checkCraft false and sendCancelCraft; no success side effects are planned",
				IsLive: false);
		}

		if (status != CmCraftStartCompositionPlanStatus.ReadyForDpSpendAndTaskStart)
		{
			return new CraftStartSideEffectBoundaryPlan(
				status == CmCraftStartCompositionPlanStatus.ReadyWithPlannerGaps
					? CraftStartSideEffectBoundaryStatus.PlannerGaps
					: CraftStartSideEffectBoundaryStatus.NotPlanned,
				inventoryMutationPlan,
				inventoryPacketPlan,
				taskPlan,
				[],
				requiresDpSpend,
				requiredDp,
				ShouldDispatchLiveSideEffects: false,
				"CraftService.startCrafting success boundary requires planned inventory mutation, packet intent, and task intent",
				IsLive: false);
		}

		var steps = new List<CraftStartSideEffectBoundaryStep>
		{
			CraftStartSideEffectBoundaryStep.ApplyCheckCraftInventoryMutation,
			CraftStartSideEffectBoundaryStep.SendCheckCraftInventoryPackets,
		};
		if (requiresDpSpend)
			steps.Add(CraftStartSideEffectBoundaryStep.SpendRecipeDp);
		steps.Add(CraftStartSideEffectBoundaryStep.CreateCraftingTask);
		steps.Add(CraftStartSideEffectBoundaryStep.StartCraftingTask);

		return new CraftStartSideEffectBoundaryPlan(
			CraftStartSideEffectBoundaryStatus.Planned,
			inventoryMutationPlan,
			inventoryPacketPlan,
			taskPlan,
			steps,
			requiresDpSpend,
			requiredDp,
			ShouldDispatchLiveSideEffects: false,
			"CraftService.checkCraft successful inventory side effects -> startCrafting DP spend -> set CraftingTask -> CraftingTask.start",
			IsLive: false);
	}
}

public static class CraftStartLiveExecutorFacadePlanService
{
	public static CraftStartLiveExecutorFacadePlan CreateDisabledPlan(CmCraftStartCompositionPlan? compositionPlan)
	{
		// Java parity: CraftService.startCrafting live side effects remain behind a disabled
		// C# boundary until mutation, persistence, packet send, DP, and CraftingTask wiring
		// are each explicitly enabled and verified.
		if (compositionPlan == null)
		{
			return new CraftStartLiveExecutorFacadePlan(
				CraftStartLiveExecutorFacadeStatus.MissingCompositionPlan,
				CompositionPlan: null,
				[NotAttempted(CraftStartLiveExecutorOperationKind.ApplyInventoryMutation, CraftStartLiveExecutorOperationStatus.NotAttemptedMissingPlan)],
				InventoryPersistenceAdapterPlan: null,
				InventoryPacketSendAdapterPlan: null,
				WouldMutateInventory: false,
				DidMutateInventory: false,
				WouldWriteInventoryPersistence: false,
				DidWriteInventoryPersistence: false,
				WouldSendInventoryPackets: false,
				DidSendInventoryPackets: false,
				WouldSpendDp: false,
				DidSpendDp: false,
				WouldCreateCraftingTask: false,
				DidCreateCraftingTask: false,
				WouldStartCraftingTask: false,
				DidStartCraftingTask: false,
				ShouldDispatchLiveSideEffects: false,
				"CraftService.startCrafting live facade requires CM_CRAFT composition evidence before any side effects can be considered",
				IsLive: false);
		}

		if (compositionPlan.Status != CmCraftStartCompositionPlanStatus.ReadyForDpSpendAndTaskStart
			|| !compositionPlan.SideEffectBoundaryPlan.IsPlanned)
		{
			return new CraftStartLiveExecutorFacadePlan(
				CraftStartLiveExecutorFacadeStatus.CompositionNotReady,
				compositionPlan,
				[NotAttempted(CraftStartLiveExecutorOperationKind.ApplyInventoryMutation, CraftStartLiveExecutorOperationStatus.NotAttemptedCompositionNotReady)],
				InventoryPersistenceAdapterPlan: null,
				InventoryPacketSendAdapterPlan: null,
				WouldMutateInventory: false,
				DidMutateInventory: false,
				WouldWriteInventoryPersistence: false,
				DidWriteInventoryPersistence: false,
				WouldSendInventoryPackets: false,
				DidSendInventoryPackets: false,
				WouldSpendDp: false,
				DidSpendDp: false,
				WouldCreateCraftingTask: false,
				DidCreateCraftingTask: false,
				WouldStartCraftingTask: false,
				DidStartCraftingTask: false,
				ShouldDispatchLiveSideEffects: false,
				"CraftService.startCrafting live facade stops before success side effects when CM_CRAFT composition is not ready",
				IsLive: false);
		}

		var operations = new List<CraftStartLiveExecutorOperation>();
		var persistenceAdapterPlan = CraftStartInventoryPersistenceAdapterPlanService.CreateDisabledPlan(compositionPlan.InventoryPersistencePlan);
		var packetSendAdapterPlan = CraftStartInventoryPacketSendAdapterPlanService.CreateDisabledPlan(compositionPlan.InventoryPacketPlan, compositionPlan.ValidationPlan?.ObjectId ?? 0);
		if (compositionPlan.InventoryMutationPlan?.IsPlanned == true)
			operations.Add(Disabled(CraftStartLiveExecutorOperationKind.ApplyInventoryMutation, "CraftService.checkCraft -> Storage.decreaseByItemId mutates consumed item stacks"));
		if (persistenceAdapterPlan.WouldExecuteSql)
			operations.Add(Disabled(CraftStartLiveExecutorOperationKind.MarkInventoryPersistenceState, "Storage.decreaseItemCount/delete dirty state -> InventoryDAO.store disabled adapter"));
		if (packetSendAdapterPlan.WouldCallSendPacketAsync)
			operations.Add(Disabled(CraftStartLiveExecutorOperationKind.SendInventoryPackets, "Storage.decreaseItemCount/delete -> ItemPacketService disabled send adapter"));
		if (compositionPlan.RequiresDpSpend)
			operations.Add(Disabled(CraftStartLiveExecutorOperationKind.SpendRecipeDp, "CraftService.startCrafting -> player.getCommonData().addDp(-recipeTemplate.getDp())"));
		if (compositionPlan.TaskPlan?.IsPlanned == true)
		{
			operations.Add(Disabled(CraftStartLiveExecutorOperationKind.CreateCraftingTask, "CraftService.startCrafting -> player.setCraftingTask(new CraftingTask(...))"));
			operations.Add(Disabled(CraftStartLiveExecutorOperationKind.StartCraftingTask, "CraftService.startCrafting -> player.getCraftingTask().start()"));
		}

		return new CraftStartLiveExecutorFacadePlan(
			CraftStartLiveExecutorFacadeStatus.DisabledNoSideEffects,
			compositionPlan,
			operations,
			persistenceAdapterPlan,
			packetSendAdapterPlan,
			WouldMutateInventory: compositionPlan.InventoryMutationPlan?.IsPlanned == true,
			DidMutateInventory: false,
			WouldWriteInventoryPersistence: persistenceAdapterPlan.WouldExecuteSql,
			DidWriteInventoryPersistence: persistenceAdapterPlan.DidExecuteSql,
			WouldSendInventoryPackets: packetSendAdapterPlan.WouldCallSendPacketAsync,
			DidSendInventoryPackets: packetSendAdapterPlan.DidCallSendPacketAsync,
			WouldSpendDp: compositionPlan.RequiresDpSpend,
			DidSpendDp: false,
			WouldCreateCraftingTask: compositionPlan.TaskPlan?.IsPlanned == true,
			DidCreateCraftingTask: false,
			WouldStartCraftingTask: compositionPlan.TaskPlan?.IsPlanned == true,
			DidStartCraftingTask: false,
			ShouldDispatchLiveSideEffects: false,
			"CraftService.startCrafting live side-effect executor facade is disabled; Java side-effect order is recorded without dispatch",
			IsLive: false);
	}

	private static CraftStartLiveExecutorOperation Disabled(CraftStartLiveExecutorOperationKind kind, string javaSource)
	{
		return new CraftStartLiveExecutorOperation(
			kind,
			CraftStartLiveExecutorOperationStatus.NotAttemptedDisabled,
			javaSource);
	}

	private static CraftStartLiveExecutorOperation NotAttempted(
		CraftStartLiveExecutorOperationKind kind,
		CraftStartLiveExecutorOperationStatus status)
	{
		return new CraftStartLiveExecutorOperation(
			kind,
			status,
			"CraftService.startCrafting live executor facade did not reach this side-effect boundary");
	}
}
