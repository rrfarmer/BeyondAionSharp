using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskTaskOperationPlanServiceTests
{
	[Fact]
	public void Create_StartPlansDelayedScheduleAndTaskStore()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);

		var taskPlan = PlayerProtectionActiveTaskTaskOperationPlanService.Create(
			sourcePlan,
			existingProtectionTaskPresent: false);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StartProtection, taskPlan.SourcePlanStatus);
		Assert.Equal(PlayerProtectionActiveTaskPlanService.ProtectionActiveTaskIdName, taskPlan.TaskIdName);
		Assert.Equal(PlayerProtectionActiveTaskPlanService.ProtectionActiveTaskIdOrdinal, taskPlan.TaskIdOrdinal);
		Assert.Equal(60_000, taskPlan.DelayMilliseconds);
		Assert.True(taskPlan.SchedulesDelayedStop);
		Assert.True(taskPlan.StoresTask);
		Assert.False(taskPlan.ReplacesExistingTask);
		Assert.False(taskPlan.CancelsExistingTask);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskTaskOperation.ScheduleDelayedStop,
				PlayerProtectionActiveTaskTaskOperation.AddTaskAndMaybeReplaceExisting,
			],
			taskPlan.Rows.Select(row => row.Operation));
		Assert.Contains("ThreadPoolManager", taskPlan.Rows[0].JavaOperation);
		Assert.Contains("tasks.compute", taskPlan.Rows[1].JavaSource);
		Assert.All(taskPlan.Rows, row => Assert.False(row.IsLive));
	}

	[Fact]
	public void Create_StartWithExistingTaskRecordsReplacementCancellation()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);

		var taskPlan = PlayerProtectionActiveTaskTaskOperationPlanService.Create(
			sourcePlan,
			existingProtectionTaskPresent: true);

		Assert.True(taskPlan.ReplacesExistingTask);
		Assert.True(taskPlan.StoresTask);
		Assert.False(taskPlan.CancelsExistingTask);
		var addRow = Assert.Single(taskPlan.Rows, row => row.Operation == PlayerProtectionActiveTaskTaskOperation.AddTaskAndMaybeReplaceExisting);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperationStatus.WouldReplaceExistingTask, addRow.Status);
		Assert.True(addRow.WouldCancelExistingTask);
		Assert.True(addRow.WouldStoreNewTask);
		Assert.Contains("cancel the previous future", addRow.Notes);
	}

	[Fact]
	public void Create_AlreadyProtectedStartHasNoTaskOperation()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);

		var taskPlan = PlayerProtectionActiveTaskTaskOperationPlanService.Create(
			sourcePlan,
			existingProtectionTaskPresent: true);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.AlreadyProtected, taskPlan.SourcePlanStatus);
		Assert.False(taskPlan.SchedulesDelayedStop);
		Assert.False(taskPlan.StoresTask);
		Assert.False(taskPlan.ReplacesExistingTask);
		Assert.False(taskPlan.CancelsExistingTask);
		var row = Assert.Single(taskPlan.Rows);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperation.NoTaskOperation, row.Operation);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperationStatus.SkippedNoOpBranch, row.Status);
		Assert.False(row.JavaCallReached);
	}

	[Fact]
	public void Create_StopWithExistingTaskRecordsCancelBeforeSpawnedSideEffects()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: true,
			isSpawned: true);

		var taskPlan = PlayerProtectionActiveTaskTaskOperationPlanService.Create(
			sourcePlan,
			existingProtectionTaskPresent: true);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtection, taskPlan.SourcePlanStatus);
		Assert.False(taskPlan.SchedulesDelayedStop);
		Assert.False(taskPlan.StoresTask);
		Assert.True(taskPlan.CancelsExistingTask);
		Assert.False(taskPlan.RemovesMissingTaskAsNoOp);
		var row = Assert.Single(taskPlan.Rows);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperation.CancelTask, row.Operation);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperationStatus.WouldCancelExistingTask, row.Status);
		Assert.True(row.JavaCallReached);
		Assert.True(row.WouldCancelExistingTask);
		Assert.Contains("before spawned-side effects", row.Notes);
	}

	[Fact]
	public void Create_StopWithoutExistingTaskStillRecordsCancelTaskNoOp()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: false,
			isSpawned: false);

		var taskPlan = PlayerProtectionActiveTaskTaskOperationPlanService.Create(
			sourcePlan,
			existingProtectionTaskPresent: false);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtectionUnspawned, taskPlan.SourcePlanStatus);
		Assert.True(taskPlan.RemovesMissingTaskAsNoOp);
		Assert.False(taskPlan.CancelsExistingTask);
		var row = Assert.Single(taskPlan.Rows);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperation.CancelTask, row.Operation);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperationStatus.WouldRemoveMissingTaskNoOp, row.Status);
		Assert.True(row.JavaCallReached);
		Assert.False(row.WouldCancelExistingTask);
		Assert.Contains("returns null", row.Notes);
	}

	private const int PlayerObjectId = 1001;
}
