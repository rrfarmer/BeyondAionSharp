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
	CreateInventoryPacketPlan,
	CreateTaskPlan,
}

public sealed record CmCraftStartCompositionPlan(
	CmCraftStartCompositionPlanStatus Status,
	CmCraftRuntimePlan? RuntimePlan,
	CraftStartValidationPlan? ValidationPlan,
	CraftStartCancelPacketPlan? CancelPacketPlan,
	CraftStartFailureOrchestrationPlan? FailurePlan,
	CraftStartConsumptionPlan? ConsumptionPlan,
	CraftStartInventoryMutationPlan? InventoryMutationPlan,
	CraftStartInventoryPacketPlan? InventoryPacketPlan,
	CraftStartTaskPlan? TaskPlan,
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
				InventoryPacketPlan: null,
				TaskPlan: null,
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
				InventoryPacketPlan: null,
				TaskPlan: null,
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
		steps.Add(CmCraftStartCompositionPlanStep.CreateInventoryPacketPlan);
		var inventoryPacketPlan = craftService.CreateStartInventoryPacketPlan(inventoryMutationPlan, player);
		steps.Add(CmCraftStartCompositionPlanStep.CreateTaskPlan);
		var taskPlan = craftService.CreateStartTaskPlan(
			validationPlan,
			productTemplate,
			startIntent.CraftType);
		var isReady = consumptionPlan.Status == CraftStartConsumptionStatus.Planned
			&& inventoryMutationPlan.Status == CraftStartInventoryMutationStatus.Planned
			&& inventoryPacketPlan.Status == CraftStartInventoryPacketStatus.Planned
			&& taskPlan.Status == CraftStartTaskPlanStatus.Planned;

		return new CmCraftStartCompositionPlan(
			isReady
				? CmCraftStartCompositionPlanStatus.ReadyForDpSpendAndTaskStart
				: CmCraftStartCompositionPlanStatus.ReadyWithPlannerGaps,
			runtimePlan,
			validationPlan,
			CancelPacketPlan: null,
			FailurePlan: null,
			consumptionPlan,
			inventoryMutationPlan,
			inventoryPacketPlan,
			taskPlan,
			steps,
			RequiresDpSpend: recipeTemplate?.Dp > 0,
			RequiredDp: recipeTemplate?.Dp ?? 0,
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
			InventoryPacketPlan: null,
			TaskPlan: null,
			runtimePlan == null ? [] : [CmCraftStartCompositionPlanStep.UseRuntimeGuardPlan],
			RequiresDpSpend: false,
			RequiredDp: 0,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);
	}
}
