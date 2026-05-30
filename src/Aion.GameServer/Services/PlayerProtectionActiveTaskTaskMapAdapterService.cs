namespace Aion.GameServer.Services;

public interface IPlayerProtectionActiveTaskTaskHandle
{
	bool IsDone { get; }

	bool Cancel(bool mayInterruptIfRunning);
}

public enum PlayerProtectionActiveTaskTaskMapOperation
{
	HasTask,
	HasScheduledTask,
	GetAndRemoveTask,
	CancelTask,
	CancelTaskIfPresent,
	AddTask,
	CancelAllTasks,
}

public enum PlayerProtectionActiveTaskTaskMapOperationStatus
{
	Present,
	Missing,
	Scheduled,
	Done,
	Stored,
	ReplacedExistingTask,
	RemovedExistingTask,
	ConditionalMatchCanceled,
	ConditionalMismatchNoOp,
	CanceledAll,
	NoTasksToCancel,
}

public sealed record PlayerProtectionActiveTaskTaskMapOperationResult(
	PlayerProtectionActiveTaskTaskMapOperation Operation,
	PlayerProtectionActiveTaskTaskMapOperationStatus Status,
	int TaskIdOrdinal,
	string TaskIdName,
	bool ExistingTaskPresentBeforeOperation,
	bool RemovedTask,
	bool StoredTask,
	bool CanceledTask,
	int CanceledTaskCount,
	IPlayerProtectionActiveTaskTaskHandle? RemovedTaskHandle,
	string JavaOperation,
	string JavaSource,
	string Notes
);

public sealed record PlayerProtectionActiveTaskTaskMapSnapshot(
	IReadOnlyList<int> TaskIdOrdinals,
	IReadOnlyList<string> TaskIdNames,
	int Count,
	string JavaSource
);

public sealed class PlayerProtectionActiveTaskTaskMapAdapterService
{
	private readonly Dictionary<int, StoredTask> _tasks = [];
	private readonly object _gate = new();

	public PlayerProtectionActiveTaskTaskMapOperationResult HasTask(int taskIdOrdinal, string taskIdName)
	{
		// Java parity: CreatureController task-map helpers own scheduled-task lookup, replacement, cancel,
		// and remove behavior. This adapter mirrors those task-id operations over a local non-live handle map.
		lock (_gate)
		{
			var present = _tasks.ContainsKey(taskIdOrdinal);
			return Result(
				PlayerProtectionActiveTaskTaskMapOperation.HasTask,
				present ? PlayerProtectionActiveTaskTaskMapOperationStatus.Present : PlayerProtectionActiveTaskTaskMapOperationStatus.Missing,
				taskIdOrdinal,
				taskIdName,
				existingBefore: present,
				removed: false,
				stored: false,
				canceled: false,
				canceledCount: 0,
				removedTaskHandle: null,
				"tasks.containsKey(taskId.ordinal())",
				"CreatureController.hasTask",
				present ? "Task id is present in the non-live adapter map." : "Task id is absent from the non-live adapter map."
			);
		}
	}

	public PlayerProtectionActiveTaskTaskMapOperationResult HasScheduledTask(int taskIdOrdinal, string taskIdName)
	{
		lock (_gate)
		{
			var present = _tasks.TryGetValue(taskIdOrdinal, out var storedTask);
			var scheduled = present && !storedTask!.Handle.IsDone;
			return Result(
				PlayerProtectionActiveTaskTaskMapOperation.HasScheduledTask,
				scheduled ? PlayerProtectionActiveTaskTaskMapOperationStatus.Scheduled
					: present ? PlayerProtectionActiveTaskTaskMapOperationStatus.Done
					: PlayerProtectionActiveTaskTaskMapOperationStatus.Missing,
				taskIdOrdinal,
				taskIdName,
				existingBefore: present,
				removed: false,
				stored: false,
				canceled: false,
				canceledCount: 0,
				removedTaskHandle: null,
				"Future<?> task = tasks.get(taskId.ordinal()); return task != null && !task.isDone()",
				"CreatureController.hasScheduledTask",
				scheduled ? "Stored handle is not done." : "Missing or done handle is not considered scheduled."
			);
		}
	}

