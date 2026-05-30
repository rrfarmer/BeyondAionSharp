namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskTaskOperation
{
	NoTaskOperation,
	ScheduleDelayedStop,
	AddTaskAndMaybeReplaceExisting,
	CancelTask,
}

public enum PlayerProtectionActiveTaskTaskOperationStatus
{
	SkippedNoOpBranch,
	PlannedNotLive,
	WouldReplaceExistingTask,
	WouldCancelExistingTask,
	WouldRemoveMissingTaskNoOp,
}

public sealed record PlayerProtectionActiveTaskTaskOperationRow(
	int Order,
	PlayerProtectionActiveTaskTaskOperation Operation,
	PlayerProtectionActiveTaskTaskOperationStatus Status,
	bool JavaCallReached,
	bool ExistingTaskPresentBeforeOperation,
	bool WouldCancelExistingTask,
	bool WouldStoreNewTask,
	bool IsLive,
	string JavaOperation,
	string JavaSource,
	string Notes
);

public sealed record PlayerProtectionActiveTaskTaskOperationPlan(
	PlayerProtectionActiveTaskPlanStatus SourcePlanStatus,
	string TaskIdName,
	int TaskIdOrdinal,
	int DelayMilliseconds,
	IReadOnlyList<PlayerProtectionActiveTaskTaskOperationRow> Rows,
	bool SchedulesDelayedStop,
	bool StoresTask,
	bool ReplacesExistingTask,
	bool CancelsExistingTask,
	bool RemovesMissingTaskAsNoOp,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskTaskOperationPlanService
{
	public static PlayerProtectionActiveTaskTaskOperationPlan Create(PlayerProtectionActiveTaskPlan plan, bool existingProtectionTaskPresent)
	{
		// Java parity: startProtectionActiveTask schedules and stores PROTECTION_ACTIVE, while
		// stopProtectionActiveTask cancels that task through CreatureController task-map helpers.
		// This planner keeps those task operations explicit without creating live futures.
		var rows = CreateRows(plan, existingProtectionTaskPresent).ToArray();

		return new PlayerProtectionActiveTaskTaskOperationPlan(
			plan.Status,
			plan.TaskIdName,
			plan.TaskIdOrdinal,
			plan.DelayMilliseconds,
			rows,
			SchedulesDelayedStop: rows.Any(row => row.Operation == PlayerProtectionActiveTaskTaskOperation.ScheduleDelayedStop && row.JavaCallReached),
			StoresTask: rows.Any(row => row.WouldStoreNewTask),
			ReplacesExistingTask: rows.Any(row => row.Status == PlayerProtectionActiveTaskTaskOperationStatus.WouldReplaceExistingTask),
			CancelsExistingTask: rows.Any(row => row.Status == PlayerProtectionActiveTaskTaskOperationStatus.WouldCancelExistingTask),
			RemovesMissingTaskAsNoOp: rows.Any(row => row.Status == PlayerProtectionActiveTaskTaskOperationStatus.WouldRemoveMissingTaskNoOp),
			"CreatureController.addTask/cancelTask with PlayerController protection active task",
			IsLive: false
		);
	}

	private static IEnumerable<PlayerProtectionActiveTaskTaskOperationRow> CreateRows(
		PlayerProtectionActiveTaskPlan plan,
		bool existingProtectionTaskPresent
	)
	{
		if (plan.Status == PlayerProtectionActiveTaskPlanStatus.AlreadyProtected)
		{
			yield return Row(
				0,
				PlayerProtectionActiveTaskTaskOperation.NoTaskOperation,
				PlayerProtectionActiveTaskTaskOperationStatus.SkippedNoOpBranch,
				javaCallReached: false,
				existingProtectionTaskPresent,
				wouldCancelExistingTask: false,
				wouldStoreNewTask: false,
				"no task operation",
				"PlayerController.startProtectionActiveTask -> already protected branch returns before ThreadPoolManager.schedule/addTask",
				"Already protected start does not schedule, replace, or cancel protection active tasks."
			);
			yield break;
		}

		if (plan.ShouldScheduleTask)
		{
			yield return Row(
				0,
				PlayerProtectionActiveTaskTaskOperation.ScheduleDelayedStop,
				PlayerProtectionActiveTaskTaskOperationStatus.PlannedNotLive,
				javaCallReached: true,
				existingProtectionTaskPresent,
				wouldCancelExistingTask: false,
				wouldStoreNewTask: false,
				"ThreadPoolManager.getInstance().schedule(this::stopProtectionActiveTask, 60000)",
				"PlayerController.startProtectionActiveTask",
				"Delay is modeled but no C# scheduled task is created."
			);
			yield return Row(
				1,
				PlayerProtectionActiveTaskTaskOperation.AddTaskAndMaybeReplaceExisting,
				existingProtectionTaskPresent
					? PlayerProtectionActiveTaskTaskOperationStatus.WouldReplaceExistingTask
					: PlayerProtectionActiveTaskTaskOperationStatus.PlannedNotLive,
				javaCallReached: true,
				existingProtectionTaskPresent,
				existingProtectionTaskPresent,
				wouldStoreNewTask: true,
				"addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)",
				"CreatureController.addTask -> tasks.compute(taskId.ordinal(), cancel old Future if present, store new Future)",
				existingProtectionTaskPresent
					? "Java would cancel the previous future before storing the new protection task."
					: "Java would store the new scheduled protection task under TaskId.PROTECTION_ACTIVE."
			);
			yield break;
		}

		if (plan.Status is PlayerProtectionActiveTaskPlanStatus.StopProtection or PlayerProtectionActiveTaskPlanStatus.StopProtectionUnspawned)
		{
			yield return Row(
				0,
				PlayerProtectionActiveTaskTaskOperation.CancelTask,
				existingProtectionTaskPresent
					? PlayerProtectionActiveTaskTaskOperationStatus.WouldCancelExistingTask
					: PlayerProtectionActiveTaskTaskOperationStatus.WouldRemoveMissingTaskNoOp,
				javaCallReached: true,
				existingProtectionTaskPresent,
				existingProtectionTaskPresent,
				wouldStoreNewTask: false,
				"cancelTask(TaskId.PROTECTION_ACTIVE)",
				"CreatureController.cancelTask -> tasks.remove(taskId.ordinal()); if task != null task.cancel(false); return task",
				existingProtectionTaskPresent
					? "Java would remove and cancel the existing protection task before spawned-side effects."
					: "Java still calls cancelTask, but missing task removal returns null and cancels nothing."
			);
		}
	}

	private static PlayerProtectionActiveTaskTaskOperationRow Row(
		int order,
		PlayerProtectionActiveTaskTaskOperation operation,
		PlayerProtectionActiveTaskTaskOperationStatus status,
		bool javaCallReached,
		bool existingProtectionTaskPresent,
		bool wouldCancelExistingTask,
		bool wouldStoreNewTask,
		string javaOperation,
		string javaSource,
		string notes
	) =>
		new(
			order,
			operation,
			status,
			javaCallReached,
			existingProtectionTaskPresent,
			wouldCancelExistingTask,
			wouldStoreNewTask,
			IsLive: false,
			javaOperation,
			javaSource,
			notes
		);
}
