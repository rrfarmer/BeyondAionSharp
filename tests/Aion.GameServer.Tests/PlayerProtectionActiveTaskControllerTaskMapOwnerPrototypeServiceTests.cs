using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeServiceTests
{
	[Fact]
	public void AddTask_StoresProtectionTaskAndReportsControllerOwnedNonLiveSnapshot()
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(OwnerObjectId);
		var handle = new RecordingTaskHandle();

		var add = owner.AddTask(handle);
		var hasTask = owner.HasTask();
		var hasScheduled = owner.HasScheduledTask();
		var snapshot = owner.CreateSnapshot();

		Assert.False(owner.IsLive);
		Assert.True(owner.IsControllerOwned);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Stored, add.Status);
		Assert.Equal(PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService.ProtectionActiveTaskIdOrdinal, add.TaskIdOrdinal);
		Assert.Equal(PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService.ProtectionActiveTaskIdName, add.TaskIdName);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Present, hasTask.Status);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Scheduled, hasScheduled.Status);
		Assert.Equal(OwnerObjectId, snapshot.OwnerObjectId);
		Assert.Equal(1, snapshot.TaskCount);
		Assert.Equal([PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService.ProtectionActiveTaskIdOrdinal], snapshot.TaskIdOrdinals);
		Assert.Equal([PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService.ProtectionActiveTaskIdName], snapshot.TaskIdNames);
	}

	[Fact]
	public void AddTask_ReplacesExistingProtectionTaskAndCancelsOldHandle()
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(OwnerObjectId);
		var oldHandle = new RecordingTaskHandle();
		var newHandle = new RecordingTaskHandle();
		owner.AddTask(oldHandle);

		var replace = owner.AddTask(newHandle);
		var removed = owner.GetAndRemoveTask();

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.ReplacedExistingTask, replace.Status);
		Assert.True(replace.ExistingTaskPresentBeforeOperation);
		Assert.True(replace.RemovedTask);
		Assert.True(replace.StoredTask);
		Assert.True(replace.CanceledTask);
		Assert.Same(oldHandle, replace.RemovedTaskHandle);
		Assert.Equal(1, oldHandle.CancelCalls);
		Assert.False(oldHandle.MayInterruptIfRunningValues.Single());
		Assert.Same(newHandle, removed.RemovedTaskHandle);
		Assert.Equal(0, newHandle.CancelCalls);
		Assert.Equal(0, owner.CreateSnapshot().TaskCount);
	}

	[Fact]
	public void CancelTask_RemovesBeforeCancelAndMissingCancelIsNoOp()
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(OwnerObjectId);
		var handle = new RecordingTaskHandle();
		owner.AddTask(handle);

		var cancel = owner.CancelTask();
		var missing = owner.CancelTask();

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.RemovedExistingTask, cancel.Status);
		Assert.True(cancel.RemovedTask);
		Assert.True(cancel.CanceledTask);
		Assert.Same(handle, cancel.RemovedTaskHandle);
		Assert.Equal(1, handle.CancelCalls);
		Assert.False(handle.MayInterruptIfRunningValues.Single());
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Missing, missing.Status);
		Assert.False(missing.RemovedTask);
		Assert.False(missing.CanceledTask);
	}

	[Fact]
	public void CancelTaskIfPresent_CancelsOnlyMatchingProtectionHandle()
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(OwnerObjectId);
		var stored = new RecordingTaskHandle();
		var other = new RecordingTaskHandle();
		owner.AddTask(stored);

		var mismatch = owner.CancelTaskIfPresent(other);
		var match = owner.CancelTaskIfPresent(stored);

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.ConditionalMismatchNoOp, mismatch.Status);
		Assert.False(mismatch.RemovedTask);
		Assert.False(mismatch.CanceledTask);
		Assert.Equal(0, other.CancelCalls);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.ConditionalMatchCanceled, match.Status);
		Assert.True(match.RemovedTask);
		Assert.True(match.CanceledTask);
		Assert.Same(stored, match.RemovedTaskHandle);
		Assert.Equal(1, stored.CancelCalls);
		Assert.Equal(0, owner.CreateSnapshot().TaskCount);
	}

	[Fact]
	public void CancelAllTasks_CancelsProtectionTaskAndClearsOwnerMap()
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(OwnerObjectId);
		var handle = new RecordingTaskHandle();
		owner.AddTask(handle);

		var cancelAll = owner.CancelAllTasks();
		var cancelEmpty = owner.CancelAllTasks();

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.CanceledAll, cancelAll.Status);
		Assert.True(cancelAll.RemovedTask);
		Assert.True(cancelAll.CanceledTask);
		Assert.Equal(1, cancelAll.CanceledTaskCount);
		Assert.Equal(1, handle.CancelCalls);
		Assert.Equal(0, owner.CreateSnapshot().TaskCount);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.NoTasksToCancel, cancelEmpty.Status);
		Assert.False(cancelEmpty.CanceledTask);
	}

	[Fact]
	public void HasScheduledTask_TreatsDoneProtectionHandleAsNotScheduled()
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(OwnerObjectId);
		owner.AddTask(new RecordingTaskHandle(isDone: true));

		var result = owner.HasScheduledTask();

		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Done, result.Status);
		Assert.True(result.ExistingTaskPresentBeforeOperation);
		Assert.False(result.RemovedTask);
		Assert.False(result.CanceledTask);
	}

	private const int OwnerObjectId = 1001;

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
