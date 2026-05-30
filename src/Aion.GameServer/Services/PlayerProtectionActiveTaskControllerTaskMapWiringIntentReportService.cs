namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskControllerTaskMapWiringHook
{
	StartProtectionTaskStorage,
	StopProtectionTaskCancellation,
	SchedulerCallbackExecution,
	ControllerLifecycleCleanup,
	SpawnedPlayerSideEffects,
	FirstActionPacketStopTriggers,
	RuntimeComparison,
}

public enum PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus
{
	PlannedBlocked,
	SkippedByJavaBranch,
	BlockedMissingPrerequisite,
	NeedsRuntimeVerification,
}

public sealed record PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow(
	int Order,
	PlayerProtectionActiveTaskControllerTaskMapWiringHook Hook,
	PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus Status,
	bool ShouldImplementHook,
	bool BlocksImplementation,
	string JavaOperation,
	string JavaSource,
	string CSharpTarget,
	string Notes
);

public sealed record PlayerProtectionActiveTaskControllerTaskMapWiringIntentReport(
	PlayerProtectionActiveTaskAdapterAction Action,
	int PlayerObjectId,
	IReadOnlyList<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> Rows,
	bool HasStartStorageIntent,
	bool HasStopCancellationIntent,
	bool HasSchedulerCallbackIntent,
	bool HasLifecycleCleanupIntent,
	bool HasFirstActionPacketStopTriggerIntent,
	bool HasRuntimeComparisonBlocker,
	bool ReadyForImplementation,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskControllerTaskMapWiringIntentReportService
{
	public static PlayerProtectionActiveTaskControllerTaskMapWiringIntentReport Create(
		PlayerProtectionActiveTaskLifecycleClosureReport closure,
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport? stopTriggerSummary = null
	)
	{
		// Java parity: wiring live protection-task scheduling depends on the whole PlayerController and
		// CreatureController chain: addTask on start, cancelTask on stop, ThreadPoolManager callback execution,
		// lifecycle cleanup, and first-action packet stop triggers. This report turns that into implementation intent.
		var rows = new List<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow>();

		AddStartStorage(rows, closure);
		AddStopCancellation(rows, closure);
		AddSchedulerCallback(rows, closure);
		AddLifecycleCleanup(rows, closure);
		AddSpawnedSideEffects(rows, closure);
		AddFirstActionPacketStopTriggers(rows, stopTriggerSummary);
		AddRuntimeComparison(rows, closure);

		var rowArray = rows.ToArray();
		var ready = rowArray.Length > 0 && rowArray.All(row => !row.BlocksImplementation);

		return new PlayerProtectionActiveTaskControllerTaskMapWiringIntentReport(
			closure.Action,
			closure.PlayerObjectId,
			rowArray,
			HasStartStorageIntent: rowArray.Any(row =>
				row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.StartProtectionTaskStorage && row.ShouldImplementHook
			),
			HasStopCancellationIntent: rowArray.Any(row =>
				row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.StopProtectionTaskCancellation && row.ShouldImplementHook
			),
			HasSchedulerCallbackIntent: rowArray.Any(row =>
				row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.SchedulerCallbackExecution && row.ShouldImplementHook
			),
			HasLifecycleCleanupIntent: rowArray.Any(row =>
				row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.ControllerLifecycleCleanup && row.ShouldImplementHook
			),
			HasFirstActionPacketStopTriggerIntent: rowArray.Any(row =>
				row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.FirstActionPacketStopTriggers && row.ShouldImplementHook
			),
			HasRuntimeComparisonBlocker: rowArray.Any(row =>
				row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.RuntimeComparison && row.BlocksImplementation
			),
			ReadyForImplementation: ready,
			"PlayerController protection active task-map wiring intent report",
			IsLive: false
		);
	}

	private static void AddStartStorage(
		ICollection<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> rows,
		PlayerProtectionActiveTaskLifecycleClosureReport closure
	)
	{
		var scheduler = Find(closure, PlayerProtectionActiveTaskLifecycleClosurePrerequisite.SchedulerCallbackPlan);
		if (scheduler?.Status == PlayerProtectionActiveTaskLifecycleClosureStatus.Skipped)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskControllerTaskMapWiringHook.StartProtectionTaskStorage,
				PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.SkippedByJavaBranch,
				shouldImplementHook: false,
				blocksImplementation: false,
				"if (!getOwner().isProtectionActive()) ... addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)",
				"PlayerController.startProtectionActiveTask",
				"future PlayerController start protection task-map storage hook",
				"Java already-protected branch returns before scheduler/task-map storage, so this path should not request start storage wiring."
			);
			return;
		}

		var owner = Find(closure, PlayerProtectionActiveTaskLifecycleClosurePrerequisite.OwnerPrototype);
		Add(
			rows,
			PlayerProtectionActiveTaskControllerTaskMapWiringHook.StartProtectionTaskStorage,
			ToStatus(owner),
			shouldImplementHook: owner is { Status: not PlayerProtectionActiveTaskLifecycleClosureStatus.Skipped },
			blocksImplementation: true,
			"addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)",
			"PlayerController.startProtectionActiveTask -> CreatureController.addTask",
			"future controller-owned protection task-map storage hook",
			owner == null
				? "Owner prototype evidence is missing, so production task-map storage cannot be wired."
				: "Controller-owned owner prototype exists only as non-live metadata; production PlayerController/CreatureController storage remains unwired."
		);
	}

	private static void AddStopCancellation(
		ICollection<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> rows,
		PlayerProtectionActiveTaskLifecycleClosureReport closure
	)
	{
		var preview = Find(closure, PlayerProtectionActiveTaskLifecycleClosurePrerequisite.DelayedStopCallbackPreview);
		if (preview?.Status == PlayerProtectionActiveTaskLifecycleClosureStatus.Skipped)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskControllerTaskMapWiringHook.StopProtectionTaskCancellation,
				PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.SkippedByJavaBranch,
				shouldImplementHook: false,
				blocksImplementation: false,
				"stopProtectionActiveTask -> cancelTask(TaskId.PROTECTION_ACTIVE)",
				"PlayerController.stopProtectionActiveTask",
				"future stop protection task cancellation hook",
				"Delayed callback preview was skipped because Java did not schedule a delayed stop in this branch."
			);
			return;
		}

		Add(
			rows,
			PlayerProtectionActiveTaskControllerTaskMapWiringHook.StopProtectionTaskCancellation,
			ToStatus(preview),
			shouldImplementHook: preview is { EvidenceSource: not "Readiness aggregate" },
			blocksImplementation: true,
			"cancelTask(TaskId.PROTECTION_ACTIVE)",
			"PlayerController.stopProtectionActiveTask -> CreatureController.cancelTask",
			"future stop protection task-map cancellation hook",
			preview == null || preview.EvidenceSource == "Readiness aggregate"
				? "Delayed callback preview is missing, so stop cancellation wiring intent remains blocked."
				: "Stop cancellation is represented by non-live metadata only; production cancellation hook remains unwired."
		);
	}

	private static void AddSchedulerCallback(
		ICollection<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> rows,
		PlayerProtectionActiveTaskLifecycleClosureReport closure
	)
	{
		var scheduler = Find(closure, PlayerProtectionActiveTaskLifecycleClosurePrerequisite.SchedulerCallbackPlan);
		if (scheduler?.Status == PlayerProtectionActiveTaskLifecycleClosureStatus.Skipped)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskControllerTaskMapWiringHook.SchedulerCallbackExecution,
				PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.SkippedByJavaBranch,
				shouldImplementHook: false,
				blocksImplementation: false,
				"ThreadPoolManager.getInstance().schedule(this::stopProtectionActiveTask, 60000)",
				"PlayerController.startProtectionActiveTask",
				"future scheduler callback execution hook",
				"Java already-protected branch returns before scheduling, so this path should not request scheduler callback wiring."
			);
			return;
		}

		Add(
			rows,
			PlayerProtectionActiveTaskControllerTaskMapWiringHook.SchedulerCallbackExecution,
			ToStatus(scheduler),
			shouldImplementHook: scheduler != null,
			blocksImplementation: true,
			"ThreadPoolManager.getInstance().schedule(this::stopProtectionActiveTask, 60000)",
			"PlayerController.startProtectionActiveTask / ThreadPoolManager.schedule",
			"future C# scheduler callback execution hook",
			scheduler == null
				? "Scheduler callback metadata is missing, so production scheduling intent remains blocked."
				: "Scheduler callback metadata exists but live scheduler/callback execution remains disabled."
		);
	}

	private static void AddLifecycleCleanup(
		ICollection<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> rows,
		PlayerProtectionActiveTaskLifecycleClosureReport closure
	)
	{
		var lifecycle = Find(closure, PlayerProtectionActiveTaskLifecycleClosurePrerequisite.LifecycleCleanup);
		Add(
			rows,
			PlayerProtectionActiveTaskControllerTaskMapWiringHook.ControllerLifecycleCleanup,
			ToStatus(lifecycle),
			shouldImplementHook: lifecycle != null,
			blocksImplementation: true,
			"onDelete() -> cancelAllTasks()",
			"CreatureController.onDelete",
			"future controller lifecycle cleanup hook",
			lifecycle == null
				? "Lifecycle cleanup evidence is missing, so production cleanup wiring remains blocked."
				: "Lifecycle cleanup evidence is non-live or blocked; production delete/logout cleanup hook remains unwired."
		);
	}

	private static void AddSpawnedSideEffects(
		ICollection<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> rows,
		PlayerProtectionActiveTaskLifecycleClosureReport closure
	)
	{
		var sideEffects = Find(closure, PlayerProtectionActiveTaskLifecycleClosurePrerequisite.LiveSideEffects);
		Add(
			rows,
			PlayerProtectionActiveTaskControllerTaskMapWiringHook.SpawnedPlayerSideEffects,
			ToStatus(sideEffects),
			shouldImplementHook: sideEffects != null,
			blocksImplementation: true,
			"if (player.isSpawned()) unset BLINKING; broadcast SM_PLAYER_STATE; notifyAIOnMove()",
			"PlayerController.stopProtectionActiveTask",
			"future spawned-player visual/socket/AI side-effect hooks",
			sideEffects == null
				? "Spawned-player side-effect readiness evidence is missing."
				: "Visual mutation, packet fanout, and AI notification remain disabled until production paths exist."
		);
	}

	private static void AddRuntimeComparison(
		ICollection<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> rows,
		PlayerProtectionActiveTaskLifecycleClosureReport closure
	)
	{
		var runtime = Find(closure, PlayerProtectionActiveTaskLifecycleClosurePrerequisite.RuntimeComparison);
		Add(
			rows,
			PlayerProtectionActiveTaskControllerTaskMapWiringHook.RuntimeComparison,
			PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.NeedsRuntimeVerification,
			shouldImplementHook: false,
			blocksImplementation: true,
			"Future.cancel(false) / ScheduledFuture callback timing / ConcurrentHashMap",
			runtime?.JavaSource ?? "ThreadPoolManager.schedule / CreatureController.tasks",
			"future Java runtime comparison artifact gate",
			runtime == null
				? "Runtime comparison prerequisite is missing from the closure report."
				: "Java runtime comparison must be generated before production scheduler/future/concurrency parity can be claimed."
		);
	}

	private static void AddFirstActionPacketStopTriggers(
		ICollection<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport? stopTriggerSummary
	)
	{
		if (stopTriggerSummary == null)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskControllerTaskMapWiringHook.FirstActionPacketStopTriggers,
				PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.BlockedMissingPrerequisite,
				shouldImplementHook: false,
				blocksImplementation: true,
				"client packet runImpl -> player.getController().stopProtectionActiveTask()",
				"CM_MOVE / CM_MOVE_IN_AIR / action packet callers",
				"future packet-handler stopProtectionActiveTask hook composition",
				"First-action stop-trigger summary is missing, so production packet stop hook wiring cannot be requested."
			);
			return;
		}

		Add(
			rows,
			PlayerProtectionActiveTaskControllerTaskMapWiringHook.FirstActionPacketStopTriggers,
			stopTriggerSummary.HasRuntimeComparisonBlocker
				? PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.NeedsRuntimeVerification
				: PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.PlannedBlocked,
			shouldImplementHook: stopTriggerSummary.HasAllKnownPacketSources
				&& !stopTriggerSummary.Rows.Any(row => row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.PendingDetailedAudit),
			blocksImplementation: true,
			"client packet runImpl -> stopProtectionActiveTask before/after packet-specific guards",
			stopTriggerSummary.JavaSource,
			"future production packet-handler stop hook integration",
			stopTriggerSummary.ReadyForProductionPacketStopWiring
				? "Summary reports packet stop triggers ready, but production integration is still held behind this wiring intent gate."
				: "Summary classifies packet stop triggers but production packet handlers, controller side effects, and Java runtime comparison remain disabled."
		);
	}

	private static PlayerProtectionActiveTaskLifecycleClosureRow? Find(
		PlayerProtectionActiveTaskLifecycleClosureReport closure,
		PlayerProtectionActiveTaskLifecycleClosurePrerequisite prerequisite
	) => closure.Rows.FirstOrDefault(row => row.Prerequisite == prerequisite);

	private static PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus ToStatus(PlayerProtectionActiveTaskLifecycleClosureRow? row) =>
		row?.Status switch
		{
			PlayerProtectionActiveTaskLifecycleClosureStatus.Skipped => PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.SkippedByJavaBranch,
			PlayerProtectionActiveTaskLifecycleClosureStatus.NeedsVerification =>
				PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.NeedsRuntimeVerification,
			null => PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.BlockedMissingPrerequisite,
			_ => PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.PlannedBlocked,
		};

	private static void Add(
		ICollection<PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow> rows,
		PlayerProtectionActiveTaskControllerTaskMapWiringHook hook,
		PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus status,
		bool shouldImplementHook,
		bool blocksImplementation,
		string javaOperation,
		string javaSource,
		string cSharpTarget,
		string notes
	)
	{
		rows.Add(
			new PlayerProtectionActiveTaskControllerTaskMapWiringIntentRow(
				rows.Count + 1,
				hook,
				status,
				shouldImplementHook,
				blocksImplementation,
				javaOperation,
				javaSource,
				cSharpTarget,
				notes
			)
		);
	}
}
