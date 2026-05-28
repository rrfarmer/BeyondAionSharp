namespace Aion.GameServer.Services;

public static class ItemExtractionLiveMutationBoundaryPlanService
{
	public const string JavaExtractActionSource =
		"game-server/src/com/aionemu/gameserver/model/templates/item/actions/ExtractAction.java";

	public const string JavaEnchantServiceSource =
		"game-server/src/com/aionemu/gameserver/services/EnchantService.java#breakItem";

	public const string CSharpPlannerSource =
		"dotnetConversion/src/Aion.GameServer/Services/EnchantService.cs#CreateBreakItemPlan";

	public static ItemExtractionLiveMutationBoundaryPlan CreatePlan()
	{
		return new ItemExtractionLiveMutationBoundaryPlan(
			RuntimeParityReady: false,
			JavaExtractActionSource,
			JavaEnchantServiceSource,
			CSharpPlannerSource,
			[
				new ItemExtractionLiveMutationBoundaryStep(
					ItemExtractionLiveMutationBoundaryStepKind.InitialInventoryPresenceGuard,
					"Java breakItem returns false when either the target item or extraction tool is absent from inventory."),
				new ItemExtractionLiveMutationBoundaryStep(
					ItemExtractionLiveMutationBoundaryStepKind.CompatibilityGuard,
					"Java rejects non-armor and non-weapon targets before any storage mutation."),
				new ItemExtractionLiveMutationBoundaryStep(
					ItemExtractionLiveMutationBoundaryStepKind.RewardStoneSelection,
					"Java selects the extraction stone id and count before deleting the target item."),
				new ItemExtractionLiveMutationBoundaryStep(
					ItemExtractionLiveMutationBoundaryStepKind.DeleteTarget,
					"Java calls Storage.delete(targetItem) first, gates tool decrease on a non-null delete result, but still returns true after this branch."),
				new ItemExtractionLiveMutationBoundaryStep(
					ItemExtractionLiveMutationBoundaryStepKind.DecreaseTool,
					"Java calls Storage.decreaseByObjectId(parentItem.getObjectId(), 1) after target deletion."),
				new ItemExtractionLiveMutationBoundaryStep(
					ItemExtractionLiveMutationBoundaryStepKind.AddRewardWhenToolDecreaseSucceeds,
					"Java only calls ItemService.addItem for the reward when the extraction tool decrease returns true."),
				new ItemExtractionLiveMutationBoundaryStep(
					ItemExtractionLiveMutationBoundaryStepKind.FinalAnimationResult,
					"ExtractAction sends the success final animation when breakItem returns true, including target-delete-failed and target-deleted/tool-decrease-failed paths.")
			],
			[
				"Live Storage.delete(targetItem) result must be observable before attempting Storage.decreaseByObjectId(parentItem, 1).",
				"Live Storage.delete(targetItem) failure must be representable as a success-animation path after the initial presence/type guards.",
				"Live Storage.decreaseByObjectId(parentItem, 1) failure must be representable after target deletion without rolling back the target delete.",
				"SM_ITEM_USAGE_ANIMATION final result must distinguish Java breakItem false from Java target-deleted/tool-decrease-failed success."
			],
			[
				"Current C# CreateBreakItemPlan snapshots inventory and applies target deletion and tool consumption in one in-memory plan.",
				"Current C# plan cannot model a delete failure that still produces Java's final success animation.",
				"Current C# plan cannot model a concurrent or persistence-layer tool decrease failure after target deletion succeeds.",
				"Runtime packet parity for the partial mutation path still needs a Java replay/golden trace or deterministic storage-boundary test."
			]);
	}
}

public sealed record ItemExtractionLiveMutationBoundaryPlan(
	bool RuntimeParityReady,
	string JavaExtractActionSource,
	string JavaEnchantServiceSource,
	string CSharpPlannerSource,
	IReadOnlyList<ItemExtractionLiveMutationBoundaryStep> JavaMutationOrder,
	IReadOnlyList<string> MissingRuntimeBoundaries,
	IReadOnlyList<string> KnownCSharpLimitations)
{
	public bool RequiresLiveStorageMutationBoundary => MissingRuntimeBoundaries.Count > 0;
}

public sealed record ItemExtractionLiveMutationBoundaryStep(
	ItemExtractionLiveMutationBoundaryStepKind Kind,
	string Description);

public enum ItemExtractionLiveMutationBoundaryStepKind
{
	InitialInventoryPresenceGuard,
	CompatibilityGuard,
	RewardStoneSelection,
	DeleteTarget,
	DecreaseTool,
	AddRewardWhenToolDecreaseSucceeds,
	FinalAnimationResult,
}
