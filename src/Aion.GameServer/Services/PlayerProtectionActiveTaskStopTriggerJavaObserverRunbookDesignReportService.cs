namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea
{
	ToolingPrerequisite,
	PacketStopTriggerHook,
	ControllerProtectionHook,
	ControllerTaskMapHook,
	TeleportAnimationHook,
	PacketFanoutHook,
	TraceSerializer,
	ArtifactGenerationCommand,
}

public enum PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus
{
	ReadyForDesignOnly,
	BlockedMissingJava25Maven,
}

public sealed record PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea Area,
	PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus Status,
	string JavaSource,
	string ExpectedObserverEvent,
	string ArtifactOutput,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignRow> Rows,
	bool HasToolingPrerequisite,
	bool HasPacketStopTriggerHooks,
	bool HasControllerHooks,
	bool HasTeleportHooks,
	bool HasSerializerPlan,
	bool RequiresJava25Maven,
	bool ReadyForArtifactGeneration,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live runbook design for a future Java observer that can emit
/// protection stop-trigger schema-v1 artifacts without mutating Java gameplay control flow.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReportService
{
	public static PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReport Create(
		PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport executionPlan)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignRow>();

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ToolingPrerequisite,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.BlockedMissingJava25Maven,
			"root pom.xml; local java/javac/mvn tooling",
			"tooling_prerequisite",
			"no artifact output",
			"Root Maven requires compiler release 25; local Java is 1.8.0_491, javac is absent, Maven is absent, and no Maven wrapper exists.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.PacketStopTriggerHook,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.ReadyForDesignOnly,
			"CM_MOVE; CM_MOVE_IN_AIR; CM_ATTACK; CM_CASTSPELL; CM_USE_ITEM; CM_SHOW_DIALOG; CM_DIALOG_SELECT; CM_COMPOSITE_STONES; CM_EMOTION",
			"packet_stop_trigger_decision",
			"traceRows[].phase=packet_stop_decision",
			"Observer should capture protection-active guard outcome, stop-call intent, packet name, deterministic return reason, and player snapshot without changing runImpl control flow.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ControllerProtectionHook,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.ReadyForDesignOnly,
			"PlayerController.startProtectionActiveTask; PlayerController.stopProtectionActiveTask",
			"controller_protection_state",
			"traceRows[].phase=controller_protection_state",
			"Observer should capture BLINKING before/after, spawned guard result, broadcast intent, and AI move notification intent without altering scheduler or packet fanout behavior.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ControllerTaskMapHook,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.ReadyForDesignOnly,
			"CreatureController.addTask; getAndRemoveTask; cancelTask; cancelTaskIfPresent; cancelAllTasks",
			"controller_task_map_operation",
			"traceRows[].phase=controller_task_map",
			"Observer should preserve ConcurrentHashMap task ordering uncertainty and record task id, operation, cancel(false) intent, and future done state when available.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.TeleportAnimationHook,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.ReadyForDesignOnly,
			"TeleportService.sendLoc; CM_TELEPORT_ANIMATION_DONE.runImpl",
			"teleport_animation_task_dispatch",
			"traceRows[].phase=teleport_task_remove|teleport_task_run|teleport_exception_fallback",
			"Observer should capture FutureTask storage/removal, RunnableFuture type check, isDone guard, run/get execution, exception fallback, SM_PLAYER_INFO fallback intent, and World.spawn intent.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.PacketFanoutHook,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.ReadyForDesignOnly,
			"PacketSendUtility.broadcastToSightedPlayers; PacketSendUtility.sendPacket; AionServerPacket",
			"packet_fanout_or_serialization_boundary",
			"traceRows[].packetName and optional fanout metadata",
			"Observer should record packet names and intended recipients when available; byte-level serialization capture is still a separate blocked prerequisite.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.TraceSerializer,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.ReadyForDesignOnly,
			"future schema-v1 Java trace serializer",
			"schema_v1_serializer",
			"parity-artifacts/protection-stop-trigger/java/*.json",
			"Serializer must preserve schemaVersion, Java commit, scenario, event order, enum names, invariant floats, nulls, and timestamp non-parity semantics.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ArtifactGenerationCommand,
			PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.BlockedMissingJava25Maven,
			"root Maven reactor; game-server module",
			"artifact_generation_command",
			"parity-artifacts/protection-stop-trigger/java",
			$"Command shape remains blocked until Java 25 JDK and Maven are available; executionPlanRows={executionPlan.Rows.Count}; needsJavaTooling={executionPlan.NeedsJavaTooling}; ready={executionPlan.ReadyForRuntimeComparison}.");

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReport(
			rowArray,
			HasToolingPrerequisite: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ToolingPrerequisite),
			HasPacketStopTriggerHooks: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.PacketStopTriggerHook),
			HasControllerHooks: rowArray.Any(row =>
				row.Area is PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ControllerProtectionHook
					or PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ControllerTaskMapHook),
			HasTeleportHooks: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.TeleportAnimationHook),
			HasSerializerPlan: rowArray.Any(row =>
				row.Area is PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.TraceSerializer
					or PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ArtifactGenerationCommand),
			RequiresJava25Maven: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.BlockedMissingJava25Maven),
			ReadyForArtifactGeneration: false,
			"Protection stop-trigger Java observer/runbook design; no Java source modified",
			IsLive: false);
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea area,
		PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus status,
		string javaSource,
		string expectedObserverEvent,
		string artifactOutput,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignRow(
			rows.Count + 1,
			area,
			status,
			javaSource,
			expectedObserverEvent,
			artifactOutput,
			notes));
	}
}
