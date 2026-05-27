using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskPlanServiceTests
{
	[Fact]
	public void CreateStartPlan_ModelsJavaProtectionTaskStart()
	{
		var player = new Player { ObjectId = PlayerObjectId };

		var plan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StartProtection, plan.Status);
		Assert.False(plan.WasProtectionActive);
		Assert.True(plan.ShouldSetBlinkingVisualState);
		Assert.True(plan.ShouldCancelCastOnPlayer);
		Assert.True(plan.ShouldRemovePlayerFromTargets);
		Assert.True(plan.ShouldBroadcastPlayerState);
		Assert.True(plan.ShouldScheduleTask);
		Assert.True(plan.ShouldStoreTask);
		Assert.False(plan.ShouldCancelTask);
		Assert.Equal(60_000, plan.DelayMilliseconds);
		Assert.Equal("TaskId.PROTECTION_ACTIVE", plan.TaskIdName);
		Assert.Equal(3, plan.TaskIdOrdinal);
		Assert.Equal(typeof(SmPlayerState), plan.BroadcastPacketType);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskPlanStep.CheckProtectionActive,
				PlayerProtectionActiveTaskPlanStep.SetBlinkingVisualState,
				PlayerProtectionActiveTaskPlanStep.CancelCastOnPlayer,
				PlayerProtectionActiveTaskPlanStep.RemovePlayerFromTargets,
				PlayerProtectionActiveTaskPlanStep.BroadcastPlayerState,
				PlayerProtectionActiveTaskPlanStep.ScheduleProtectionActiveTask,
				PlayerProtectionActiveTaskPlanStep.StoreProtectionActiveTask,
			],
			plan.Steps);
		Assert.Contains("startProtectionActiveTask", plan.JavaSource);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateStartPlan_NoOpsWhenPlayerAlreadyBlinking()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var plan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.AlreadyProtected, plan.Status);
		Assert.True(plan.WasProtectionActive);
		Assert.False(plan.ShouldSetBlinkingVisualState);
		Assert.False(plan.ShouldScheduleTask);
		Assert.False(plan.ShouldBroadcastPlayerState);
		Assert.Null(plan.BroadcastPacketType);
		Assert.Equal([PlayerProtectionActiveTaskPlanStep.CheckProtectionActive], plan.Steps);
	}

	[Fact]
	public void CreateStopPlan_ModelsJavaSpawnedStopAndBroadcast()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var plan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: true,
			isSpawned: true);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtection, plan.Status);
		Assert.True(plan.WasProtectionActive);
		Assert.True(plan.ShouldCancelTask);
		Assert.True(plan.ShouldUnsetBlinkingVisualState);
		Assert.True(plan.ShouldBroadcastPlayerState);
		Assert.True(plan.ShouldNotifyAiOnMove);
		Assert.False(plan.ShouldScheduleTask);
		Assert.Equal(typeof(SmPlayerState), plan.BroadcastPacketType);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskPlanStep.CancelProtectionActiveTask,
				PlayerProtectionActiveTaskPlanStep.UnsetBlinkingVisualState,
				PlayerProtectionActiveTaskPlanStep.BroadcastPlayerState,
				PlayerProtectionActiveTaskPlanStep.NotifyAiOnMove,
			],
			plan.Steps);
		Assert.Contains("stopProtectionActiveTask", plan.JavaSource);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateStopPlan_UnspawnedOnlyCancelsRepresentedTask()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var plan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: true,
			isSpawned: false);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtectionUnspawned, plan.Status);
		Assert.True(plan.WasProtectionActive);
		Assert.True(plan.ShouldCancelTask);
		Assert.False(plan.ShouldUnsetBlinkingVisualState);
		Assert.False(plan.ShouldBroadcastPlayerState);
		Assert.False(plan.ShouldNotifyAiOnMove);
		Assert.Null(plan.BroadcastPacketType);
		Assert.Equal([PlayerProtectionActiveTaskPlanStep.CancelProtectionActiveTask], plan.Steps);
	}

	[Fact]
	public void CreateStopPlan_SpawnedStillBroadcastsWhenTaskOrBlinkingIsAlreadyMissing()
	{
		var player = new Player { ObjectId = PlayerObjectId };

		var plan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: false,
			isSpawned: true);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtection, plan.Status);
		Assert.False(plan.WasProtectionActive);
		Assert.False(plan.ShouldCancelTask);
		Assert.True(plan.ShouldUnsetBlinkingVisualState);
		Assert.True(plan.ShouldBroadcastPlayerState);
		Assert.True(plan.ShouldNotifyAiOnMove);
		Assert.Equal(typeof(SmPlayerState), plan.BroadcastPacketType);
	}

	private const int PlayerObjectId = 1001;
}
