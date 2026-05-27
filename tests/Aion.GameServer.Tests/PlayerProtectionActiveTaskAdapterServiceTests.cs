using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskAdapterServiceTests
{
	[Fact]
	public void Apply_DisabledExposesStartPlanWithoutVisualMutation()
	{
		var player = new Player { ObjectId = PlayerObjectId };

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.DisabledPlanned, result.Status);
		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StartProtection, result.Plan.Status);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned, result.FanoutPlan.Status);
		Assert.True(result.FanoutPlan.ShouldBroadcast);
		Assert.False(result.FanoutPlan.SentPackets);
		Assert.False(result.MutatedVisualState);
		Assert.False(result.MutatedScheduler);
		Assert.False(result.SentPackets);
		Assert.True(result.ExposesPlanForObservation);
		Assert.False(result.IsLive);
		Assert.False(player.IsProtectionActive());
	}

	[Fact]
	public void Apply_LiveStartSetsBlinkingButLeavesSchedulerAndPacketsPlanned()
	{
		var player = new Player { ObjectId = PlayerObjectId };

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStarted, result.Status);
		Assert.True(result.MutatedVisualState);
		Assert.False(result.MutatedScheduler);
		Assert.False(result.SentPackets);
		Assert.True(result.IsLive);
		Assert.True(player.IsProtectionActive());
		Assert.True(result.Plan.ShouldScheduleTask);
		Assert.True(result.Plan.ShouldBroadcastPlayerState);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned, result.FanoutPlan.Status);
		Assert.False(result.FanoutPlan.SentPackets);
		Assert.Equal(PlayerProtectionActiveTaskPlanStep.SetBlinkingVisualState, result.FanoutPlan.VisualMutationStep);
		Assert.Contains("setVisualState(BLINKING)", result.JavaSource);
	}

	[Fact]
	public void Apply_LiveStartAlreadyProtectedDoesNotMutate()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.AlreadyProtected, result.Status);
		Assert.False(result.MutatedVisualState);
		Assert.True(player.IsProtectionActive());
		Assert.False(result.Plan.ShouldScheduleTask);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.SkippedAlreadyProtectedStart, result.FanoutPlan.Status);
		Assert.False(result.FanoutPlan.ShouldBroadcast);
	}

	[Fact]
	public void Apply_LiveStopClearsBlinkingButLeavesSchedulerAndPacketsPlanned()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: true));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopped, result.Status);
		Assert.True(result.MutatedVisualState);
		Assert.False(result.MutatedScheduler);
		Assert.False(result.SentPackets);
		Assert.True(result.IsLive);
		Assert.False(player.IsProtectionActive());
		Assert.True(result.Plan.ShouldCancelTask);
		Assert.True(result.Plan.ShouldBroadcastPlayerState);
		Assert.True(result.Plan.ShouldNotifyAiOnMove);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned, result.FanoutPlan.Status);
		Assert.False(result.FanoutPlan.SentPackets);
		Assert.Equal(PlayerProtectionActiveTaskPlanStep.UnsetBlinkingVisualState, result.FanoutPlan.VisualMutationStep);
		Assert.Contains("unsetVisualState(BLINKING)", result.JavaSource);
	}

	[Fact]
	public void Apply_LiveStopUnspawnedDoesNotClearBlinking()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: false));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopUnspawned, result.Status);
		Assert.False(result.MutatedVisualState);
		Assert.True(player.IsProtectionActive());
		Assert.True(result.Plan.ShouldCancelTask);
		Assert.False(result.Plan.ShouldBroadcastPlayerState);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.SkippedUnspawnedStop, result.FanoutPlan.Status);
		Assert.False(result.FanoutPlan.ShouldBroadcast);
	}

	private const int PlayerObjectId = 1001;
}
