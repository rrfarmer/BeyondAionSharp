using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskSchedulerCallbackPlanServiceTests
{
	[Fact]
	public void Create_StartWithOwnerPrototypeRecordsSixtySecondDelayedStopWithoutInvokingScheduler()
	{
		var plan = CreateStartPlan(alreadyProtected: false);
		var ownerPrototype = CreateOwnerPrototypeSnapshot();

		var report = PlayerProtectionActiveTaskSchedulerCallbackPlanService.Create(new PlayerProtectionActiveTaskSchedulerCallbackPlanRequest(
			plan,
			ownerPrototype));

		Assert.False(report.IsLive);
		Assert.Equal(PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive, report.Status);
		Assert.Equal(60000, report.DelayMilliseconds);
		Assert.True(report.SchedulesDelayedStop);
		Assert.True(report.StoresScheduledFuture);
		Assert.True(report.HasOwnerPrototypeEvidence);
		Assert.False(report.InvokesScheduler);
		Assert.False(report.InvokesCallback);
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordScheduleCall
			&& row.JavaOperation.Contains("schedule(this::stopProtectionActiveTask, 60000)", StringComparison.Ordinal)
			&& !row.IsLive);
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordCallbackTarget
			&& row.JavaSource == "PlayerController.stopProtectionActiveTask");
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordTaskMapStorage
			&& row.Notes.Contains("production storage is still not wired", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_StartWithoutOwnerPrototypeBlocksBeforeScheduleMetadata()
	{
		var plan = CreateStartPlan(alreadyProtected: false);

		var report = PlayerProtectionActiveTaskSchedulerCallbackPlanService.Create(new PlayerProtectionActiveTaskSchedulerCallbackPlanRequest(plan));

		Assert.Equal(PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.BlockedMissingOwnerPrototype, report.Status);
		Assert.False(report.SchedulesDelayedStop);
		Assert.False(report.StoresScheduledFuture);
		Assert.False(report.HasOwnerPrototypeEvidence);
		Assert.False(report.InvokesScheduler);
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RequireOwnerPrototype
			&& row.Status == PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.BlockedMissingOwnerPrototype);
		Assert.DoesNotContain(report.Rows, row => row.Kind == PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordScheduleCall);
	}

	[Fact]
	public void Create_AlreadyProtectedStartSkipsSchedulerCallbackLikeJavaGuard()
	{
		var plan = CreateStartPlan(alreadyProtected: true);

		var report = PlayerProtectionActiveTaskSchedulerCallbackPlanService.Create(new PlayerProtectionActiveTaskSchedulerCallbackPlanRequest(
			plan,
			CreateOwnerPrototypeSnapshot()));

		Assert.Equal(PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.SkippedAlreadyProtected, report.Status);
		Assert.False(report.SchedulesDelayedStop);
		Assert.False(report.StoresScheduledFuture);
		Assert.False(report.InvokesScheduler);
		Assert.False(report.InvokesCallback);
		var row = Assert.Single(report.Rows);
		Assert.Equal(PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.ObserveStartBranch, row.Kind);
		Assert.Contains("returns before scheduling", row.Notes, StringComparison.Ordinal);
	}

	private static PlayerProtectionActiveTaskPlan CreateStartPlan(bool alreadyProtected)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		if (alreadyProtected)
			player.SetVisualState(PlayerVisualStates.Blinking);

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));

		return result.Plan;
	}

	private static PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot CreateOwnerPrototypeSnapshot()
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(PlayerObjectId);
		return owner.CreateSnapshot();
	}

	private const int PlayerObjectId = 1001;
}
