using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportRuntimeStatePlanServiceTests
{
	[Fact]
	public void CreateScheduleSkillUseTaskPlan_RecordsJavaDelayAndTaskId()
	{
		var plan = BindPointTeleportRuntimeStatePlanService.CreateScheduleSkillUseTaskPlan(
			playerObjectId: 7001,
			locId: 6001,
			hasExistingSkillUseTask: false);

		Assert.False(plan.IsLive);
		Assert.Equal(BindPointTeleportSkillUseTaskPlanStatus.ScheduleNewTask, plan.Status);
		Assert.False(plan.ShouldCancelExistingTask);
		Assert.True(plan.ShouldScheduleTask);
		Assert.True(plan.ShouldStoreTask);
		Assert.Equal(10_000, plan.DelayMilliseconds);
		Assert.Equal("TaskId.SKILL_USE", plan.TaskIdName);
		Assert.Equal(16, plan.TaskIdOrdinal);
		Assert.Equal(
			[
				BindPointTeleportSkillUseTaskPlanStep.CheckTaskIdSkillUse,
				BindPointTeleportSkillUseTaskPlanStep.ScheduleDelayedTask,
				BindPointTeleportSkillUseTaskPlanStep.StoreTask,
			],
			plan.Steps);
	}

	[Fact]
	public void CreateScheduleSkillUseTaskPlan_ReplacesExistingJavaTaskSlot()
	{
		var plan = BindPointTeleportRuntimeStatePlanService.CreateScheduleSkillUseTaskPlan(
			playerObjectId: 7002,
			locId: 6002,
			hasExistingSkillUseTask: true);

		Assert.Equal(BindPointTeleportSkillUseTaskPlanStatus.ReplaceExistingTask, plan.Status);
		Assert.True(plan.HasExistingTask);
		Assert.True(plan.ShouldCancelExistingTask);
		Assert.True(plan.ShouldScheduleTask);
		Assert.Contains(BindPointTeleportSkillUseTaskPlanStep.CancelExistingTask, plan.Steps);
		Assert.Contains("replaces old task", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateCancelSkillUseTaskPlan_NoopsWhenTaskMissing()
	{
		var plan = BindPointTeleportRuntimeStatePlanService.CreateCancelSkillUseTaskPlan(
			playerObjectId: 7003,
			locId: 6003,
			hasExistingSkillUseTask: false);

		Assert.Equal(BindPointTeleportSkillUseTaskPlanStatus.NoTaskToCancel, plan.Status);
		Assert.False(plan.ShouldCancelExistingTask);
		Assert.False(plan.ShouldScheduleTask);
		Assert.False(plan.ShouldStoreTask);
		Assert.Equal([BindPointTeleportSkillUseTaskPlanStep.CheckTaskIdSkillUse], plan.Steps);
		Assert.Contains("no-op", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateCancelSkillUseTaskPlan_RemovesThenCancelsJavaTask()
	{
		var plan = BindPointTeleportRuntimeStatePlanService.CreateCancelSkillUseTaskPlan(
			playerObjectId: 7004,
			locId: 6004,
			hasExistingSkillUseTask: true);

		Assert.Equal(BindPointTeleportSkillUseTaskPlanStatus.CancelExistingTask, plan.Status);
		Assert.True(plan.ShouldCancelExistingTask);
		Assert.False(plan.ShouldScheduleTask);
		Assert.False(plan.ShouldStoreTask);
		Assert.Equal(
			[
				BindPointTeleportSkillUseTaskPlanStep.CheckTaskIdSkillUse,
				BindPointTeleportSkillUseTaskPlanStep.RemoveTask,
				BindPointTeleportSkillUseTaskPlanStep.CancelExistingTask,
			],
			plan.Steps);
		Assert.Contains("cancel(false)", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateAddCooldownPlan_StoresJavaTenMinuteCooldown()
	{
		var plan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId: 7005,
			locId: 6005,
			currentTimeMillis: 1_000);

		Assert.False(plan.IsLive);
		Assert.Equal(BindPointTeleportCooldownPlanStatus.AddCooldown, plan.Status);
		Assert.Equal(6005, plan.LocId);
		Assert.Equal(601_000, plan.CooldownEndMillis);
		Assert.Equal(600, plan.TimeLeftSeconds);
		Assert.True(plan.ShouldStoreCooldown);
		Assert.Equal([BindPointTeleportCooldownPlanStep.PutCooldown], plan.Steps);
	}

	[Fact]
	public void CreateLookupCooldownPlan_ActiveCooldownUsesJavaWholeSecondTruncation()
	{
		var fact = new BindPointTeleportCooldownFact(
			PlayerObjectId: 7006,
			LocId: 6006,
			CooldownEndMillis: 2_501);

		var plan = BindPointTeleportRuntimeStatePlanService.CreateLookupCooldownPlan(
			playerObjectId: 7006,
			fact,
			currentTimeMillis: 1_000);

		Assert.Equal(BindPointTeleportCooldownPlanStatus.ActiveCooldown, plan.Status);
		Assert.Equal(6006, plan.LocId);
		Assert.Equal(1, plan.TimeLeftSeconds);
		Assert.False(plan.ShouldStoreCooldown);
		Assert.Equal(
			[
				BindPointTeleportCooldownPlanStep.CheckCooldownMap,
				BindPointTeleportCooldownPlanStep.CalculateTimeLeft,
			],
			plan.Steps);
	}

	[Fact]
	public void CreateLookupCooldownPlan_MissingOrWrongPlayerFactReturnsNoCooldown()
	{
		var missing = BindPointTeleportRuntimeStatePlanService.CreateLookupCooldownPlan(
			playerObjectId: 7007,
			cooldownFact: null,
			currentTimeMillis: 1_000);
		var wrongPlayer = BindPointTeleportRuntimeStatePlanService.CreateLookupCooldownPlan(
			playerObjectId: 7007,
			new BindPointTeleportCooldownFact(7008, LocId: 6008, CooldownEndMillis: 9_000),
			currentTimeMillis: 1_000);

		Assert.Equal(BindPointTeleportCooldownPlanStatus.NoCooldown, missing.Status);
		Assert.Equal(0, missing.TimeLeftSeconds);
		Assert.Null(missing.LocId);
		Assert.Equal(BindPointTeleportCooldownPlanStatus.NoCooldown, wrongPlayer.Status);
		Assert.Equal(0, wrongPlayer.TimeLeftSeconds);
		Assert.Null(wrongPlayer.LocId);
	}

	[Fact]
	public void CreateLookupCooldownPlan_ExpiredCooldownReturnsZeroLikeJava()
	{
		var fact = new BindPointTeleportCooldownFact(
			PlayerObjectId: 7009,
			LocId: 6009,
			CooldownEndMillis: 1_999);

		var plan = BindPointTeleportRuntimeStatePlanService.CreateLookupCooldownPlan(
			playerObjectId: 7009,
			fact,
			currentTimeMillis: 2_000);

		Assert.Equal(BindPointTeleportCooldownPlanStatus.ExpiredCooldown, plan.Status);
		Assert.Equal(6009, plan.LocId);
		Assert.Equal(0, plan.TimeLeftSeconds);
		Assert.Contains("returns 0", plan.JavaSource, StringComparison.Ordinal);
	}
}
