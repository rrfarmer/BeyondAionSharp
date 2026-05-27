namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase
{
	PacketRead,
	PacketEnter,
	PreStopGuard,
	PreStopSideEffect,
	StopConditionEval,
	GuardReturn,
	StopCallEnter,
	StopCalled,
	TaskCancel,
	VisualMutate,
	StateBroadcast,
	PacketFanout,
	AiNotifyEnqueue,
	AiNotify,
	StopCallExit,
	PostStopPacketSideEffect,
	PacketReturn,
	PacketExit,
}

public enum PlayerProtectionActiveTaskStopTriggerTraceArtifactField
{
	TraceSchemaVersion,
	TraceId,
	EventSeq,
	ServerFlavor,
	PlayerObjectId,
	WorldId,
	PacketName,
	PacketOpcode,
	PacketSequence,
	ThreadId,
	ThreadName,
	WallTimeEpochMillis,
	TimestampNanos,
	Phase,
	JavaSource,
	JavaSourceFile,
	JavaLine,
	ProtectionActiveBefore,
	ProtectionActiveAfter,
	VisualStateBefore,
	VisualStateAfter,
	PlayerSpawned,
	PlayerDead,
	PlayerFlying,
	PlayerTrading,
	PlayerCasting,
	PlayerUsingItem,
	TaskIdName,
	TaskIdOrdinal,
	TaskPresentBeforeCancel,
	TaskRemovedBeforeCancel,
	FutureCancelArgument,
	FutureCancelResult,
	ScheduledDelayMillis,
	StopOrigin,
	PacketReturnReason,
	FanoutPacketName,
	FanoutRecipientCount,
	NotifyAiOnMoveCalled,
	MovementOldX,
	MovementOldY,
	MovementOldZ,
	MovementPacketX,
	MovementPacketY,
	MovementPacketZ,
	MovementZDelta,
	MovementHeading,
	MovementType,
	MovementAntiHackAccepted,
	TeleportationModeAbsoluteMove,
	AirMovementFlightPathPresent,
	AirMovementDistance,
	ActionBranchName,
	AttackTargetObjectId,
	CastSpellId,
	CastTargetType,
	ItemObjectId,
	DialogTargetObjectId,
	DialogActionId,
	DialogQuestId,
	CompositeToolObjectId,
	CompositeFirstObjectId,
	CompositeSecondObjectId,
	EmotionType,
	EmotionId,
	EmotionStance,
	EmotionCanUse,
	EmotionBroadcasted,
}

public enum PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason
{
	DeadGuard,
	FearOrConfuseGuard,
	BogusPacket,
	CmMoveAntiHackRejected,
	CmMoveNotSpawned,
	CmMoveTeleportationAbsoluteMove,
	CmMoveSamePositionTurn,
	CmMoveAcceptedXyDelta,
	CmMoveAcceptedZDropThreshold,
	CmMoveInAirNotSpawned,
	CmMoveInAirNotFlying,
	CmMoveInAirInactiveProtection,
	CmMoveInAirAcceptedFlying,
	SpellZeroCancel,
	PetOrderWithoutPet,
	SkillTemplateMissingOrPassive,
	ItemMissing,
	ItemRestrictionFailed,
	ItemNoActions,
	TradingGuard,
	UnknownDialogAction,
	QuestTemplateMissing,
	UnsupportedDialogAction,
	IllegalDialogAction,
	EarlyActionPreStopGuardReturned,
	EarlyActionStopThenContinue,
	CmCompositeNullPlayer,
	CompositeToolMissing,
	CompositeFirstMissing,
	CompositeSecondMissing,
	CompositeCanActFailed,
	CmCompositeInvalidAfterStop,
	CmCompositeSuccessfulScheduleAfterStop,
	CmEmotionDeadOrAbnormalReturn,
	CmEmotionPrivateShopOrAttackModeReturn,
	CmEmotionSelectTargetReturn,
	CmEmotionStanceRejectionReturn,
	CmEmotionValidationReturn,
	CmEmotionLateStopAfterStateMutation,
	ProtectionInactiveNoStop,
	StopCompleted,
}

public enum PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus
{
	BlockedMissingJavaInstrumentation,
	BlockedMissingTraceSerializer,
	ReadyForJavaImplementationDesignOnly,
}

