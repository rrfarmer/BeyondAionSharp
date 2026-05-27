namespace Aion.GameServer.Services;

public sealed record PlayerProtectionActiveTaskTaskMapSimulationRequest(
	PlayerProtectionActiveTaskTaskOperationPlan TaskOperationPlan,
	IPlayerProtectionActiveTaskTaskHandle? ScheduledTaskHandle = null,
	IPlayerProtectionActiveTaskTaskHandle? ExistingTaskHandle = null,
	bool RunCancelAllAfterPlan = false);

public sealed record PlayerProtectionActiveTaskTaskMapSimulationRow(
	int Order,
	PlayerProtectionActiveTaskTaskOperation SourceOperation,
	PlayerProtectionActiveTaskTaskOperationStatus SourceStatus,
	PlayerProtectionActiveTaskTaskMapOperationResult AdapterResult,
	string JavaSource,
	string Notes);

public sealed record PlayerProtectionActiveTaskTaskMapSimulationReport(
	PlayerProtectionActiveTaskPlanStatus SourcePlanStatus,
	IReadOnlyList<PlayerProtectionActiveTaskTaskMapSimulationRow> Rows,
	PlayerProtectionActiveTaskTaskMapSnapshot FinalSnapshot,
	bool UsedExistingTaskHandle,
	bool UsedScheduledTaskHandle,
	bool CanceledExistingTask,
	bool StoredScheduledTask,
	bool RemovedMissingTaskAsNoOp,
	bool RanCancelAllAfterPlan,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskTaskMapSimulationService
{
	public static PlayerProtectionActiveTaskTaskMapSimulationReport Create(
		PlayerProtectionActiveTaskTaskMapSimulationRequest request)
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var usedExistingTaskHandle = request.ExistingTaskHandle != null;
		if (request.ExistingTaskHandle != null)
			adapter.AddTask(request.TaskOperationPlan.TaskIdOrdinal, request.TaskOperationPlan.TaskIdName, request.ExistingTaskHandle);

		var rows = new List<PlayerProtectionActiveTaskTaskMapSimulationRow>();
		var scheduledHandle = request.ScheduledTaskHandle ?? new NonLiveTaskHandle();

		foreach (var taskRow in request.TaskOperationPlan.Rows)
		{
			if (!taskRow.JavaCallReached)
				continue;

			switch (taskRow.Operation)
			{
				case PlayerProtectionActiveTaskTaskOperation.ScheduleDelayedStop:
					// Java schedule returns a Future that is consumed by the following addTask call.
					break;
				case PlayerProtectionActiveTaskTaskOperation.AddTaskAndMaybeReplaceExisting:
					AddRow(
						rows,
						taskRow,
						adapter.AddTask(request.TaskOperationPlan.TaskIdOrdinal, request.TaskOperationPlan.TaskIdName, scheduledHandle),
						"Simulated addTask stores the supplied scheduled-task handle; no scheduler callback was created.");
					break;
				case PlayerProtectionActiveTaskTaskOperation.CancelTask:
					AddRow(
						rows,
						taskRow,
						adapter.CancelTask(request.TaskOperationPlan.TaskIdOrdinal, request.TaskOperationPlan.TaskIdName),
						"Simulated cancelTask removes before Cancel(false), matching Java source order.");
					break;
				case PlayerProtectionActiveTaskTaskOperation.NoTaskOperation:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(taskRow.Operation), taskRow.Operation, null);
			}
		}

		if (request.RunCancelAllAfterPlan)
		{
			var cancelAll = adapter.CancelAllTasks();
			rows.Add(new PlayerProtectionActiveTaskTaskMapSimulationRow(
				rows.Count + 1,
				PlayerProtectionActiveTaskTaskOperation.CancelTask,
				PlayerProtectionActiveTaskTaskOperationStatus.PlannedNotLive,
				cancelAll,
				"CreatureController.cancelAllTasks / onDelete",
				"Optional cleanup simulation cancels every remaining task handle and clears the map."));
		}

		var rowArray = rows.ToArray();
		return new PlayerProtectionActiveTaskTaskMapSimulationReport(
			request.TaskOperationPlan.SourcePlanStatus,
			rowArray,
			adapter.CreateSnapshot(),
			usedExistingTaskHandle,
			request.ScheduledTaskHandle != null,
			rowArray.Any(row => row.AdapterResult.CanceledTask),
			rowArray.Any(row => row.AdapterResult.StoredTask),
			rowArray.Any(row => row.AdapterResult.Status == PlayerProtectionActiveTaskTaskMapOperationStatus.Missing),
			request.RunCancelAllAfterPlan,
			"PlayerProtectionActiveTaskTaskOperationPlan -> non-live CreatureController task-map adapter simulation",
			IsLive: false);
	}

	private static void AddRow(
		ICollection<PlayerProtectionActiveTaskTaskMapSimulationRow> rows,
		PlayerProtectionActiveTaskTaskOperationRow taskRow,
		PlayerProtectionActiveTaskTaskMapOperationResult adapterResult,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskTaskMapSimulationRow(
			rows.Count + 1,
			taskRow.Operation,
			taskRow.Status,
			adapterResult,
			taskRow.JavaSource,
			notes));
	}

	private sealed class NonLiveTaskHandle : IPlayerProtectionActiveTaskTaskHandle
	{
		public bool IsDone { get; private set; }

		public bool Cancel(bool mayInterruptIfRunning)
		{
			if (IsDone)
				return false;

			IsDone = true;
			return true;
		}
	}
}
