using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskTaskMapAdapterServiceTests
{
	[Fact]
	public void AddTask_StoresNewHandleAndReportsPresence()
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var handle = new RecordingTaskHandle();

		var add = adapter.AddTask(ProtectionActiveOrdinal, ProtectionActiveName, handle);
		var hasTask = adapter.HasTask(ProtectionActiveOrdinal, ProtectionActiveName);
		var hasScheduled = adapter.HasScheduledTask(ProtectionActiveOrdinal, ProtectionActiveName);
		var snapshot = adapter.CreateSnapshot();

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Stored, add.Status);
		Assert.False(add.ExistingTaskPresentBeforeOperation);
		Assert.True(add.StoredTask);
		Assert.False(add.CanceledTask);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Present, hasTask.Status);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Scheduled, hasScheduled.Status);
		Assert.Equal([ProtectionActiveOrdinal], snapshot.TaskIdOrdinals);
		Assert.Equal([ProtectionActiveName], snapshot.TaskIdNames);
		Assert.Equal(1, snapshot.Count);
	}

	[Fact]
	public void AddTask_ReplacesExistingHandleAndCancelsOldHandle()
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var oldHandle = new RecordingTaskHandle();
		var newHandle = new RecordingTaskHandle();
		adapter.AddTask(ProtectionActiveOrdinal, ProtectionActiveName, oldHandle);

		var replace = adapter.AddTask(ProtectionActiveOrdinal, ProtectionActiveName, newHandle);
		var removed = adapter.GetAndRemoveTask(ProtectionActiveOrdinal, ProtectionActiveName);

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.ReplacedExistingTask, replace.Status);
		Assert.True(replace.ExistingTaskPresentBeforeOperation);
		Assert.True(replace.StoredTask);
		Assert.True(replace.RemovedTask);
		Assert.True(replace.CanceledTask);
		Assert.Equal(1, oldHandle.CancelCalls);
		Assert.False(oldHandle.MayInterruptIfRunningValues.Single());
		Assert.Same(oldHandle, replace.RemovedTaskHandle);
		Assert.Same(newHandle, removed.RemovedTaskHandle);
		Assert.Equal(0, newHandle.CancelCalls);
	}

	[Fact]
	public void CancelTask_RemovesBeforeCancelAndMissingCancelIsNoOp()
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var handle = new RecordingTaskHandle();
		adapter.AddTask(ProtectionActiveOrdinal, ProtectionActiveName, handle);

		var cancel = adapter.CancelTask(ProtectionActiveOrdinal, ProtectionActiveName);
		var missing = adapter.CancelTask(ProtectionActiveOrdinal, ProtectionActiveName);

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.RemovedExistingTask, cancel.Status);
		Assert.True(cancel.RemovedTask);
		Assert.True(cancel.CanceledTask);
		Assert.Same(handle, cancel.RemovedTaskHandle);
		Assert.Equal(1, handle.CancelCalls);
		Assert.False(handle.MayInterruptIfRunningValues.Single());
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Missing, missing.Status);
		Assert.False(missing.RemovedTask);
		Assert.False(missing.CanceledTask);
		Assert.Equal(1, handle.CancelCalls);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Missing, adapter.HasTask(ProtectionActiveOrdinal, ProtectionActiveName).Status);
	}

	[Fact]
	public void GetAndRemoveTask_RemovesWithoutCanceling()
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var handle = new RecordingTaskHandle();
		adapter.AddTask(ProtectionActiveOrdinal, ProtectionActiveName, handle);

		var removed = adapter.GetAndRemoveTask(ProtectionActiveOrdinal, ProtectionActiveName);

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.RemovedExistingTask, removed.Status);
		Assert.True(removed.RemovedTask);
		Assert.False(removed.CanceledTask);
		Assert.Same(handle, removed.RemovedTaskHandle);
		Assert.Equal(0, handle.CancelCalls);
		Assert.Equal(0, adapter.CreateSnapshot().Count);
	}

	[Fact]
	public void CancelTaskIfPresent_CancelsOnlyMatchingStoredHandle()
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var stored = new RecordingTaskHandle();
		var other = new RecordingTaskHandle();
		adapter.AddTask(ProtectionActiveOrdinal, ProtectionActiveName, stored);

		var mismatch = adapter.CancelTaskIfPresent(ProtectionActiveOrdinal, ProtectionActiveName, other);
		var match = adapter.CancelTaskIfPresent(ProtectionActiveOrdinal, ProtectionActiveName, stored);

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.ConditionalMismatchNoOp, mismatch.Status);
		Assert.False(mismatch.RemovedTask);
		Assert.False(mismatch.CanceledTask);
		Assert.Equal(0, other.CancelCalls);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.ConditionalMatchCanceled, match.Status);
		Assert.True(match.RemovedTask);
		Assert.True(match.CanceledTask);
		Assert.Same(stored, match.RemovedTaskHandle);
		Assert.Equal(1, stored.CancelCalls);
		Assert.Equal(0, adapter.CreateSnapshot().Count);
	}

	[Fact]
	public void CancelAllTasks_CancelsEveryStoredHandleAndClearsMap()
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var protection = new RecordingTaskHandle();
		var teleport = new RecordingTaskHandle();
		adapter.AddTask(ProtectionActiveOrdinal, ProtectionActiveName, protection);
		adapter.AddTask(TeleportOrdinal, "TELEPORT", teleport);

		var cancelAll = adapter.CancelAllTasks();
		var cancelEmpty = adapter.CancelAllTasks();

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.CanceledAll, cancelAll.Status);
		Assert.True(cancelAll.RemovedTask);
		Assert.True(cancelAll.CanceledTask);
		Assert.Equal(2, cancelAll.CanceledTaskCount);
		Assert.Equal(1, protection.CancelCalls);
		Assert.Equal(1, teleport.CancelCalls);
		Assert.Equal(0, adapter.CreateSnapshot().Count);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.NoTasksToCancel, cancelEmpty.Status);
		Assert.False(cancelEmpty.CanceledTask);
		Assert.Equal(0, cancelEmpty.CanceledTaskCount);
	}

	[Fact]
	public void HasScheduledTask_TreatsDoneHandleAsNotScheduled()
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var doneHandle = new RecordingTaskHandle(isDone: true);
		adapter.AddTask(ProtectionActiveOrdinal, ProtectionActiveName, doneHandle);

		var result = adapter.HasScheduledTask(ProtectionActiveOrdinal, ProtectionActiveName);

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Done, result.Status);
		Assert.True(result.ExistingTaskPresentBeforeOperation);
		Assert.False(result.RemovedTask);
		Assert.False(result.CanceledTask);
	}

	private const int ProtectionActiveOrdinal = 3;
	private const int TeleportOrdinal = 1;
	private const string ProtectionActiveName = "PROTECTION_ACTIVE";

	private sealed class RecordingTaskHandle : IPlayerProtectionActiveTaskTaskHandle
	{
		public RecordingTaskHandle(bool isDone = false)
		{
			IsDone = isDone;
		}

		public bool IsDone { get; private set; }
		public int CancelCalls { get; private set; }
		public List<bool> MayInterruptIfRunningValues { get; } = [];

		public bool Cancel(bool mayInterruptIfRunning)
		{
			CancelCalls++;
			MayInterruptIfRunningValues.Add(mayInterruptIfRunning);
			if (IsDone)
				return false;

			IsDone = true;
			return true;
		}
	}
}