	public PlayerProtectionActiveTaskTaskMapOperationResult GetAndRemoveTask(int taskIdOrdinal, string taskIdName)
	{
		lock (_gate)
		{
			var removed = _tasks.Remove(taskIdOrdinal, out var storedTask);
			return Result(
				PlayerProtectionActiveTaskTaskMapOperation.GetAndRemoveTask,
				removed ? PlayerProtectionActiveTaskTaskMapOperationStatus.RemovedExistingTask : PlayerProtectionActiveTaskTaskMapOperationStatus.Missing,
				taskIdOrdinal,
				taskIdName,
				existingBefore: removed,
				removed,
				stored: false,
				canceled: false,
				canceledCount: 0,
				removed ? storedTask!.Handle : null,
				"tasks.remove(taskId.ordinal())",
				"CreatureController.getAndRemoveTask",
				removed ? "Task was removed without cancellation." : "Missing task removal returned null."
			);
		}
	}

	public PlayerProtectionActiveTaskTaskMapOperationResult CancelTask(int taskIdOrdinal, string taskIdName)
	{
		lock (_gate)
		{
			var removed = _tasks.Remove(taskIdOrdinal, out var storedTask);
			var canceled = removed && storedTask!.Handle.Cancel(mayInterruptIfRunning: false);
			return Result(
				PlayerProtectionActiveTaskTaskMapOperation.CancelTask,
				removed ? PlayerProtectionActiveTaskTaskMapOperationStatus.RemovedExistingTask : PlayerProtectionActiveTaskTaskMapOperationStatus.Missing,
				taskIdOrdinal,
				taskIdName,
				existingBefore: removed,
				removed,
				stored: false,
				canceled,
				canceled ? 1 : 0,
				removed ? storedTask!.Handle : null,
				"Future<?> task = getAndRemoveTask(taskId); if (task != null) task.cancel(false); return task",
				"CreatureController.cancelTask",
				removed ? "Task was removed before cancel(false)." : "Missing task cancel is a no-op."
			);
		}
	}

	public PlayerProtectionActiveTaskTaskMapOperationResult CancelTaskIfPresent(
		int taskIdOrdinal,
		string taskIdName,
		IPlayerProtectionActiveTaskTaskHandle expectedTask
	)
	{
		lock (_gate)
		{
			var existingBefore = _tasks.TryGetValue(taskIdOrdinal, out var storedTask);
			var matches = existingBefore && ReferenceEquals(storedTask!.Handle, expectedTask);
			if (!matches)
			{
				return Result(
					PlayerProtectionActiveTaskTaskMapOperation.CancelTaskIfPresent,
					PlayerProtectionActiveTaskTaskMapOperationStatus.ConditionalMismatchNoOp,
					taskIdOrdinal,
					taskIdName,
					existingBefore,
					removed: false,
					stored: false,
					canceled: false,
					canceledCount: 0,
					removedTaskHandle: null,
					"tasks.remove(taskId.ordinal(), task)",
					"CreatureController.cancelTaskIfPresent",
					"Stored task did not match the supplied handle, so Java would not remove or cancel."
				);
			}

			_tasks.Remove(taskIdOrdinal);
			var canceled = expectedTask.Cancel(mayInterruptIfRunning: false);
			return Result(
				PlayerProtectionActiveTaskTaskMapOperation.CancelTaskIfPresent,
				PlayerProtectionActiveTaskTaskMapOperationStatus.ConditionalMatchCanceled,
				taskIdOrdinal,
				taskIdName,
				existingBefore: true,
				removed: true,
				stored: false,
				canceled,
				canceled ? 1 : 0,
				expectedTask,
				"if (tasks.remove(taskId.ordinal(), task)) task.cancel(false)",
				"CreatureController.cancelTaskIfPresent",
				"Stored task matched the supplied handle and was removed before cancel(false)."
			);
		}
	}

