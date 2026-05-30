namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskReadinessAggregateArea
{
	BranchObservation,
	VisualMutation,
	KnownListCastCancellation,
	KnownListTargetClear,
	PacketConstruction,
	PacketFanout,
	SchedulerCallback,
	TaskMapStorage,
	TaskMapCancellation,
	ScheduledTaskHandleAdapter,
	LifecycleCleanupHook,
	AiMoveNotification,
	ProductionOwnerSelection,
	JavaRuntimeComparison,
}

public enum PlayerProtectionActiveTaskReadinessAggregateStatus
{
	Ready,
	ObservedNonLive,
	LiveVisualOnly,
	Skipped,
	Blocked,
	NeedsVerification,
}

public sealed record PlayerProtectionActiveTaskReadinessAggregateRequest(
	PlayerProtectionActiveTaskExecutionSummary Summary,
	PlayerProtectionActiveTaskLiveReadinessReport ReadinessReport,
	PlayerProtectionActiveTaskTaskMapAuditReport TaskMapAuditReport,
	IReadOnlyList<PlayerProtectionActiveTaskTaskMapSimulationReport> TaskMapSimulationReports,
	PlayerProtectionActiveTaskTaskMapLifecycleCleanupReport LifecycleCleanupReport,
	PlayerProtectionActiveTaskTaskMapOwnerSelectionReport? OwnerSelectionReport = null,
	PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot? OwnerPrototypeSnapshot = null,
	PlayerProtectionActiveTaskSchedulerCallbackPlan? SchedulerCallbackPlan = null,
	PlayerProtectionActiveTaskDelayedStopCallbackPreview? DelayedStopCallbackPreview = null,
	bool ScheduledTaskHandleAdapterAvailable = true
);

public sealed record PlayerProtectionActiveTaskReadinessAggregateRow(
	int Order,
	PlayerProtectionActiveTaskReadinessAggregateArea Area,
	PlayerProtectionActiveTaskReadinessAggregateStatus Status,
	bool BlocksLiveEnablement,
	string EvidenceSource,
	string JavaOperation,
	string JavaSource,
	string Notes
);

