namespace Aion.GameServer.Services;

public static class DecomposeRewardOrderingPlanService
{
	public const string JavaDecomposeActionSource =
		"game-server/src/com/aionemu/gameserver/model/templates/item/actions/DecomposeAction.java";

	public const string CSharpHandlerSource =
		"dotnetConversion/src/Aion.GameServer/Network/Aion/GameServerConnection.cs#CompleteDecomposeUseItemAsync";

	public static DecomposeRewardOrderingPlan CreatePlan()
	{
		return new DecomposeRewardOrderingPlan(
			RuntimeParityReady: false,
			JavaDecomposeActionSource,
			CSharpHandlerSource,
			[
				new DecomposeRewardOrderingStep(
					DecomposeRewardOrderingStepKind.StartAnimation,
					"Java broadcasts SM_ITEM_USAGE_ANIMATION with the 3000ms delay before scheduling the delayed task."),
				new DecomposeRewardOrderingStep(
					DecomposeRewardOrderingStepKind.DelayedPostValidate,
					"Java removes the observer, re-runs canAct, and decreases the source item by object id during postValidate."),
				new DecomposeRewardOrderingStep(
					DecomposeRewardOrderingStepKind.SourceConsumePacket,
					"Java Storage.decreaseByObjectId emits the source item decrease or delete packet before the success message."),
				new DecomposeRewardOrderingStep(
					DecomposeRewardOrderingStepKind.SuccessMessage,
					"Java sends STR_DECOMPOSE_ITEM_SUCCEED immediately after postValidate succeeds and before adding rewards."),
				new DecomposeRewardOrderingStep(
					DecomposeRewardOrderingStepKind.RewardAddPackets,
					"Java calls ItemService.addItem for fixed rewards and then random rewards, using DECOMPOSABLE/INC_ITEM_COLLECT packet types."),
				new DecomposeRewardOrderingStep(
					DecomposeRewardOrderingStepKind.FinalAnimation,
					"Java broadcasts the final SM_ITEM_USAGE_ANIMATION with end state 1 after reward add attempts, or 2 after postValidate failure.")
			],
			[
				"Current C# tests cover normal fixed-reward source decrement/delete order, restricted reward add/merge flags, inventory-full precheck, and selectable decompose packet contracts.",
				"Current C# normal decompose sends source consume packets, then success message, then reward packets, then final success animation.",
				"Current C# plans reward inventory before persistence, so missing reward templates and add-plan failures prevent source consumption; Java reward add failures after success message still need runtime evidence.",
				"Selectable decompose follows CM_SELECT_DECOMPOSABLE rather than the delayed normal DecomposeAction path and has a separate packet order."
			],
			[
				"Java reward add return values are ignored after the success message; C# reward add planning is all-or-nothing before source mutation is persisted.",
				"Java random reward selection uses live DataManager item lookups and Rnd retries; C# source-reviewed deterministic helpers still lack Java runtime golden coverage for every random type.",
				"Java Storage and ItemService packet side effects are live and inline, while C# persists the complete mutation before sending packets.",
				"Java 25/Maven tooling is still required for generated decompose runtime packet artifacts."
			]);
	}
}

public sealed record DecomposeRewardOrderingPlan(
	bool RuntimeParityReady,
	string JavaDecomposeActionSource,
	string CSharpHandlerSource,
	IReadOnlyList<DecomposeRewardOrderingStep> JavaVisibleOrder,
	IReadOnlyList<string> CoveredCSharpBehaviors,
	IReadOnlyList<string> RemainingRisks)
{
	public bool RequiresRuntimeGoldenForFullParity => RemainingRisks.Count > 0;
}

public sealed record DecomposeRewardOrderingStep(
	DecomposeRewardOrderingStepKind Kind,
	string Description);

public enum DecomposeRewardOrderingStepKind
{
	StartAnimation,
	DelayedPostValidate,
	SourceConsumePacket,
	SuccessMessage,
	RewardAddPackets,
	FinalAnimation,
}
