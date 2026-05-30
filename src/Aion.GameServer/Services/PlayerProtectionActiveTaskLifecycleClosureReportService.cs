namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskLifecycleClosurePrerequisite
{
	OwnerSelection,
	OwnerPrototype,
	SchedulerCallbackPlan,
	DelayedStopCallbackPreview,
	LifecycleCleanup,
	LiveSideEffects,
	RuntimeComparison,
}

public enum PlayerProtectionActiveTaskLifecycleClosureStatus
{
	ObservedNonLive,
	Skipped,
	Blocked,
	NeedsVerification,
}

public sealed record PlayerProtectionActiveTaskLifecycleClosureRow(
	int Order,
	PlayerProtectionActiveTaskLifecycleClosurePrerequisite Prerequisite,
	PlayerProtectionActiveTaskLifecycleClosureStatus Status,
	bool BlocksProductionEnablement,
	string EvidenceSource,
	string JavaSource,
	string Notes
);

public sealed record PlayerProtectionActiveTaskLifecycleClosureReport(
	PlayerProtectionActiveTaskAdapterAction Action,
	int PlayerObjectId,
	IReadOnlyList<PlayerProtectionActiveTaskLifecycleClosureRow> Rows,
	bool HasOwnerSelectionEvidence,
	bool HasSchedulerCallbackEvidence,
	bool HasDelayedStopPreviewEvidence,
	bool HasLifecycleCleanupEvidence,
	bool NeedsRuntimeComparison,
	bool CanEnableProductionProtectionLifecycle,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskLifecycleClosureReportService
{
	public static PlayerProtectionActiveTaskLifecycleClosureReport Create(PlayerProtectionActiveTaskReadinessAggregateReport aggregate)
	{
		// Java parity: live protection-task parity is only closed when owner selection, scheduler callback,
		// delayed stop preview, lifecycle cleanup, live side effects, and runtime comparison all line up.
		// This report turns the aggregate evidence into a production-closure checklist.
		var rows = new List<PlayerProtectionActiveTaskLifecycleClosureRow>();

		AddOwnerSelectionRows(rows, aggregate);
		AddOwnerPrototypeRows(rows, aggregate);
		AddSchedulerRows(rows, aggregate);
		AddDelayedPreviewRows(rows, aggregate);
		AddLifecycleRows(rows, aggregate);
		AddLiveSideEffectRows(rows, aggregate);
		AddRuntimeRows(rows, aggregate);

		var rowArray = rows.ToArray();
		var canEnable = rowArray.All(row => !row.BlocksProductionEnablement);

		return new PlayerProtectionActiveTaskLifecycleClosureReport(
			aggregate.Action,
			aggregate.PlayerObjectId,
			rowArray,
			HasOwnerSelectionEvidence: aggregate.Rows.Any(row => row.EvidenceSource == "Owner selection report"),
			HasSchedulerCallbackEvidence: aggregate.Rows.Any(row => row.EvidenceSource == "Scheduler callback plan"),
			HasDelayedStopPreviewEvidence: aggregate.Rows.Any(row => row.EvidenceSource == "Delayed-stop callback preview"),
			aggregate.HasLifecycleCleanupEvidence,
			NeedsRuntimeComparison: aggregate.BlockedAreas.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.JavaRuntimeComparison),
			CanEnableProductionProtectionLifecycle: canEnable,
			"PlayerController protection active lifecycle closure checklist",
			IsLive: false
		);
	}

	private static void AddOwnerSelectionRows(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskReadinessAggregateReport aggregate
	)
	{
		var selectionRows = aggregate.Rows.Where(row => row.EvidenceSource == "Owner selection report").ToArray();
		AddFromEvidence(
			rows,
			PlayerProtectionActiveTaskLifecycleClosurePrerequisite.OwnerSelection,
			selectionRows,
			"Owner selection report is required before enabling production task-map storage.",
			"PlayerController.startProtectionActiveTask / CreatureController.tasks"
		);
	}

	private static void AddOwnerPrototypeRows(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskReadinessAggregateReport aggregate
	)
	{
		var prototypeRows = aggregate.Rows.Where(row => row.EvidenceSource == "Controller-owned owner prototype snapshot").ToArray();
		AddFromEvidence(
			rows,
			PlayerProtectionActiveTaskLifecycleClosurePrerequisite.OwnerPrototype,
			prototypeRows,
			"Controller-owned owner prototype evidence is required before production wiring.",
			"CreatureController.tasks owner prototype"
		);
	}

	private static void AddSchedulerRows(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskReadinessAggregateReport aggregate
	)
	{
		var schedulerRows = aggregate.Rows.Where(row => row.EvidenceSource == "Scheduler callback plan").ToArray();
		AddFromEvidence(
			rows,
			PlayerProtectionActiveTaskLifecycleClosurePrerequisite.SchedulerCallbackPlan,
			schedulerRows,
			"Scheduler callback metadata is required before considering production delayed stop scheduling.",
			"PlayerController.startProtectionActiveTask / ThreadPoolManager.schedule"
		);
	}

	private static void AddDelayedPreviewRows(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskReadinessAggregateReport aggregate
	)
	{
		var previewRows = aggregate.Rows.Where(row => row.EvidenceSource == "Delayed-stop callback preview").ToArray();
		AddFromEvidence(
			rows,
			PlayerProtectionActiveTaskLifecycleClosurePrerequisite.DelayedStopCallbackPreview,
			previewRows,
			"Delayed-stop callback preview is required before enabling callback execution.",
			"PlayerController.stopProtectionActiveTask"
		);
	}

	private static void AddLifecycleRows(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskReadinessAggregateReport aggregate
	)
	{
		var lifecycleRows = aggregate.Rows.Where(row => row.EvidenceSource == "Lifecycle cleanup report").ToArray();
		AddFromEvidence(
			rows,
			PlayerProtectionActiveTaskLifecycleClosurePrerequisite.LifecycleCleanup,
			lifecycleRows,
			"Lifecycle cleanup must cancel stored protection tasks before production enablement.",
			"CreatureController.onDelete -> cancelAllTasks"
		);
	}

	private static void AddLiveSideEffectRows(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskReadinessAggregateReport aggregate
	)
	{
		var sideEffectRows = aggregate
			.Rows.Where(row =>
				row.Area
					is PlayerProtectionActiveTaskReadinessAggregateArea.VisualMutation
						or PlayerProtectionActiveTaskReadinessAggregateArea.PacketFanout
						or PlayerProtectionActiveTaskReadinessAggregateArea.AiMoveNotification
			)
			.ToArray();
		AddFromEvidence(
			rows,
			PlayerProtectionActiveTaskLifecycleClosurePrerequisite.LiveSideEffects,
			sideEffectRows,
			"Live visual mutation, packet fanout, and AI move-notification side effects must remain blocked until production paths exist.",
			"PlayerController.startProtectionActiveTask / stopProtectionActiveTask"
		);
	}

	private static void AddRuntimeRows(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskReadinessAggregateReport aggregate
	)
	{
		var runtimeRows = aggregate.Rows.Where(row => row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.JavaRuntimeComparison).ToArray();
		AddFromEvidence(
			rows,
			PlayerProtectionActiveTaskLifecycleClosurePrerequisite.RuntimeComparison,
			runtimeRows,
			"Java runtime comparison is required before claiming scheduler/future/concurrency parity.",
			"Future.cancel(false) / ScheduledFuture / ConcurrentHashMap"
		);
	}

	private static void AddFromEvidence(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskLifecycleClosurePrerequisite prerequisite,
		IReadOnlyList<PlayerProtectionActiveTaskReadinessAggregateRow> evidenceRows,
		string missingNotes,
		string javaSource
	)
	{
		if (evidenceRows.Count == 0)
		{
			Add(
				rows,
				prerequisite,
				PlayerProtectionActiveTaskLifecycleClosureStatus.Blocked,
				blocksProductionEnablement: true,
				"Readiness aggregate",
				javaSource,
				missingNotes
			);
			return;
		}

		var status =
			evidenceRows.Any(row => row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked)
				? PlayerProtectionActiveTaskLifecycleClosureStatus.Blocked
			: evidenceRows.Any(row => row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.NeedsVerification)
				? PlayerProtectionActiveTaskLifecycleClosureStatus.NeedsVerification
			: evidenceRows.All(row => row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.Skipped)
				? PlayerProtectionActiveTaskLifecycleClosureStatus.Skipped
			: PlayerProtectionActiveTaskLifecycleClosureStatus.ObservedNonLive;
		var blocks =
			evidenceRows.Any(row => row.BlocksLiveEnablement)
			|| status is PlayerProtectionActiveTaskLifecycleClosureStatus.Blocked or PlayerProtectionActiveTaskLifecycleClosureStatus.NeedsVerification;

		Add(
			rows,
			prerequisite,
			status,
			blocks,
			string.Join(", ", evidenceRows.Select(row => row.EvidenceSource).Distinct()),
			string.Join(" | ", evidenceRows.Select(row => row.JavaSource).Distinct()),
			string.Join(" ", evidenceRows.Select(row => row.Notes).Distinct())
		);
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskLifecycleClosureRow> rows,
		PlayerProtectionActiveTaskLifecycleClosurePrerequisite prerequisite,
		PlayerProtectionActiveTaskLifecycleClosureStatus status,
		bool blocksProductionEnablement,
		string evidenceSource,
		string javaSource,
		string notes
	)
	{
		rows.Add(
			new PlayerProtectionActiveTaskLifecycleClosureRow(
				rows.Count + 1,
				prerequisite,
				status,
				blocksProductionEnablement,
				evidenceSource,
				javaSource,
				notes
			)
		);
	}
}
