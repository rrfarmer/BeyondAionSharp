using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskTaskMapSimulationServiceTests
{
	[Fact]
	public void Create_StartWithoutExistingTaskStoresScheduledHandle()
	{
		var plan = CreateStartPlan(existingTask: false);
		var scheduled = new RecordingTaskHandle();

		var report = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			plan,
			ScheduledTaskHandle: scheduled));

		Assert.False(report.IsLive);
		Assert.False(report.UsedExistingTaskHandle);
		Assert.True(report.UsedScheduledTaskHandle);
		Assert.True(report.StoredScheduledTask);
		Assert.False(report.CanceledExistingTask);
		Assert.Equal(1, report.FinalSnapshot.Count);
		Assert.Equal([3], report.FinalSnapshot.TaskIdOrdinals);
		var row = Assert.Single(report.Rows);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperation.AddTaskAndMaybeReplaceExisting, row.SourceOperation);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Stored, row.AdapterResult.Status);
		Assert.Equal(0, scheduled.CancelCalls);
	}

	[Fact]
	public void Create_StartWithExistingTaskReplacesAndCancelsOldHandle()
	{
		var plan = CreateStartPlan(existingTask: true);
		var existing = new RecordingTaskHandle();
		var scheduled = new RecordingTaskHandle();

		var report = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			plan,
			ScheduledTaskHandle: scheduled,
			ExistingTaskHandle: existing));

		var row = Assert.Single(report.Rows);
		Assert.True(report.UsedExistingTaskHandle);
		Assert.True(report.StoredScheduledTask);
		Assert.True(report.CanceledExistingTask);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.ReplacedExistingTask, row.AdapterResult.Status);
		Assert.Same(existing, row.AdapterResult.RemovedTaskHandle);
		Assert.Equal(1, existing.CancelCalls);
		Assert.False(existing.MayInterruptIfRunningValues.Single());
		Assert.Equal(0, scheduled.CancelCalls);
		Assert.Equal(1, report.FinalSnapshot.Count);
	}

	[Fact]
	public void Create_StopWithExistingTaskCancelsAndClearsMap()
	{
		var plan = CreateStopPlan(existingTask: true);
		var existing = new RecordingTaskHandle();

		var report = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			plan,
			ExistingTaskHandle: existing));

		var row = Assert.Single(report.Rows);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperation.CancelTask, row.SourceOperation);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.RemovedExistingTask, row.AdapterResult.Status);
		Assert.True(row.AdapterResult.CanceledTask);
		Assert.Same(existing, row.AdapterResult.RemovedTaskHandle);
		Assert.Equal(1, existing.CancelCalls);
		Assert.Equal(0, report.FinalSnapshot.Count);
	}

	[Fact]
	public void Create_StopWithoutExistingTaskReportsMissingCancelNoOp()
	{
		var plan = CreateStopPlan(existingTask: false);

		var report = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(plan));

		var row = Assert.Single(report.Rows);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Missing, row.AdapterResult.Status);
		Assert.True(report.RemovedMissingTaskAsNoOp);
		Assert.False(report.CanceledExistingTask);
		Assert.Equal(0, report.FinalSnapshot.Count);
	}

	[Fact]
	public void Create_CancelAllAfterStartCancelsRemainingScheduledHandle()
	{
		var plan = CreateStartPlan(existingTask: false);
		var scheduled = new RecordingTaskHandle();

		var report = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			plan,
			ScheduledTaskHandle: scheduled,
			RunCancelAllAfterPlan: true));

		Assert.True(report.RanCancelAllAfterPlan);
		Assert.Equal(2, report.Rows.Count);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.Stored, report.Rows[0].AdapterResult.Status);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.CanceledAll, report.Rows[1].AdapterResult.Status);
		Assert.Equal(1, scheduled.CancelCalls);
		Assert.False(scheduled.MayInterruptIfRunningValues.Single());
		Assert.Equal(0, report.FinalSnapshot.Count);
	}

	[Fact]
	public async Task Create_StartStoresWrappedScheduledTaskHandle()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var callbackRan = false;
		var scheduledTask = threadPoolManager.Schedule(
			_ =>
			{
				callbackRan = true;
				return ValueTask.CompletedTask;
			},
			TimeSpan.FromMinutes(5));
		var scheduledHandle = new PlayerProtectionActiveTaskScheduledTaskHandleAdapter(scheduledTask);
		var plan = CreateStartPlan(existingTask: false);

		var report = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			plan,
			ScheduledTaskHandle: scheduledHandle));

		Assert.True(report.StoredScheduledTask);
		Assert.Equal(1, report.FinalSnapshot.Count);
		Assert.False(callbackRan);
		Assert.False(scheduledHandle.IsDone);

		Assert.True(scheduledHandle.Cancel(mayInterruptIfRunning: false));
		await WaitForCompletionAsync(scheduledTask);
	}

	[Fact]
	public async Task Create_StartReplacementCancelsExistingWrappedScheduledTaskHandle()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var existingCallbackRan = false;
		var existingTask = threadPoolManager.Schedule(
			_ =>
			{
				existingCallbackRan = true;
				return ValueTask.CompletedTask;
			},
			TimeSpan.FromMinutes(5));
		var existingHandle = new PlayerProtectionActiveTaskScheduledTaskHandleAdapter(existingTask);
		var newScheduled = new RecordingTaskHandle();
		var plan = CreateStartPlan(existingTask: true);

		var report = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			plan,
			ScheduledTaskHandle: newScheduled,
			ExistingTaskHandle: existingHandle));
		await WaitForCompletionAsync(existingTask);

		var row = Assert.Single(report.Rows);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.ReplacedExistingTask, row.AdapterResult.Status);
		Assert.Same(existingHandle, row.AdapterResult.RemovedTaskHandle);
		Assert.True(existingHandle.IsDone);
		Assert.False(existingCallbackRan);
		Assert.Equal(0, newScheduled.CancelCalls);
		Assert.Equal(1, report.FinalSnapshot.Count);
	}

	[Fact]
	public async Task Create_StopCancelsExistingWrappedScheduledTaskHandle()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var callbackRan = false;
		var existingTask = threadPoolManager.Schedule(
			_ =>
			{
				callbackRan = true;
				return ValueTask.CompletedTask;
			},
			TimeSpan.FromMinutes(5));
		var existingHandle = new PlayerProtectionActiveTaskScheduledTaskHandleAdapter(existingTask);
		var plan = CreateStopPlan(existingTask: true);

		var report = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			plan,
			ExistingTaskHandle: existingHandle));
		await WaitForCompletionAsync(existingTask);

		var row = Assert.Single(report.Rows);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOperationStatus.RemovedExistingTask, row.AdapterResult.Status);
		Assert.Same(existingHandle, row.AdapterResult.RemovedTaskHandle);
		Assert.True(existingHandle.IsDone);
		Assert.False(callbackRan);
		Assert.Equal(0, report.FinalSnapshot.Count);
	}

	private static PlayerProtectionActiveTaskTaskOperationPlan CreateStartPlan(bool existingTask)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));

		return PlayerProtectionActiveTaskTaskOperationPlanService.Create(adapterResult.Plan, existingTask);
	}

	private static PlayerProtectionActiveTaskTaskOperationPlan CreateStopPlan(bool existingTask)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: true));

		return PlayerProtectionActiveTaskTaskOperationPlanService.Create(adapterResult.Plan, existingTask);
	}

	private const int PlayerObjectId = 1001;

	private static ThreadPoolManager CreateThreadPoolManager() =>
		new(NullLogger<ThreadPoolManager>.Instance);

	private static async Task WaitForCompletionAsync(ScheduledTask scheduledTask)
	{
		var completed = await Task.WhenAny(scheduledTask.Completion, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(scheduledTask.Completion, completed);
		await scheduledTask.Completion;
	}

	private sealed class RecordingTaskHandle : IPlayerProtectionActiveTaskTaskHandle
	{
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
