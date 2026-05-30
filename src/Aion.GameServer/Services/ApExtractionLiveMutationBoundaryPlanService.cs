namespace Aion.GameServer.Services;

public static class ApExtractionLiveMutationBoundaryPlanService
{
	// Java parity: model/templates/item/actions/ApExtractAction.act and services/abyss/AbyssPointsService.addAp
	// define a live mutation boundary where target delete, tool decrease, and AP gain happen inline.
	public const string JavaApExtractActionSource = "game-server/src/com/aionemu/gameserver/model/templates/item/actions/ApExtractAction.java";

	public const string JavaAbyssPointsServiceSource = "game-server/src/com/aionemu/gameserver/services/abyss/AbyssPointsService.java#addAp";

	public const string CSharpPlannerSource = "dotnetConversion/src/Aion.GameServer/Services/ApExtractService.cs#CreateMutationPlan";

	public static ApExtractionLiveMutationBoundaryPlan CreatePlan()
	{
		// Java parity: this plan documents the Java live mutation order and the still-missing C# runtime
		// boundaries needed to reproduce partial-success and packet-order behavior.
		return new ApExtractionLiveMutationBoundaryPlan(
			RuntimeParityReady: false,
			JavaApExtractActionSource,
			JavaAbyssPointsServiceSource,
			CSharpPlannerSource,
			[
				new ApExtractionLiveMutationBoundaryStep(
					ApExtractionLiveMutationBoundaryStepKind.InitialCanActGuards,
					"Java canAct rejects null/non-AP targets, low-level tools, quality mismatches, unsupported target groups, and target/action mismatches before act."
				),
				new ApExtractionLiveMutationBoundaryStep(
					ApExtractionLiveMutationBoundaryStepKind.AcquisitionGuard,
					"Java act returns silently when acquisition metadata is absent or required AP is zero."
				),
				new ApExtractionLiveMutationBoundaryStep(
					ApExtractionLiveMutationBoundaryStepKind.ApAmountCalculation,
					"Java calculates AP as (int) (requiredAp * rate) before inventory mutation."
				),
				new ApExtractionLiveMutationBoundaryStep(
					ApExtractionLiveMutationBoundaryStepKind.DeleteTarget,
					"Java calls Storage.delete(targetItem) first and stops after an audit log when it returns null."
				),
				new ApExtractionLiveMutationBoundaryStep(
					ApExtractionLiveMutationBoundaryStepKind.DecreaseTool,
					"Java calls Storage.decreaseByObjectId(parentItem.getObjectId(), 1) only after target deletion succeeds."
				),
				new ApExtractionLiveMutationBoundaryStep(
					ApExtractionLiveMutationBoundaryStepKind.AddAbyssPoints,
					"Java calls AbyssPointsService.addAp(player, ap) only after the extraction tool decrease returns true."
				),
			],
			[
				"Live Storage.delete(targetItem) result must be observable before attempting Storage.decreaseByObjectId(parentItem, 1).",
				"Live Storage.decreaseByObjectId(parentItem, 1) failure must be representable after target deletion without rolling back the target delete.",
				"AbyssPointsService.addAp side effects must be ordered after target delete and tool decrease for Java live-order parity.",
				"Storage delete/update packets and AbyssPointsService AP/rank packets must be ordered as Java inline side effects.",
				"Silent Java returns from missing acquisition metadata and failed storage mutations need packet/no-packet confirmation.",
			],
			[
				"Current C# CreateMutationPlan creates the AbyssPointsService plan before target deletion and source-tool mutation.",
				"Current C# plan cannot model target deletion success followed by extraction-tool decrease failure.",
				"Current C# plan treats AP plan failure as a pre-mutation failure, while Java would reach AP only after item mutations succeed.",
				"Current C# handler sends and applies item/AP side effects only after the full plan and persistence transaction succeed.",
				"Runtime parity for partial item mutation plus AP packet/rank side effects still needs a Java replay/golden trace or deterministic live storage boundary.",
			]
		);
	}
}

public sealed record ApExtractionLiveMutationBoundaryPlan(
	bool RuntimeParityReady,
	string JavaApExtractActionSource,
	string JavaAbyssPointsServiceSource,
	string CSharpPlannerSource,
	IReadOnlyList<ApExtractionLiveMutationBoundaryStep> JavaMutationOrder,
	IReadOnlyList<string> MissingRuntimeBoundaries,
	IReadOnlyList<string> KnownCSharpLimitations
)
{
	public bool RequiresLiveStorageMutationBoundary => MissingRuntimeBoundaries.Count > 0;
}

public sealed record ApExtractionLiveMutationBoundaryStep(ApExtractionLiveMutationBoundaryStepKind Kind, string Description);

public enum ApExtractionLiveMutationBoundaryStepKind
{
	InitialCanActGuards,
	AcquisitionGuard,
	ApAmountCalculation,
	DeleteTarget,
	DecreaseTool,
	AddAbyssPoints,
}
