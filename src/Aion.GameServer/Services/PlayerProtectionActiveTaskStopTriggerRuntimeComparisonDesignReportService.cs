namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario
{
	CmMoveThresholdBranches,
	CmMoveInAirAcceptedFlying,
	EarlyActionPacketStop,
	CmCompositeStonesInvalidAfterStop,
	CmEmotionLateStop,
	CmEmotionEarlyReturnNoStop,
}

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonStatus
{
	BlockedMissingJavaTraceArtifact,
	BlockedMissingLiveCSharpHook,
	ReadyForTraceDesignOnly,
}

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario Scenario,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonStatus Status,
	bool ExpectsStopProtectionCall,
	string JavaPacketSources,
	string ExpectedStopPosition,
	string ExpectedControllerObservables,
	string ExpectedPacketOrActionObservables,
	string RequiredJavaTraceArtifact,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow> Rows,
	bool HasMovementThresholdScenario,
	bool HasAirMovementScenario,
	bool HasEarlyActionScenario,
	bool HasInvalidAfterStopScenario,
	bool HasLateEmotionScenario,
	bool HasEmotionEarlyReturnScenario,
	bool RequiresJavaTraceArtifacts,
	bool RequiresLiveCSharpPacketHooks,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService
{
	public static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport Create(
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport summary)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow>();

		AddCmMoveThresholdBranches(rows, summary);
		AddCmMoveInAir(rows, summary);
		AddEarlyActionPacketStops(rows, summary);
		AddCompositeInvalidAfterStop(rows, summary);
		AddCmEmotionLateStop(rows, summary);
		AddCmEmotionEarlyReturn(rows, summary);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport(
			rowArray,
			HasMovementThresholdScenario: rowArray.Any(row => row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmMoveThresholdBranches),
			HasAirMovementScenario: rowArray.Any(row => row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmMoveInAirAcceptedFlying),
			HasEarlyActionScenario: rowArray.Any(row => row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.EarlyActionPacketStop),
			HasInvalidAfterStopScenario: rowArray.Any(row => row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmCompositeStonesInvalidAfterStop),
			HasLateEmotionScenario: rowArray.Any(row => row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmEmotionLateStop),
			HasEmotionEarlyReturnScenario: rowArray.Any(row => row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmEmotionEarlyReturnNoStop),
			RequiresJavaTraceArtifacts: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonStatus.BlockedMissingJavaTraceArtifact),
			RequiresLiveCSharpPacketHooks: !summary.ReadyForProductionPacketStopWiring,
			ReadyForRuntimeComparison: false,
			"Java packet stop trigger runtime comparison design",
			IsLive: false);
	}

	private static void AddCmMoveThresholdBranches(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport summary) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmMoveThresholdBranches,
			true,
			"CM_MOVE",
			"x/y exact-delta branches stop after accepted movement and spawned check; same-position and z-drop <= 0.5 skip; oldZ > packetZ + 0.5 stops.",
			CommonStopObservables(),
			"Trace accepted anti-hack movement, teleportation absolute-move early return, World.updatePosition ordering, and no-stop heading turn.",
			"Java trace artifact for CM_MOVE with x-delta, y-delta, z-drop 0.5, z-drop >0.5, same-position turn, anti-hack reject, not-spawned, and teleportation-mode absolute movement.",
			$"Summary threshold evidence={summary.HasThresholdedMovementSource}; exact float inequality and asymmetric z behavior must be captured.");

	private static void AddCmMoveInAir(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport summary) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmMoveInAirAcceptedFlying,
			true,
			"CM_MOVE_IN_AIR",
			"Spawned + flying + protection-active stops before World.updatePosition, onMoveFromClient, and onMove; not-spawned/not-flying skip.",
			CommonStopObservables(),
			"Trace flight-path distance update before stop when present, then world-position and movement callbacks after stop.",
			"Java trace artifact for spawned/flying/protected, not spawned, not flying, and inactive-protection CM_MOVE_IN_AIR branches.",
			$"Summary air movement evidence={summary.Rows.Any(row => row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.AcceptedAirMovement)}.");

	private static void AddEarlyActionPacketStops(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport summary) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.EarlyActionPacketStop,
			true,
			"CM_ATTACK, CM_CASTSPELL, CM_USE_ITEM, CM_SHOW_DIALOG, CM_DIALOG_SELECT",
			"Stop occurs before each packet's core lookup/validation/action side effects after packet-specific pre-stop guards.",
			CommonStopObservables(),
			"Trace attack known-list lookup, cast cancelUseItem, use-item source lookup, show-dialog trading/NPC lookup, and dialog-select trading/action lookup after stop.",
			"Java trace artifact for active/inactive protection plus representative pre-stop guard skips for attack, cast, use-item, show-dialog, and dialog-select.",
			$"Summary early action evidence={summary.HasEarlyActionStopSources}.");

	private static void AddCompositeInvalidAfterStop(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport summary) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmCompositeStonesInvalidAfterStop,
			true,
			"CM_COMPOSITE_STONES",
			"Stop occurs after null-player guard and before casting cancellation, inventory lookup, restrictions, canAct, and scheduling; later invalid branches still occur after stop.",
			CommonStopObservables(),
			"Trace missing tool/first/second item, PlayerRestrictions failure, CompositionAction.canAct failure, and successful scheduling after the early stop site.",
			"Java trace artifact for null player, active-protection invalid-after-stop branches, inactive protection, and successful composition scheduling.",
			$"Summary composition category present={summary.Rows.Any(row => row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.EarlyAfterNullGuardActionStop)}.");

	private static void AddCmEmotionLateStop(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport summary) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmEmotionLateStop,
			true,
			"CM_EMOTION",
			"Stop occurs only after cancellation checks, stance checks, emotion-specific state mutation, canUse gating, and optional SM_EMOTION broadcast.",
			CommonStopObservables(),
			"Trace cancelUseItem/cancelCurrentSkill, representative state changes, optional broadcast, and late stop order.",
			"Java trace artifact for successful SIT/STAND or equivalent late path with active and inactive protection, including canUse false broadcast-skip branch.",
			$"Summary late emotion evidence={summary.HasLateGuardedEmotionSource}.");

	private static void AddCmEmotionEarlyReturn(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport summary) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmEmotionEarlyReturnNoStop,
			false,
			"CM_EMOTION",
			"Dead, abnormal movement/fear/confuse, private-shop/attack-mode, select-target, stance rejection, and validation-return branches skip stop.",
			"No controller stop observables expected; no task-map cancellation, blinking unset, SM_PLAYER_STATE, or notifyAIOnMove should occur.",
			"Trace any pre-return side effects such as SELECT_TARGET cancelUseItem or stance rejection packet without stopProtectionActiveTask.",
			"Java trace artifact for representative CM_EMOTION early-return branches that prove no stop call occurs.",
			$"Summary late emotion evidence={summary.HasLateGuardedEmotionSource}; early-return no-stop cases must be represented separately.");

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario scenario,
		bool expectsStop,
		string javaPacketSources,
		string expectedStopPosition,
		string expectedControllerObservables,
		string expectedPacketOrActionObservables,
		string requiredJavaTraceArtifact,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonRow(
			rows.Count + 1,
			scenario,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonStatus.BlockedMissingJavaTraceArtifact,
			expectsStop,
			javaPacketSources,
			expectedStopPosition,
			expectedControllerObservables,
			expectedPacketOrActionObservables,
			requiredJavaTraceArtifact,
			notes));
	}

	private static string CommonStopObservables() =>
		"Expect stopProtectionActiveTask call, cancelTask(TaskId.PROTECTION_ACTIVE), spawned-player BLINKING unset, SM_PLAYER_STATE broadcast, and notifyAIOnMove when spawned.";
}
