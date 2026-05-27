using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskDelayedStopCallbackPreviewServiceTests
{
	[Fact]
	public void Create_PlannedDelayedCallbackComposesStopPlanAndCancelsOwnerTask()
	{
		var owner = CreateOwnerPrototype(withStoredTask: true);
		var schedulerPlan = CreateSchedulerPlan(owner.CreateSnapshot(), alreadyProtected: false);
		var stopPlan = CreateStopTaskOperationPlan(existingTask: true);

		var preview = PlayerProtectionActiveTaskDelayedStopCallbackPreviewService.Create(new PlayerProtectionActiveTaskDelayedStopCallbackPreviewRequest(
			schedulerPlan,
			stopPlan,
			owner));

		Assert.False(preview.IsLive);
		Assert.Equal(PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.PlannedNotLive, preview.Status);
		Assert.True(preview.HasScheduledCallbackMetadata);
		Assert.True(preview.ComposesStopTaskOperationPlan);
		Assert.True(preview.CancelsOwnerTask);
		Assert.False(preview.RemovesMissingTaskAsNoOp);
		Assert.False(preview.InvokesScheduler);
		Assert.False(preview.InvokesCallback);
		Assert.False(preview.InvokesSocketFanout);
		Assert.False(preview.InvokesAiMoveNotification);
		Assert.Equal(0, owner.CreateSnapshot().TaskCount);
		Assert.Contains(preview.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordCallbackTarget
			&& row.JavaOperation == "this::stopProtectionActiveTask"
			&& !row.IsLive);
		Assert.Contains(preview.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.ComposeStopTaskOperationPlan
			&& row.Notes.Contains("StopProtection", StringComparison.Ordinal));
		Assert.Contains(preview.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.CancelOwnerTask
			&& row.Notes.Contains("CanceledTask=True", StringComparison.Ordinal));
		Assert.Contains(preview.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RecordLiveSideEffectBoundary
			&& row.Notes.Contains("does not mutate visual state", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingOwnerTaskRecordsNoOpCancellationBranch()
	{
		var owner = CreateOwnerPrototype(withStoredTask: false);
		var schedulerPlan = CreateSchedulerPlan(owner.CreateSnapshot(), alreadyProtected: false);
		var stopPlan = CreateStopTaskOperationPlan(existingTask: false);

		var preview = PlayerProtectionActiveTaskDelayedStopCallbackPreviewService.Create(new PlayerProtectionActiveTaskDelayedStopCallbackPreviewRequest(
			schedulerPlan,
			stopPlan,
			owner));

		Assert.Equal(PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.MissingOwnerTaskNoOp, preview.Status);
		Assert.True(preview.HasScheduledCallbackMetadata);
		Assert.True(preview.ComposesStopTaskOperationPlan);
		Assert.False(preview.CancelsOwnerTask);
		Assert.True(preview.RemovesMissingTaskAsNoOp);
		Assert.Contains(preview.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.CancelOwnerTask
			&& row.Status == PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.MissingOwnerTaskNoOp
			&& row.Notes.Contains("Missing task cancel is a no-op", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_SkipsPreviewWhenSchedulerPlanDidNotScheduleDelayedStop()
	{
		var owner = CreateOwnerPrototype(withStoredTask: false);
		var schedulerPlan = CreateSchedulerPlan(owner.CreateSnapshot(), alreadyProtected: true);
		var stopPlan = CreateStopTaskOperationPlan(existingTask: false);

		var preview = PlayerProtectionActiveTaskDelayedStopCallbackPreviewService.Create(new PlayerProtectionActiveTaskDelayedStopCallbackPreviewRequest(
			schedulerPlan,
			stopPlan,
			owner));

		Assert.Equal(PlayerProtectionActiveTaskDelayedStopCallbackPreviewStatus.SkippedNoDelayedStop, preview.Status);
		Assert.False(preview.HasScheduledCallbackMetadata);
		Assert.False(preview.ComposesStopTaskOperationPlan);
		Assert.False(preview.CancelsOwnerTask);
		var row = Assert.Single(preview.Rows);
		Assert.Equal(PlayerProtectionActiveTaskDelayedStopCallbackPreviewRowKind.RequireScheduledCallbackPlan, row.Kind);
		Assert.Contains("did not schedule a delayed stop", row.Notes, StringComparison.Ordinal);
	}

	private static PlayerProtectionActiveTaskSchedulerCallbackPlan CreateSchedulerPlan(
		PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot ownerSnapshot,
		bool alreadyProtected)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		if (alreadyProtected)
			player.SetVisualState(PlayerVisualStates.Blinking);

		var startPlan = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true)).Plan;

		return PlayerProtectionActiveTaskSchedulerCallbackPlanService.Create(new PlayerProtectionActiveTaskSchedulerCallbackPlanRequest(
			startPlan,
			ownerSnapshot));
	}

	private static PlayerProtectionActiveTaskTaskOperationPlan CreateStopTaskOperationPlan(bool existingTask)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var stopPlan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: existingTask,
			isSpawned: true);

		return PlayerProtectionActiveTaskTaskOperationPlanService.Create(stopPlan, existingTask);
	}

	private static PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService CreateOwnerPrototype(bool withStoredTask)
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(PlayerObjectId);
		if (withStoredTask)
			owner.AddTask(new RecordingTaskHandle());

		return owner;
	}

	private sealed class RecordingTaskHandle : IPlayerProtectionActiveTaskTaskHandle
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

	private const int PlayerObjectId = 1001;
}
