namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus
{
	PlannedNotLive,
	MissingOwnerTaskNoOp,
	SkippedNoDelayedStop,
	BlockedMissingOwnerPrototype,
}

public enum PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind
{
	RequireScheduledCallbackPlan,
	RecordCallbackTarget,
	ComposeStopTaskOperationPlan,
	CancelOwnerTask,
	RecordLiveSideEffectBoundary,
	RecordRuntimeBlocker,
}

public sealed record PlayerProtectionActiveTaskDelayedStopCallbackPreviewRequest(
	PlayerProtectionActiveTaskSchedulerCallbackPlan SchedulerCallbackPlan,
	PlayerProtectionActiveTaskTaskOperationPlan StopTaskOperationPlan,
	PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService? OwnerPrototype = null
);

public sealed record PlayerProtectionActiveTaskDelayedStopCallbackPreviewRow(
	int Order,
	PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind Kind,
	PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus Status,
	bool JavaCallReached,
	bool IsLive,
	string JavaOperation,
	string JavaSource,
	string Notes
);

public sealed record PlayerProtectionActiveTaskDelayedStopCallbackPreview(
	PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus Status,
	int PlayerObjectId,
	IReadOnlyList<PlayerProtectionActiveTaskDelayedStopCallbackPreviewRow> Rows,
	bool HasScheduledCallbackMetadata,
	bool ComposesStopTaskOperationPlan,
	bool CancelsOwnerTask,
	bool RemovesMissingTaskAsNoOp,
	bool InvokesScheduler,
	bool InvokesCallback,
	bool InvokesSocketFanout,
	bool InvokesAiMoveNotification,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskDelayedStopCallbackPreviewService
{
	public static PlayerProtectionActiveTaskDelayedStopCallbackPreview Create(PlayerProtectionActiveTaskDelayedStopCallbackPreviewRequest request)
	{
		// Java parity: the delayed callback scheduled by startProtectionActiveTask eventually runs
		// stopProtectionActiveTask. This preview records that callback chain and its task cancellation
		// consequences without invoking scheduler, fanout, or AI side effects.
		var rows = new List<PlayerProtectionActiveTaskDelayedStopCallbackPreviewRow>();

		if (!request.SchedulerCallbackPlan.SchedulesDelayedStop)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RequireScheduledCallbackPlan,
				PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.SkippedNoDelayedStop,
				javaCallReached: false,
				"is scheduled callback present",
				request.SchedulerCallbackPlan.JavaSource,
				"Scheduler callback plan did not schedule a delayed stop, so no delayed callback preview is composed."
			);

			return CreateReport(
				request,
				PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.SkippedNoDelayedStop,
				rows,
				hasScheduledCallbackMetadata: false,
				composesStopTaskOperationPlan: false,
				cancelsOwnerTask: false,
				removesMissingTaskAsNoOp: false
			);
		}

		Add(
			rows,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordCallbackTarget,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.PlannedNotLive,
			javaCallReached: true,
			"this::stopProtectionActiveTask",
			"PlayerController.startProtectionActiveTask -> ThreadPoolManager.schedule(this::stopProtectionActiveTask, 60000)",
			"Delayed callback target is recorded as metadata only; the callback is not invoked."
		);

		Add(
			rows,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.ComposeStopTaskOperationPlan,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.PlannedNotLive,
			javaCallReached: request.StopTaskOperationPlan.CancelsExistingTask || request.StopTaskOperationPlan.RemovesMissingTaskAsNoOp,
			"stopProtectionActiveTask -> cancelTask(TaskId.PROTECTION_ACTIVE)",
			request.StopTaskOperationPlan.JavaSource,
			$"Stop task-operation plan is composed with status {request.StopTaskOperationPlan.SourcePlanStatus}; IsLive={request.StopTaskOperationPlan.IsLive}."
		);

		if (request.OwnerPrototype == null)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.CancelOwnerTask,
				PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.BlockedMissingOwnerPrototype,
				javaCallReached: true,
				"cancelTask(TaskId.PROTECTION_ACTIVE)",
				"CreatureController.cancelTask",
				"Owner prototype is required before previewing non-live delayed-stop cancellation."
			);

			return CreateReport(
				request,
				PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.BlockedMissingOwnerPrototype,
				rows,
				hasScheduledCallbackMetadata: true,
				composesStopTaskOperationPlan: true,
				cancelsOwnerTask: false,
				removesMissingTaskAsNoOp: false
			);
		}

		var cancelResult = request.OwnerPrototype.CancelTask();
		var missingNoOp = cancelResult.Status == PlayerProtectionActiveTaskTaskMapOperationStatus.Missing;
		Add(
			rows,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.CancelOwnerTask,
			missingNoOp
				? PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.MissingOwnerTaskNoOp
				: PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.PlannedNotLive,
			javaCallReached: true,
			cancelResult.JavaOperation,
			cancelResult.JavaSource,
			$"{cancelResult.Notes} OwnerObjectId={request.OwnerPrototype.OwnerObjectId}; CanceledTask={cancelResult.CanceledTask}; IsLive={request.OwnerPrototype.IsLive}."
		);

		Add(
			rows,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordLiveSideEffectBoundary,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.PlannedNotLive,
			javaCallReached: true,
			"if (player.isSpawned()) unset BLINKING; broadcast SM_PLAYER_STATE; notifyAIOnMove()",
			"PlayerController.stopProtectionActiveTask",
			"Preview does not mutate visual state, broadcast sockets, or notify AI movement."
		);

		Add(
			rows,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordRuntimeBlocker,
			PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.PlannedNotLive,
			javaCallReached: true,
			"ScheduledFuture callback execution / Future.cancel(false)",
			"ThreadPoolManager.schedule / CreatureController.cancelTask",
			"Java scheduled callback timing and Future cancellation behavior still require runtime comparison."
		);

		return CreateReport(
			request,
			missingNoOp
				? PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.MissingOwnerTaskNoOp
				: PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.PlannedNotLive,
			rows,
			hasScheduledCallbackMetadata: true,
			composesStopTaskOperationPlan: true,
			cancelsOwnerTask: cancelResult.CanceledTask,
			removesMissingTaskAsNoOp: missingNoOp
		);
	}

	private static PlayerProtectionActiveTaskDelayedStopCallbackPreview CreateReport(
		PlayerProtectionActiveTaskDelayedStopCallbackPreviewRequest request,
		PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus status,
		IReadOnlyList<PlayerProtectionActiveTaskDelayedStopCallbackPreviewRow> rows,
		bool hasScheduledCallbackMetadata,
		bool composesStopTaskOperationPlan,
		bool cancelsOwnerTask,
		bool removesMissingTaskAsNoOp
	) =>
		new(
			status,
			request.SchedulerCallbackPlan.PlayerObjectId,
			rows,
			hasScheduledCallbackMetadata,
			composesStopTaskOperationPlan,
			cancelsOwnerTask,
			removesMissingTaskAsNoOp,
			InvokesScheduler: false,
			InvokesCallback: false,
			InvokesSocketFanout: false,
			InvokesAiMoveNotification: false,
			"PlayerController.startProtectionActiveTask delayed stop callback preview -> stopProtectionActiveTask",
			IsLive: false
		);

	private static void Add(
		ICollection<PlayerProtectionActiveTaskDelayedStopCallbackPreviewRow> rows,
		PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind kind,
		PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus status,
		bool javaCallReached,
		string javaOperation,
		string javaSource,
		string notes
	)
	{
		rows.Add(
			new PlayerProtectionActiveTaskDelayedStopCallbackPreviewRow(
				rows.Count + 1,
				kind,
				status,
				javaCallReached,
				IsLive: false,
				javaOperation,
				javaSource,
				notes
			)
		);
	}
}