	public PlayerProtectionActiveTaskTaskMapOperationResult AddTask(int taskIdOrdinal, string taskIdName, IPlayerProtectionActiveTaskTaskHandle task)
	{
		lock (_gate)
		{
			var replaced = _tasks.TryGetValue(taskIdOrdinal, out var oldTask);
			var canceled = replaced && oldTask!.Handle.Cancel(mayInterruptIfRunning: false);
			_tasks[taskIdOrdinal] = new StoredTask(taskIdName, task);
			return Result(
				PlayerProtectionActiveTaskTaskMapOperation.AddTask,
				replaced ? PlayerProtectionActiveTaskTaskMapOperationStatus.ReplacedExistingTask : PlayerProtectionActiveTaskTaskMapOperationStatus.Stored,
				taskIdOrdinal,
				taskIdName,
				existingBefore: replaced,
				removed: replaced,
				stored: true,
				canceled,
				canceled ? 1 : 0,
				replaced ? oldTask!.Handle : null,
				"tasks.compute(taskId.ordinal(), (k, oldTask) -> { if (oldTask != null) oldTask.cancel(false); return task; })",
				"CreatureController.addTask",
				replaced ? "Old task was canceled before the new handle was stored." : "New handle was stored for the task id."
			);
		}
	}

	public PlayerProtectionActiveTaskTaskMapOperationResult CancelAllTasks()
	{
		lock (_gate)
		{
			var canceledCount = 0;
			foreach (var storedTask in _tasks.Values)
			{
				if (storedTask.Handle.Cancel(mayInterruptIfRunning: false))
					canceledCount++;
			}

			var hadTasks = _tasks.Count > 0;
			_tasks.Clear();
			return Result(
				PlayerProtectionActiveTaskTaskMapOperation.CancelAllTasks,
				hadTasks ? PlayerProtectionActiveTaskTaskMapOperationStatus.CanceledAll : PlayerProtectionActiveTaskTaskMapOperationStatus.NoTasksToCancel,
				taskIdOrdinal: -1,
				taskIdName: "ALL",
				existingBefore: hadTasks,
				removed: hadTasks,
				stored: false,
				canceled: canceledCount > 0,
				canceledCount,
				removedTaskHandle: null,
				"for (Entry<Integer, Future<?>> e : tasks.entrySet()) task.cancel(false); tasks.clear()",
				"CreatureController.cancelAllTasks",
				hadTasks ? "All stored task handles were canceled then the map was cleared." : "Cancel-all on an empty task map is a no-op."
			);
		}
	}

	public PlayerProtectionActiveTaskTaskMapSnapshot CreateSnapshot()
	{
		lock (_gate)
		{
			var ordered = _tasks.OrderBy(pair => pair.Key).Select(pair => (TaskIdOrdinal: pair.Key, pair.Value.TaskIdName)).ToArray();
			return new PlayerProtectionActiveTaskTaskMapSnapshot(
				ordered.Select(pair => pair.TaskIdOrdinal).ToArray(),
				ordered.Select(pair => pair.TaskIdName).ToArray(),
				_tasks.Count,
				"CreatureController.tasks snapshot for non-live protection task-map adapter"
			);
		}
	}

	private static PlayerProtectionActiveTaskTaskMapOperationResult Result(
		PlayerProtectionActiveTaskTaskMapOperation operation,
		PlayerProtectionActiveTaskTaskMapOperationStatus status,
		int taskIdOrdinal,
		string taskIdName,
		bool existingBefore,
		bool removed,
		bool stored,
		bool canceled,
		int canceledCount,
		IPlayerProtectionActiveTaskTaskHandle? removedTaskHandle,
		string javaOperation,
		string javaSource,
		string notes
	) =>
		new(
			operation,
			status,
			taskIdOrdinal,
			taskIdName,
			existingBefore,
			removed,
			stored,
			canceled,
			canceledCount,
			removedTaskHandle,
			javaOperation,
			javaSource,
			notes
		);

	private sealed record StoredTask(string TaskIdName, IPlayerProtectionActiveTaskTaskHandle Handle);
}