public sealed record PlayerProtectionActiveTaskStopTriggerTraceArtifactPhaseRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase Phase,
	PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus Status,
	string JavaSource,
	string RequiredObservation,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerTraceArtifactFieldRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerTraceArtifactField Field,
	PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus Status,
	string RequiredFor,
	string SerializationNote,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReasonRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason Reason,
	string PacketName,
	string JavaSource,
	bool ExpectsStopProtectionCall,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerTraceArtifactControllerObservableRow(
	int Order,
	string JavaOperation,
	string JavaSource,
	PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus Status,
	string RequiredFields,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerTraceArtifactInstrumentationCaveatRow(
	int Order,
	string Caveat,
	string JavaSource,
	string Risk);

public sealed record PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactPhaseRow> Phases,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactFieldRow> Fields,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReasonRow> PacketReturnReasons,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactControllerObservableRow> ControllerObservables,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactInstrumentationCaveatRow> InstrumentationCaveats,
	bool HasAllRequiredPhases,
	bool HasMovementPrecisionFields,
	bool HasTaskCancellationFields,
	bool HasFanoutAndAiFields,
	bool HasPacketReturnReasons,
	bool RequiresJavaInstrumentation,
	bool RequiresTraceSerializer,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: future trace artifact schema for
/// com.aionemu.gameserver.network.aion.clientpackets.* stopProtectionActiveTask callers,
/// PlayerController.startProtectionActiveTask/stopProtectionActiveTask, and CreatureController task-map operations.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService
{
	public static PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport Create(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport runtimeComparisonDesign)
	{
		var phases = CreatePhases();
		var fields = CreateFields();
		var returnReasons = CreatePacketReturnReasons();
		var controllerObservables = CreateControllerObservables();
		var caveats = CreateInstrumentationCaveats();

		return new PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport(
			phases,
			fields,
			returnReasons,
			controllerObservables,
			caveats,
			HasAllRequiredPhases: RequiredPhases.All(phase => phases.Any(row => row.Phase == phase)),
			HasMovementPrecisionFields: HasFields(fields,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementOldX,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementPacketX,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementZDelta),
			HasTaskCancellationFields: HasFields(fields,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TaskIdOrdinal,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TaskPresentBeforeCancel,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TaskRemovedBeforeCancel,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FutureCancelArgument,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FutureCancelResult),
			HasFanoutAndAiFields: HasFields(fields,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FanoutPacketName,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FanoutRecipientCount,
				PlayerProtectionActiveTaskStopTriggerTraceArtifactField.NotifyAiOnMoveCalled),
			HasPacketReturnReasons: returnReasons.Count > 0,
			RequiresJavaInstrumentation: true,
			RequiresTraceSerializer: true,
			ReadyForRuntimeComparison: false,
			$"Protection stop-trigger trace artifact schema; runtime design ready={runtimeComparisonDesign.ReadyForRuntimeComparison}",
			IsLive: false);
	}

	private static IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactPhaseRow> CreatePhases()
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerTraceArtifactPhaseRow>();
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketRead, "AionClientPacket.readImpl", "Trace raw packet field decisions needed by stop-condition evaluation.", "Optional when packet payload is already captured by packet-enter fields, but required for generated Java vectors.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketEnter, "AionClientPacket.runImpl", "Trace packet identity, sequence, player snapshot, and current protection/task state before any guard can return.", "First phase for every instrumented packet.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PreStopGuard, "CM_MOVE/CM_MOVE_IN_AIR/action packet guards", "Trace guards evaluated before stop can be called.", "Separates branch decisions from returns.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PreStopSideEffect, "CM_EMOTION.runImpl and packet-specific pre-stop paths", "Trace side effects that happen before a late stop or no-stop return.", "Required for CM_EMOTION SELECT_TARGET/cancel-use-item and late state mutation ordering.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopConditionEval, "CM_MOVE.runImpl and protection-active guards", "Trace protection-active and packet-specific stop-condition inputs.", "Needed for exact x/y and strict z threshold comparison.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.GuardReturn, "CM_MOVE/CM_MOVE_IN_AIR/action packet guards", "Trace early returns that skip stopProtectionActiveTask, including packet-specific return reason.", "Required so no-stop branches do not look like missing instrumentation.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopCallEnter, "packet caller -> PlayerController.stopProtectionActiveTask", "Trace call origin and pre-stop protection/visual/task state.", "Must distinguish first-action packet origin from scheduled callback origin.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopCalled, "PlayerController.stopProtectionActiveTask", "Trace controller method entry and stop origin.", "Kept as a coarse compatibility phase for existing runtime-comparison design rows.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.TaskCancel, "CreatureController.cancelTask(TaskId.PROTECTION_ACTIVE)", "Trace task-map presence, remove-before-cancel ordering, and Future.cancel(false) result.", "Java removes the map entry before calling cancel(false).");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.VisualMutate, "PlayerController.stopProtectionActiveTask", "Trace spawned-player BLINKING clear and visual state before/after.", "Unspawned stop calls should not produce visual mutation.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StateBroadcast, "PacketSendUtility.broadcastToSightedPlayers(... SM_PLAYER_STATE ...)", "Trace SM_PLAYER_STATE construction and toSelf=true broadcast request after visual mutation.", "Source player is included before sighted recipients.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketFanout, "PacketSendUtility.broadcastToSightedPlayers(... SM_PLAYER_STATE ...)", "Trace packet name and recipient count for self/sighted-player broadcast behavior.", "Needed before C# socket fanout can be compared.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.AiNotifyEnqueue, "PlayerController.notifyAIOnMove", "Trace MovementNotifyTask enqueue intent after fanout.", "Useful because AI notification execution may be asynchronous.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.AiNotify, "PlayerController.notifyAIOnMove", "Trace notifyAIOnMove invocation after spawned-player fanout.", "Needed before AI move-notification parity can be claimed.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopCallExit, "PlayerController.stopProtectionActiveTask", "Trace protection/task/visual state after controller stop completes.", "Required to prove task cancel precedes visual/fanout/AI side effects.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PostStopPacketSideEffect, "action packet runImpl after stop", "Trace packet work that continues after stop, including invalid-after-stop branches.", "Needed for CM_USE_ITEM and CM_COMPOSITE_STONES.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketReturn, "AionClientPacket.runImpl", "Trace return reason and packet result.", "Stable return reason enum should be emitted here.");
		AddPhase(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketExit, "AionClientPacket.runImpl", "Trace final packet result, return reason, and post-packet protection/task state.", "Last phase for every instrumented branch.");
		return rows;
	}

	private static IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactFieldRow> CreateFields()
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerTraceArtifactFieldRow>();
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TraceSchemaVersion, "every artifact", "integer", "Starts at 1 and changes only on incompatible trace schema changes.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TraceId, "every row", "stable string/id", "Correlates phases for one packet execution without relying on thread id alone.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.EventSeq, "every row", "monotonic integer per trace", "Preserves phase ordering without relying on wall-clock time.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ServerFlavor, "every row", "string: java", "Prevents mixing Java and future C# artifacts.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerObjectId, "every row", "integer", "Required to correlate packet, controller, fanout, and AI events.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.WorldId, "packet/controller rows", "integer or null", "Useful when comparing movement/world fanout with C# world instances.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PacketName, "packet phases", "string", "Uses Java class simple name such as CM_MOVE.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PacketOpcode, "packet phases", "integer or null", "Use null if unavailable without changing packet handling.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PacketSequence, "packet phases", "monotonic integer", "Preserves per-connection ordering when thread scheduling interleaves traces.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ThreadId, "every row", "integer/string", "Threading diagnostic only; not a parity key.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ThreadName, "every row", "string", "Diagnostic for Java packet/scheduler thread source.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.WallTimeEpochMillis, "every row", "long", "Diagnostic only; do not use as wall-clock parity evidence.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TimestampNanos, "every row", "long", "Ordering diagnostic only; avoid wall-clock parity claims.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.Phase, "every row", "enum string", "Must match schema phase names.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.JavaSource, "every row", "class#method string", "Breadcrumb to exact Java source method.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.JavaSourceFile, "every row", "path or simple file name", "Breadcrumb to exact Java source file.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.JavaLine, "every row", "integer or null", "Breadcrumb to line near the branch/side effect, when stable enough.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ProtectionActiveBefore, "packet/stop/task phases", "boolean", "Captures branch guard state before stop.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ProtectionActiveAfter, "packet exit/visual phases", "boolean", "Captures final protection visual-state outcome.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.VisualStateBefore, "stop/visual phases", "string/list", "Must include BLINKING presence when available.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.VisualStateAfter, "visual/exit phases", "string/list", "Must capture BLINKING removal or unspawned skip.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerSpawned, "guard/stop phases", "boolean", "Controls visual/fanout/AI side effects.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerDead, "guard phases", "boolean", "Required for CM_MOVE and CM_EMOTION early-return branches.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerFlying, "CM_MOVE_IN_AIR", "boolean", "Required to prove not-flying skip vs accepted flying stop.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerTrading, "action/emotion packets", "boolean", "Required for show/dialog/emotion pre-stop guard branches.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerCasting, "action/emotion packets", "boolean", "Tracks cancellation ordering for cast/emotion paths.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerUsingItem, "action/emotion packets", "boolean", "Tracks cancelUseItem ordering.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TaskIdName, "task phases", "string", "Expected PROTECTION_ACTIVE.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TaskIdOrdinal, "task phases", "integer", "Expected Java TaskId.PROTECTION_ACTIVE ordinal; do not assume C# enum numeric equivalence.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TaskPresentBeforeCancel, "task cancel", "boolean", "Uses map presence, not !Future.isDone, to mirror cancelTask.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TaskRemovedBeforeCancel, "task cancel", "boolean", "Documents remove-before-cancel ordering.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FutureCancelArgument, "task cancel", "boolean", "Expected false for Java Future.cancel(false).");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FutureCancelResult, "task cancel", "boolean or null", "Result may differ for already-running callbacks; record without branching on it.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ScheduledDelayMillis, "start/callback traces", "integer", "Expected 60000 for protection delayed stop scheduling.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.StopOrigin, "stop phases", "enum string", "Values should distinguish first-action packet and scheduled callback origins.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PacketReturnReason, "guard/exit phases", "enum string", "Must use explicit packet return-reason schema values.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FanoutPacketName, "fanout phase", "string", "Expected SM_PLAYER_STATE for spawned stop side effects.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FanoutRecipientCount, "fanout phase", "integer", "Required for socket fanout comparison.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.NotifyAiOnMoveCalled, "AI phase/exit", "boolean", "Expected true only for spawned stop side effect path.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementOldX, "CM_MOVE", "float", "Preserves Java float comparison inputs.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementOldY, "CM_MOVE", "float", "Preserves Java float comparison inputs.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementOldZ, "CM_MOVE", "float", "Preserves asymmetric z threshold input.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementPacketX, "CM_MOVE", "float", "Preserves Java packet coordinate input.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementPacketY, "CM_MOVE", "float", "Preserves Java packet coordinate input.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementPacketZ, "CM_MOVE", "float", "Preserves Java packet coordinate input.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementZDelta, "CM_MOVE", "float", "Record oldZ - packetZ for the strict > 0.5 branch.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementHeading, "CM_MOVE/CM_MOVE_IN_AIR", "byte/integer", "Required for same-position turn and movement packet reproduction.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementType, "CM_MOVE/CM_MOVE_IN_AIR", "integer/string", "Separates movement mode branches without full packet serialization.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementAntiHackAccepted, "CM_MOVE", "boolean", "Captures anti-hack reject before stop.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TeleportationModeAbsoluteMove, "CM_MOVE", "boolean", "Captures absolute teleportation branch that returns before stop.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.AirMovementFlightPathPresent, "CM_MOVE_IN_AIR", "boolean", "Captures distance update branch before stop.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.AirMovementDistance, "CM_MOVE_IN_AIR", "float", "Captures Java flight-path distance side effect before stop.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ActionBranchName, "action/emotion packets", "string", "Names packet-specific branch without needing full production object serialization.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.AttackTargetObjectId, "CM_ATTACK", "integer or null", "Needed for known-list lookup and attack side-effect comparison.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.CastSpellId, "CM_CASTSPELL", "integer", "Needed for zero spell, template, passive, and next-skill branches.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.CastTargetType, "CM_CASTSPELL", "integer/string", "Needed for target-specific cast branches.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ItemObjectId, "CM_USE_ITEM", "integer", "Needed to prove item-missing after stop.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.DialogTargetObjectId, "CM_SHOW_DIALOG/CM_DIALOG_SELECT", "integer", "Needed for NPC/dialog target lookup branch tracing.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.DialogActionId, "CM_DIALOG_SELECT", "integer", "Needed for action-name and illegal-action return reasons.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.DialogQuestId, "CM_DIALOG_SELECT", "integer", "Needed for quest-template return reasons.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.CompositeToolObjectId, "CM_COMPOSITE_STONES", "integer", "Needed for tool-missing invalid-after-stop trace.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.CompositeFirstObjectId, "CM_COMPOSITE_STONES", "integer", "Needed for first-item missing invalid-after-stop trace.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.CompositeSecondObjectId, "CM_COMPOSITE_STONES", "integer", "Needed for second-item missing invalid-after-stop trace.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.EmotionType, "CM_EMOTION", "integer/string", "Needed for emotion branch classification.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.EmotionId, "CM_EMOTION", "integer", "Needed for validation and broadcast branches.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.EmotionStance, "CM_EMOTION", "string", "Needed for stance rejection no-stop trace.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.EmotionCanUse, "CM_EMOTION", "boolean", "Needed for canUse false broadcast-skip branch.");
		AddField(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.EmotionBroadcasted, "CM_EMOTION", "boolean", "Records optional SM_EMOTION broadcast before late stop.");
		return rows;
	}

	private static IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReasonRow> CreatePacketReturnReasons()
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReasonRow>();
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.DeadGuard, "CM_MOVE/CM_ATTACK/CM_CASTSPELL/CM_EMOTION", "runImpl", false, "Dead-player guard returns before stop in representative packets.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.FearOrConfuseGuard, "CM_MOVE/CM_EMOTION", "runImpl", false, "Fear/confuse/abnormal movement returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.BogusPacket, "CM_MOVE", "CM_MOVE.runImpl", false, "Bogus packet returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveAntiHackRejected, "CM_MOVE", "CM_MOVE.runImpl", false, "Anti-hack reject returns before position update and before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveNotSpawned, "CM_MOVE", "CM_MOVE.runImpl", false, "Unspawned player returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveTeleportationAbsoluteMove, "CM_MOVE", "CM_MOVE.runImpl", false, "Teleportation absolute-move branch returns before normal movement stop logic.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveSamePositionTurn, "CM_MOVE", "CM_MOVE.runImpl", false, "Same x/y/z heading-turn branch skips stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveAcceptedXyDelta, "CM_MOVE", "CM_MOVE.runImpl", true, "Accepted x or y delta calls stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveAcceptedZDropThreshold, "CM_MOVE", "CM_MOVE.runImpl", true, "Strict oldZ > packetZ + 0.5 calls stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveInAirNotSpawned, "CM_MOVE_IN_AIR", "CM_MOVE_IN_AIR.runImpl", false, "Unspawned air movement returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveInAirNotFlying, "CM_MOVE_IN_AIR", "CM_MOVE_IN_AIR.runImpl", false, "Not-flying branch returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveInAirInactiveProtection, "CM_MOVE_IN_AIR", "CM_MOVE_IN_AIR.runImpl", false, "Accepted air movement without protection does not call stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveInAirAcceptedFlying, "CM_MOVE_IN_AIR", "CM_MOVE_IN_AIR.runImpl", true, "Spawned flying protected movement calls stop before world movement callbacks.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.SpellZeroCancel, "CM_CASTSPELL", "CM_CASTSPELL.runImpl", false, "Zero spell id returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.PetOrderWithoutPet, "CM_CASTSPELL", "CM_CASTSPELL.runImpl", false, "Pet order without pet returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.SkillTemplateMissingOrPassive, "CM_CASTSPELL", "CM_CASTSPELL.runImpl", false, "Missing or passive skill template returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.ItemMissing, "CM_USE_ITEM", "CM_USE_ITEM.runImpl", true, "Item lookup can fail after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.ItemRestrictionFailed, "CM_USE_ITEM", "CM_USE_ITEM.runImpl", true, "Item restriction can fail after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.ItemNoActions, "CM_USE_ITEM", "CM_USE_ITEM.runImpl", true, "No-actions branch can occur after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.TradingGuard, "CM_SHOW_DIALOG/CM_DIALOG_SELECT", "runImpl", true, "Trading guard occurs after stop in dialog packets.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.UnknownDialogAction, "CM_DIALOG_SELECT", "CM_DIALOG_SELECT.runImpl", true, "Unknown action can return after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.QuestTemplateMissing, "CM_DIALOG_SELECT", "CM_DIALOG_SELECT.runImpl", true, "Quest-template lookup can fail after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.UnsupportedDialogAction, "CM_DIALOG_SELECT", "CM_DIALOG_SELECT.runImpl", true, "Unsupported action can return after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.IllegalDialogAction, "CM_DIALOG_SELECT", "CM_DIALOG_SELECT.runImpl", true, "Illegal interaction can return after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.EarlyActionPreStopGuardReturned, "CM_ATTACK/CM_CASTSPELL/CM_USE_ITEM/CM_SHOW_DIALOG/CM_DIALOG_SELECT", "runImpl", false, "Representative pre-stop guards return before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.EarlyActionStopThenContinue, "CM_ATTACK/CM_CASTSPELL/CM_USE_ITEM/CM_SHOW_DIALOG/CM_DIALOG_SELECT", "runImpl", true, "Protection-active action packets call stop before core action side effects.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmCompositeNullPlayer, "CM_COMPOSITE_STONES", "CM_COMPOSITE_STONES.runImpl", false, "Null player returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CompositeToolMissing, "CM_COMPOSITE_STONES", "CM_COMPOSITE_STONES.runImpl", true, "Missing tool branch occurs after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CompositeFirstMissing, "CM_COMPOSITE_STONES", "CM_COMPOSITE_STONES.runImpl", true, "Missing first item branch occurs after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CompositeSecondMissing, "CM_COMPOSITE_STONES", "CM_COMPOSITE_STONES.runImpl", true, "Missing second item branch occurs after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CompositeCanActFailed, "CM_COMPOSITE_STONES", "CM_COMPOSITE_STONES.runImpl", true, "CompositionAction.canAct failure occurs after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmCompositeInvalidAfterStop, "CM_COMPOSITE_STONES", "CM_COMPOSITE_STONES.runImpl", true, "Invalid item/restriction/canAct branches occur after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmCompositeSuccessfulScheduleAfterStop, "CM_COMPOSITE_STONES", "CM_COMPOSITE_STONES.runImpl", true, "Successful composition action schedules after stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmEmotionDeadOrAbnormalReturn, "CM_EMOTION", "CM_EMOTION.runImpl", false, "Dead/fear/confuse/abnormal-movement guards return before late stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmEmotionPrivateShopOrAttackModeReturn, "CM_EMOTION", "CM_EMOTION.runImpl", false, "Private-shop/attack-mode branches return before late stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmEmotionSelectTargetReturn, "CM_EMOTION", "CM_EMOTION.runImpl", false, "Select-target branch can perform local side effects but returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmEmotionStanceRejectionReturn, "CM_EMOTION", "CM_EMOTION.runImpl", false, "Stance rejection packet path returns before stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmEmotionValidationReturn, "CM_EMOTION", "CM_EMOTION.runImpl", false, "Validation return after pre-stop state checks still skips stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmEmotionLateStopAfterStateMutation, "CM_EMOTION", "CM_EMOTION.runImpl", true, "Late successful emotion path calls stop after state mutation and optional broadcast.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.ProtectionInactiveNoStop, "all stop-trigger packets", "runImpl", false, "Protection-inactive paths do not call stop.");
		AddReturnReason(rows, PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.StopCompleted, "all stop-trigger packets", "runImpl", true, "Terminal reason for branches where stop completed and packet execution continued or exited normally.");
		return rows;
	}

	private static IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactControllerObservableRow> CreateControllerObservables()
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerTraceArtifactControllerObservableRow>();
		AddControllerObservable(rows, "stopProtectionActiveTask()", "PlayerController.stopProtectionActiveTask", "StopOrigin, ProtectionActiveBefore, PlayerSpawned, VisualStateBefore", "Must be emitted for packet-origin and callback-origin stops.");
		AddControllerObservable(rows, "cancelTask(TaskId.PROTECTION_ACTIVE)", "CreatureController.cancelTask", "TaskIdName, TaskIdOrdinal, TaskPresentBeforeCancel, TaskRemovedBeforeCancel, FutureCancelArgument, FutureCancelResult", "Java removes the task before calling Future.cancel(false).");
		AddControllerObservable(rows, "unsetVisualState(CreatureVisualState.BLINKING)", "PlayerController.stopProtectionActiveTask", "PlayerSpawned, VisualStateBefore, VisualStateAfter", "Only spawned players perform visual mutation.");
		AddControllerObservable(rows, "broadcastToSightedPlayers(... SM_PLAYER_STATE ..., true)", "PlayerController.stopProtectionActiveTask", "FanoutPacketName, FanoutRecipientCount, PlayerObjectId", "Must capture self/sighted-player fanout before C# socket parity can be claimed.");
		AddControllerObservable(rows, "notifyAIOnMove()", "PlayerController.stopProtectionActiveTask", "NotifyAiOnMoveCalled, PlayerObjectId, StopOrigin", "Must be emitted after spawned-player fanout.");
		return rows;
	}

	private static IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactInstrumentationCaveatRow> CreateInstrumentationCaveats()
	{
		return
		[
			new(1, "Do not call Future.isDone from packet paths unless the value is purely observational and cannot alter control flow.", "CreatureController.hasScheduledTask/cancelTask", "Future state observation may race with callback execution and should not become a new branch."),
			new(2, "Do not add synchronization around Java task-map operations for tracing.", "CreatureController.addTask/cancelTask/cancelAllTasks", "Additional synchronization would hide ConcurrentHashMap replacement and weak-iteration behavior."),
			new(3, "Keep trace writes lightweight and async-safe.", "AionClientPacket.runImpl", "Blocking IO in packet execution could alter packet ordering and scheduler timing."),
			new(4, "Tag scheduled callback stops separately from first-action packet stops.", "PlayerController.startProtectionActiveTask/stopProtectionActiveTask", "Callback-origin and packet-origin stop behavior share controller code but have different ordering evidence."),
			new(5, "Record Java float movement inputs without rounding or culture formatting.", "CM_MOVE.runImpl", "The strict z threshold and exact x/y comparisons are precision-sensitive."),
			new(6, "Record timestamps only as ordering diagnostics, not wall-clock parity evidence.", "ThreadPoolManager.schedule", "Java and C# time sources/time zones are not equivalent verification evidence."),
		];
	}

	private static void AddPhase(
		ICollection<PlayerProtectionActiveTaskStopTriggerTraceArtifactPhaseRow> rows,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase phase,
		string javaSource,
		string requiredObservation,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerTraceArtifactPhaseRow(
			rows.Count + 1,
			phase,
			PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus.BlockedMissingJavaInstrumentation,
			javaSource,
			requiredObservation,
			notes));
	}

	private static void AddField(
		ICollection<PlayerProtectionActiveTaskStopTriggerTraceArtifactFieldRow> rows,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactField field,
		string requiredFor,
		string serializationNote,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerTraceArtifactFieldRow(
			rows.Count + 1,
			field,
			PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus.BlockedMissingTraceSerializer,
			requiredFor,
			serializationNote,
			notes));
	}

	private static void AddReturnReason(
		ICollection<PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReasonRow> rows,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason reason,
		string packetName,
		string javaSource,
		bool expectsStop,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReasonRow(
			rows.Count + 1,
			reason,
			packetName,
			javaSource,
			expectsStop,
			notes));
	}

	private static void AddControllerObservable(
		ICollection<PlayerProtectionActiveTaskStopTriggerTraceArtifactControllerObservableRow> rows,
		string javaOperation,
		string javaSource,
		string requiredFields,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerTraceArtifactControllerObservableRow(
			rows.Count + 1,
			javaOperation,
			javaSource,
			PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus.BlockedMissingJavaInstrumentation,
			requiredFields,
			notes));
	}

	private static bool HasFields(
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerTraceArtifactFieldRow> fields,
		params PlayerProtectionActiveTaskStopTriggerTraceArtifactField[] requiredFields) =>
		requiredFields.All(field => fields.Any(row => row.Field == field));

	private static readonly PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase[] RequiredPhases =
	[
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketEnter,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PreStopGuard,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopConditionEval,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.GuardReturn,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopCallEnter,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopCalled,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.TaskCancel,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.VisualMutate,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StateBroadcast,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketFanout,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.AiNotifyEnqueue,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.AiNotify,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopCallExit,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketReturn,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketExit,
	];
}