public sealed record PlayerProtectionActiveTaskReadinessAggregateReport(
	PlayerProtectionActiveTaskAdapterAction Action,
	PlayerProtectionActiveTaskAdapterStatus AdapterStatus,
	int PlayerObjectId,
	IReadOnlyList<PlayerProtectionActiveTaskReadinessAggregateRow> Rows,
	IReadOnlyList<PlayerProtectionActiveTaskReadinessAggregateArea> BlockedAreas,
	bool CanEnableProtectionTaskMapStack,
	bool HasStartStorageEvidence,
	bool HasStopCancellationEvidence,
	bool HasLifecycleCleanupEvidence,
	bool HasScheduledTaskHandleAdapterEvidence,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskReadinessAggregateService
{
	public static PlayerProtectionActiveTaskReadinessAggregateReport Create(PlayerProtectionActiveTaskReadinessAggregateRequest request)
	{
		// Java parity: enabling live protection-task behavior depends on the whole chain from
		// PlayerController through CreatureController task storage, ThreadPoolManager scheduling,
		// delayed callback execution, and lifecycle cleanup. This aggregate rolls those blockers up.
		var rows = new List<PlayerProtectionActiveTaskReadinessAggregateRow>();

		AddReadinessRows(rows, request.ReadinessReport);
		AddTaskMapAuditRows(rows, request.TaskMapAuditReport);
		AddTaskMapSimulationRows(rows, request.TaskMapSimulationReports);
		AddScheduledHandleRow(rows, request.ScheduledTaskHandleAdapterAvailable);
		AddLifecycleCleanupRows(rows, request.LifecycleCleanupReport);
		if (request.OwnerSelectionReport != null)
			AddOwnerSelectionRows(rows, request.OwnerSelectionReport);
		if (request.OwnerPrototypeSnapshot != null)
			AddOwnerPrototypeRows(rows, request.OwnerPrototypeSnapshot);
		if (request.SchedulerCallbackPlan != null)
			AddSchedulerCallbackPlanRows(rows, request.SchedulerCallbackPlan);
		if (request.DelayedStopCallbackPreview != null)
			AddDelayedStopCallbackPreviewRows(rows, request.DelayedStopCallbackPreview);
		AddRuntimeComparisonRow(rows);

		var rowArray = rows.ToArray();
		var blockedAreas = rowArray.Where(row => row.BlocksLiveEnablement).Select(row => row.Area).Distinct().ToArray();

		return new PlayerProtectionActiveTaskReadinessAggregateReport(
			request.Summary.Action,
			request.Summary.AdapterStatus,
			request.Summary.PlayerObjectId,
			rowArray,
			blockedAreas,
			CanEnableProtectionTaskMapStack: blockedAreas.Length == 0,
			HasStartStorageEvidence: request.TaskMapSimulationReports.Any(report => report.StoredScheduledTask)
				|| request.SchedulerCallbackPlan?.StoresScheduledFuture == true,
			HasStopCancellationEvidence: request.TaskMapSimulationReports.Any(report => report.CanceledExistingTask || report.RemovedMissingTaskAsNoOp)
				|| request.DelayedStopCallbackPreview is { CancelsOwnerTask: true } or { RemovesMissingTaskAsNoOp: true },
			HasLifecycleCleanupEvidence: request.LifecycleCleanupReport.Rows.Any(row =>
				row.Kind == PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.CancelAllTasks
			),
			HasScheduledTaskHandleAdapterEvidence: request.ScheduledTaskHandleAdapterAvailable,
			"PlayerController protection active task production-readiness aggregate: PlayerController -> CreatureController task map -> ThreadPoolManager -> lifecycle cleanup",
			IsLive: false
		);
	}

	private static void AddDelayedStopCallbackPreviewRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskDelayedStopCallbackPreview preview
	)
	{
		foreach (var row in preview.Rows)
		{
			if (row.Kind == PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordLiveSideEffectBoundary)
			{
				AddDelayedStopLiveBoundaryRows(rows, row, preview);
				continue;
			}

			Add(
				rows,
				ToArea(row.Kind),
				ToStatus(row.Status),
				BlocksLiveEnablement(row),
				"Delayed-stop callback preview",
				row.JavaOperation,
				row.JavaSource,
				$"{row.Notes} InvokesCallback={preview.InvokesCallback}; InvokesScheduler={preview.InvokesScheduler}."
			);
		}

		if (preview.HasScheduledCallbackMetadata && !preview.InvokesCallback)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
				PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
				blocksLiveEnablement: true,
				"Delayed-stop callback preview",
				"scheduled callback execution for this::stopProtectionActiveTask",
				preview.JavaSource,
				"Delayed-stop callback preview is metadata-only; live callback invocation remains disabled."
			);
		}
	}

	private static void AddDelayedStopLiveBoundaryRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskDelayedStopCallbackPreviewRow row,
		PlayerProtectionActiveTaskDelayedStopCallbackPreview preview
	)
	{
		Add(
			rows,
			PlayerProtectionActiveTaskReadinessAggregateArea.VisualMutation,
			PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			blocksLiveEnablement: true,
			"Delayed-stop callback preview",
			row.JavaOperation,
			row.JavaSource,
			$"{row.Notes} InvokesCallback={preview.InvokesCallback}."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskReadinessAggregateArea.PacketFanout,
			PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			blocksLiveEnablement: true,
			"Delayed-stop callback preview",
			row.JavaOperation,
			row.JavaSource,
			$"{row.Notes} InvokesSocketFanout={preview.InvokesSocketFanout}."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskReadinessAggregateArea.AiMoveNotification,
			PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			blocksLiveEnablement: true,
			"Delayed-stop callback preview",
			row.JavaOperation,
			row.JavaSource,
			$"{row.Notes} InvokesAiMoveNotification={preview.InvokesAiMoveNotification}."
		);
	}

	private static void AddSchedulerCallbackPlanRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskSchedulerCallbackPlan plan
	)
	{
		foreach (var row in plan.Rows)
		{
			Add(
				rows,
				ToArea(row.Kind),
				ToStatus(row.Status),
				BlocksLiveEnablement(row),
				"Scheduler callback plan",
				row.JavaOperation,
				row.JavaSource,
				$"{row.Notes} DelayMilliseconds={plan.DelayMilliseconds}; InvokesScheduler={plan.InvokesScheduler}; InvokesCallback={plan.InvokesCallback}."
			);
		}

		if (!plan.InvokesScheduler && plan.Status == PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
				PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
				blocksLiveEnablement: true,
				"Scheduler callback plan",
				"ThreadPoolManager.getInstance().schedule(this::stopProtectionActiveTask, 60000)",
				plan.JavaSource,
				"Callback plan is metadata-only; live ThreadPoolManager.Schedule and delayed stopProtectionActiveTask invocation remain disabled."
			);
		}
	}

	private static void AddReadinessRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskLiveReadinessReport readiness
	)
	{
		foreach (var row in readiness.Rows)
		{
			var area = ToArea(row.Capability);
			var status = ToStatus(row.Status);
			var notes = row.BlockedReasons.Count == 0 ? row.Notes : string.Join(" ", row.BlockedReasons);

			Add(rows, area, status, row.BlocksAdditionalLiveSideEffects, "Live readiness report", row.JavaOperation, row.JavaSource, notes);
		}
	}

	private static void AddTaskMapAuditRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskTaskMapAuditReport audit
	)
	{
		foreach (
			var row in audit.Rows.Where(row =>
				row.Status is PlayerProtectionActiveTaskTaskMapAuditStatus.Gap or PlayerProtectionActiveTaskTaskMapAuditStatus.Requirement
			)
		)
		{
			Add(
				rows,
				ToArea(row.Area),
				PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
				blocksLiveEnablement: true,
				"Task-map audit",
				row.JavaBehavior,
				row.JavaSource,
				row.Requirement
			);
		}
	}

	private static void AddTaskMapSimulationRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		IReadOnlyList<PlayerProtectionActiveTaskTaskMapSimulationReport> simulations
	)
	{
		foreach (var simulation in simulations)
		{
			if (simulation.StoredScheduledTask)
			{
				Add(
					rows,
					PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapStorage,
					PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive,
					blocksLiveEnablement: false,
					"Task-map simulation",
					"addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)",
					simulation.JavaSource,
					"Non-live simulation stores a scheduled-task handle through the Java-shaped task-map adapter."
				);
			}

			if (simulation.CanceledExistingTask || simulation.RemovedMissingTaskAsNoOp)
			{
				Add(
					rows,
					PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapCancellation,
					PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive,
					blocksLiveEnablement: false,
					"Task-map simulation",
					"cancelTask(TaskId.PROTECTION_ACTIVE)",
					simulation.JavaSource,
					simulation.CanceledExistingTask
						? "Non-live simulation removes and cancels an existing task handle."
						: "Non-live simulation preserves Java missing-task cancel as a no-op."
				);
			}
		}
	}

	private static void AddScheduledHandleRow(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		bool scheduledTaskHandleAdapterAvailable
	)
	{
		Add(
			rows,
			PlayerProtectionActiveTaskReadinessAggregateArea.ScheduledTaskHandleAdapter,
			scheduledTaskHandleAdapterAvailable
				? PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive
				: PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			blocksLiveEnablement: !scheduledTaskHandleAdapterAvailable,
			"Scheduled-task handle adapter",
			"ThreadPoolManager.getInstance().schedule(...) returns ScheduledFuture<?>",
			"PlayerController.startProtectionActiveTask / ThreadPoolManager.schedule",
			scheduledTaskHandleAdapterAvailable
				? "C# wrapper exists for ScheduledTask, but Java Future.cancel(false) runtime comparison remains separate."
				: "C# has no ScheduledTask-to-task-map handle adapter."
		);
	}

	private static void AddLifecycleCleanupRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskTaskMapLifecycleCleanupReport cleanup
	)
	{
		foreach (var prerequisite in cleanup.RemainingPrerequisites)
		{
			Add(
				rows,
				prerequisite.Contains("owner", StringComparison.Ordinal)
					? PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection
					: PlayerProtectionActiveTaskReadinessAggregateArea.LifecycleCleanupHook,
				PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
				blocksLiveEnablement: true,
				"Lifecycle cleanup report",
				"onDelete() -> cancelAllTasks()",
				cleanup.JavaSource,
				prerequisite
			);
		}
	}

	private static void AddRuntimeComparisonRow(ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows)
	{
		Add(
			rows,
			PlayerProtectionActiveTaskReadinessAggregateArea.JavaRuntimeComparison,
			PlayerProtectionActiveTaskReadinessAggregateStatus.NeedsVerification,
			blocksLiveEnablement: true,
			"Runtime parity blocker",
			"Future.cancel(false) / ConcurrentHashMap.compute/remove/cancelAllTasks",
			"CreatureController.addTask/cancelTask/cancelAllTasks",
			"Java runtime artifact generation is still required before claiming scheduler/task-map parity."
		);
	}

	private static void AddOwnerPrototypeRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot snapshot
	)
	{
		Add(
			rows,
			PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection,
			PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive,
			blocksLiveEnablement: false,
			"Controller-owned owner prototype snapshot",
			"CreatureController.tasks owner-shaped prototype",
			snapshot.JavaSource,
			$"Non-live owner prototype exists for owner object id {snapshot.OwnerObjectId} with {snapshot.TaskCount} tracked protection task(s)."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection,
			PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			blocksLiveEnablement: true,
			"Controller-owned owner prototype snapshot",
			"future production CreatureController task-map owner",
			snapshot.JavaSource,
			"Prototype evidence does not prove production readiness because it is not wired to PlayerController, scheduler callbacks, or lifecycle cleanup."
		);
	}

	private static void AddOwnerSelectionRows(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskTaskMapOwnerSelectionReport ownerSelection
	)
	{
		foreach (var row in ownerSelection.Rows.Where(IsAggregateRelevantOwnerRow))
		{
			Add(
				rows,
				row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.LiveEnablementBlocker
					? PlayerProtectionActiveTaskReadinessAggregateArea.JavaRuntimeComparison
					: PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection,
				ToStatus(row.Status),
				row.BlocksLiveEnablement,
				"Owner selection report",
				row.JavaOperation,
				row.JavaSource,
				row.CSharpImplication + " " + row.Notes
			);
		}
	}

	private static bool IsAggregateRelevantOwnerRow(PlayerProtectionActiveTaskTaskMapOwnerSelectionRow row) =>
		row.Area
			is PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.ControllerOwnedCandidate
				or PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.PlayerModelOwnedCandidate
				or PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.ExternalServiceOwnedCandidate
				or PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.Recommendation
				or PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.LiveEnablementBlocker;

	private static bool BlocksLiveEnablement(PlayerProtectionActiveTaskSchedulerCallbackPlanRow row) =>
		row.Kind == PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RequireOwnerPrototype
		|| row.Kind == PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordRuntimeBlocker;

	private static bool BlocksLiveEnablement(PlayerProtectionActiveTaskDelayedStopCallbackPreviewRow row) =>
		row.Kind == PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordRuntimeBlocker
		|| row.Status == PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.BlockedMissingOwnerPrototype;

	private static PlayerProtectionActiveTaskReadinessAggregateArea ToArea(PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind kind) =>
		kind switch
		{
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RequireScheduledCallbackPlan =>
				PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordCallbackTarget =>
				PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.ComposeStopTaskOperationPlan =>
				PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapCancellation,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.CancelOwnerTask =>
				PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapCancellation,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordLiveSideEffectBoundary =>
				PlayerProtectionActiveTaskReadinessAggregateArea.VisualMutation,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordRuntimeBlocker =>
				PlayerProtectionActiveTaskReadinessAggregateArea.JavaRuntimeComparison,
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
		};

	private static PlayerProtectionActiveTaskReadinessAggregateArea ToArea(PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind kind) =>
		kind switch
		{
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.ObserveStartBranch => PlayerProtectionActiveTaskReadinessAggregateArea.BranchObservation,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RequireOwnerPrototype =>
				PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordScheduleCall => PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordCallbackTarget =>
				PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordTaskMapStorage => PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapStorage,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordRuntimeBlocker =>
				PlayerProtectionActiveTaskReadinessAggregateArea.JavaRuntimeComparison,
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
		};

	private static PlayerProtectionActiveTaskReadinessAggregateArea ToArea(PlayerProtectionActiveTaskLiveReadinessCapability capability) =>
		capability switch
		{
			PlayerProtectionActiveTaskLiveReadinessCapability.BranchObservation => PlayerProtectionActiveTaskReadinessAggregateArea.BranchObservation,
			PlayerProtectionActiveTaskLiveReadinessCapability.VisualMutation => PlayerProtectionActiveTaskReadinessAggregateArea.VisualMutation,
			PlayerProtectionActiveTaskLiveReadinessCapability.CastCancellation =>
				PlayerProtectionActiveTaskReadinessAggregateArea.KnownListCastCancellation,
			PlayerProtectionActiveTaskLiveReadinessCapability.TargetClear => PlayerProtectionActiveTaskReadinessAggregateArea.KnownListTargetClear,
			PlayerProtectionActiveTaskLiveReadinessCapability.PacketConstruction => PlayerProtectionActiveTaskReadinessAggregateArea.PacketConstruction,
			PlayerProtectionActiveTaskLiveReadinessCapability.PacketFanout => PlayerProtectionActiveTaskReadinessAggregateArea.PacketFanout,
			PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap => PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
			PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification => PlayerProtectionActiveTaskReadinessAggregateArea.AiMoveNotification,
			_ => throw new ArgumentOutOfRangeException(nameof(capability), capability, null),
		};

	private static PlayerProtectionActiveTaskReadinessAggregateArea ToArea(PlayerProtectionActiveTaskTaskMapAuditArea area) =>
		area switch
		{
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaSchedule => PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaTaskMapStorage => PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaLifecycleCleanup => PlayerProtectionActiveTaskReadinessAggregateArea.LifecycleCleanupHook,
			PlayerProtectionActiveTaskTaskMapAuditArea.CSharpTaskMapGap => PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection,
			PlayerProtectionActiveTaskTaskMapAuditArea.ReadinessGate => PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback,
			PlayerProtectionActiveTaskTaskMapAuditArea.ImplementationChecklist => PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection,
			_ => PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapStorage,
		};

	private static PlayerProtectionActiveTaskReadinessAggregateStatus ToStatus(PlayerProtectionActiveTaskLiveReadinessStatus status) =>
		status switch
		{
			PlayerProtectionActiveTaskLiveReadinessStatus.Ready => PlayerProtectionActiveTaskReadinessAggregateStatus.Ready,
			PlayerProtectionActiveTaskLiveReadinessStatus.Blocked => PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			PlayerProtectionActiveTaskLiveReadinessStatus.NotReached => PlayerProtectionActiveTaskReadinessAggregateStatus.Skipped,
			PlayerProtectionActiveTaskLiveReadinessStatus.SkippedBranch => PlayerProtectionActiveTaskReadinessAggregateStatus.Skipped,
			PlayerProtectionActiveTaskLiveReadinessStatus.LiveOnlyAllowed => PlayerProtectionActiveTaskReadinessAggregateStatus.LiveVisualOnly,
			_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
		};

	private static PlayerProtectionActiveTaskReadinessAggregateStatus ToStatus(PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus status) =>
		status switch
		{
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement => PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.PreferredCandidate => PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.RejectedCandidate => PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.Blocked => PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.NeedsVerification => PlayerProtectionActiveTaskReadinessAggregateStatus.NeedsVerification,
			_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
		};

	private static PlayerProtectionActiveTaskReadinessAggregateStatus ToStatus(PlayerProtectionActiveTaskSchedulerCallbackPlanStatus status) =>
		status switch
		{
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive => PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive,
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.SkippedAlreadyProtected => PlayerProtectionActiveTaskReadinessAggregateStatus.Skipped,
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.BlockedMissingOwnerPrototype =>
				PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
		};

	private static PlayerProtectionActiveTaskReadinessAggregateStatus ToStatus(PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus status) =>
		status switch
		{
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.PlannedNotLive => PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.MissingOwnerTaskNoOp =>
				PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.SkippedNoDelayedStop => PlayerProtectionActiveTaskReadinessAggregateStatus.Skipped,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.BlockedMissingOwnerPrototype =>
				PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked,
			_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
		};

	private static void Add(
		ICollection<PlayerProtectionActiveTaskReadinessAggregateRow> rows,
		PlayerProtectionActiveTaskReadinessAggregateArea area,
		PlayerProtectionActiveTaskReadinessAggregateStatus status,
		bool blocksLiveEnablement,
		string evidenceSource,
		string javaOperation,
		string javaSource,
		string notes
	)
	{
		rows.Add(
			new PlayerProtectionActiveTaskReadinessAggregateRow(
				rows.Count + 1,
				area,
				status,
				blocksLiveEnablement,
				evidenceSource,
				javaOperation,
				javaSource,
				notes
			)
		);
	}
}
