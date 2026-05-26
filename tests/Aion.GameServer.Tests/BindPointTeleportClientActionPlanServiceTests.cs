using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportClientActionPlanServiceTests
{
	[Fact]
	public void CreatePlan_DeadPlayerReturnsBeforeActionDispatch()
	{
		var plan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 1,
			locId: 730001,
			kinah: 12345,
			playerIsDead: true);

		Assert.Equal(BindPointTeleportClientActionPlanStatus.NoActionDeadPlayer, plan.Status);
		Assert.Equal([BindPointTeleportClientActionStep.CheckDeadPlayer], plan.Steps);
		Assert.False(plan.ShouldInvokeTeleport);
		Assert.False(plan.ShouldInvokeCancel);
		Assert.Contains("player.isDead", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_ActionOneRequestsTeleportWithLocIdAndKinah()
	{
		var plan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 1,
			locId: 730002,
			kinah: 987654321,
			playerIsDead: false);

		Assert.Equal(BindPointTeleportClientActionPlanStatus.TeleportRequested, plan.Status);
		Assert.Equal(1, plan.Action);
		Assert.Equal(730002, plan.LocId);
		Assert.Equal(987654321, plan.Kinah);
		Assert.Equal(
			[BindPointTeleportClientActionStep.CheckDeadPlayer, BindPointTeleportClientActionStep.DispatchTeleport],
			plan.Steps);
		Assert.True(plan.ShouldInvokeTeleport);
		Assert.False(plan.ShouldInvokeCancel);
		Assert.Contains("BindPointTeleportService.teleport", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_ActionTwoRequestsCancelWithParsedDefaults()
	{
		var plan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 2,
			locId: 0,
			kinah: 0,
			playerIsDead: false);

		Assert.Equal(BindPointTeleportClientActionPlanStatus.CancelRequested, plan.Status);
		Assert.Equal(2, plan.Action);
		Assert.Equal(0, plan.LocId);
		Assert.Equal(0, plan.Kinah);
		Assert.Equal(
			[BindPointTeleportClientActionStep.CheckDeadPlayer, BindPointTeleportClientActionStep.DispatchCancel],
			plan.Steps);
		Assert.False(plan.ShouldInvokeTeleport);
		Assert.True(plan.ShouldInvokeCancel);
		Assert.Contains("BindPointTeleportService.cancelTeleport", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_UnknownActionNoopsAfterDeadCheck()
	{
		var plan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 3,
			locId: 0,
			kinah: 0,
			playerIsDead: false);

		Assert.Equal(BindPointTeleportClientActionPlanStatus.NoActionUnknownAction, plan.Status);
		Assert.Equal(
			[BindPointTeleportClientActionStep.CheckDeadPlayer, BindPointTeleportClientActionStep.NoopUnknownAction],
			plan.Steps);
		Assert.False(plan.ShouldInvokeTeleport);
		Assert.False(plan.ShouldInvokeCancel);
		Assert.Contains("switch default no-op", plan.JavaSource, StringComparison.Ordinal);
	}
}
