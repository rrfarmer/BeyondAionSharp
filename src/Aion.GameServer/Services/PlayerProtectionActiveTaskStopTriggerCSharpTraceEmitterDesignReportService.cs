namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite
{
	ArtifactShapeValidationBoundary,
	PacketGuardAndStopDecision,
	PacketExitReason,
	ControllerStopEntry,
	TaskCancellation,
	VisualStateMutation,
	StateBroadcastFanout,
	AiMoveNotification,
	TeleportAnimationTaskDispatch,
}

public enum PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignStatus
{
	ReadyForDesignOnly,
	BlockedMissingLiveEmitter,
}

public sealed record PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite HookSite,
	PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignStatus Status,
	string JavaSource,
	string CSharpTarget,
	string RequiredTraceFields,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> Rows,
	bool HasPacketHookSites,
	bool HasControllerHookSites,
	bool HasTeleportHookSites,
	bool RequiresLiveEmitter,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live design for future C# trace emitters that should produce
/// PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceRow records matching generated Java artifacts.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService
{
	public static PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport Create(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport runtimeDesign,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport traceSchema)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow>();

		AddArtifactShapeValidationBoundary(rows);
		AddPacketGuardAndStopDecision(rows);
		AddPacketExitReason(rows);
		AddControllerStopEntry(rows);
		AddTaskCancellation(rows);
		AddVisualStateMutation(rows);
		AddStateBroadcastFanout(rows);
		AddAiMoveNotification(rows);
		AddTeleportAnimationTaskDispatch(rows);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport(
			rowArray,
			HasPacketHookSites: rowArray.Any(row =>
				row.HookSite is PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.PacketGuardAndStopDecision
					or PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.PacketExitReason),
			HasControllerHookSites: rowArray.Any(row =>
				row.HookSite is PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.ControllerStopEntry
					or PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.TaskCancellation
					or PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.VisualStateMutation
					or PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.StateBroadcastFanout
					or PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.AiMoveNotification),
			HasTeleportHookSites: rowArray.Any(row => row.HookSite == PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.TeleportAnimationTaskDispatch),
			RequiresLiveEmitter: true,
			ReadyForRuntimeComparison: false,
			$"Future C# trace emitter design; runtimeDesignReady={runtimeDesign.ReadyForRuntimeComparison}; schemaReady={traceSchema.ReadyForRuntimeComparison}",
			IsLive: false);
	}

	private static void AddArtifactShapeValidationBoundary(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.ArtifactShapeValidationBoundary,
			"schema-v1 generated Java trace artifact contract",
			"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService / future C# trace emitter row adapter",
			"actionBranchName, player snapshot, movement, scheduler, taskCancellation, fanout, aiNotify, emotion, actionPayload, callerOrigin",
			"Future C# trace rows must satisfy the same broad shape contract as generated Java artifacts; this remains non-live metadata and does not prove parity.");

	private static void AddPacketGuardAndStopDecision(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.PacketGuardAndStopDecision,
			"CM_MOVE/CM_MOVE_IN_AIR/action packet runImpl guard and stop decision sites",
			"future C# game packet handlers before/around stopProtectionActiveTask calls",
			"Scenario, EventSeq, Phase, PacketName, ReturnReason, StopCalled, ExpectsStopProtectionCall, player snapshot",
			"Must preserve Java packet guard order and no-stop early returns before any live C# packet stop hook is enabled.");

	private static void AddPacketExitReason(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.PacketExitReason,
			"AionClientPacket.runImpl and concrete packet return branches",
			"future C# packet exit trace adapter",
			"EventSeq, Phase, PacketName, ReturnReason, TimestampIsParityKey=false",
			"Exit rows must use deterministic return reasons, never wall-clock timestamps, as parity keys.");

	private static void AddControllerStopEntry(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.ControllerStopEntry,
			"PlayerController.stopProtectionActiveTask",
			"future C# PlayerController stop trace boundary",
			"PlayerObjectId, ProtectionActiveBefore, ProtectionActiveAfter, VisualStateBefore, VisualStateAfter",
			"Controller entry/exit rows are required to separate packet-origin stops from scheduled callback-origin stops.");

	private static void AddTaskCancellation(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.TaskCancellation,
			"CreatureController.cancelTask(TaskId.PROTECTION_ACTIVE)",
			"future C# controller task-map adapter trace boundary",
			"EventSeq, Phase, ReturnReason, StopCalled, task cancellation metadata when available",
			"Java removes the future before cancel(false); future C# tracing must not change task-map ordering or cancellation behavior.");

	private static void AddVisualStateMutation(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.VisualStateMutation,
			"PlayerController.stopProtectionActiveTask visual state mutation",
			"future C# visual-state mutation trace boundary",
			"PlayerSpawned, VisualStateBefore, VisualStateAfter, ProtectionActiveBefore, ProtectionActiveAfter",
			"Only spawned Java players clear BLINKING and broadcast state; unspawned stop calls must not look like missing trace rows.");

	private static void AddStateBroadcastFanout(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.StateBroadcastFanout,
			"PacketSendUtility.broadcastToSightedPlayers(... SM_PLAYER_STATE ..., true)",
			"future C# sighted-player fanout trace boundary",
			"PacketName, fanout packet name/recipient metadata when available, player snapshot",
			"Packet fanout remains metadata-only here; no socket sends or packet bytes are emitted.");

	private static void AddAiMoveNotification(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.AiMoveNotification,
			"PlayerController.notifyAIOnMove",
			"future C# AI move-notification trace boundary",
			"EventSeq, Phase, PlayerObjectId, StopCalled, player snapshot",
			"AI notification may be asynchronous in Java; tracing must record intent without changing scheduling.");

	private static void AddTeleportAnimationTaskDispatch(ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.TeleportAnimationTaskDispatch,
			"CM_TELEPORT_ANIMATION_DONE.runImpl and TeleportService.SpawnTask.run",
			"future C# teleport animation/spawn-task trace boundary",
			"Scenario, EventSeq, Phase, PacketName, ReturnReason, player snapshot",
			"Needed for missing/done/non-runnable task no-ops, inline runnable execution, exception fallback, and same-map protection-start skip paths.");

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite hookSite,
		string javaSource,
		string csharpTarget,
		string requiredTraceFields,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignRow(
			rows.Count + 1,
			hookSite,
			PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignStatus.BlockedMissingLiveEmitter,
			javaSource,
			csharpTarget,
			requiredTraceFields,
			notes));
	}
}
