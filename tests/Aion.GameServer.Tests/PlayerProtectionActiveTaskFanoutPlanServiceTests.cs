using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskFanoutPlanServiceTests
{
	[Fact]
	public void Create_StartBroadcastsSmPlayerStateAfterBlinkingState()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);

		var fanout = PlayerProtectionActiveTaskFanoutPlanService.Create(
			sourcePlan,
			PlayerProtectionActiveTaskFanoutAction.Start);

		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned, fanout.Status);
		Assert.True(fanout.ShouldBroadcast);
		Assert.False(fanout.SentPackets);
		Assert.True(fanout.IncludeSourcePlayer);
		Assert.True(fanout.SendsSourceBeforeSightedPlayers);
		Assert.True(fanout.UsesKnownListSeesFilter);
		Assert.True(fanout.RequiresLiveKnownList);
		Assert.Equal(typeof(SmPlayerState), fanout.PacketType);
		Assert.Equal(nameof(SmPlayerState), fanout.PacketTypeName);
		Assert.Equal(SmPlayerState.PacketOpCode, fanout.PacketOpCode);
		Assert.Equal(PlayerProtectionActiveTaskPlanStep.SetBlinkingVisualState, fanout.VisualMutationStep);
		Assert.True(fanout.VisualMutationStepIndex < fanout.BroadcastStepIndex);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskFanoutStep.CaptureVisualStateAfterMutation,
				PlayerProtectionActiveTaskFanoutStep.ConstructSmPlayerState,
				PlayerProtectionActiveTaskFanoutStep.SendToSourcePlayer,
				PlayerProtectionActiveTaskFanoutStep.BroadcastToSightedPlayers,
			],
			fanout.Steps);
		Assert.Contains("toSelf=true", fanout.RecipientSelection);
		Assert.Contains("broadcastToSightedPlayers", fanout.JavaSource);
		Assert.False(fanout.IsLive);
	}

	[Fact]
	public void Create_StartAlreadyProtectedSkipsFanout()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);

		var fanout = PlayerProtectionActiveTaskFanoutPlanService.Create(
			sourcePlan,
			PlayerProtectionActiveTaskFanoutAction.Start);

		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.SkippedAlreadyProtectedStart, fanout.Status);
		Assert.False(fanout.ShouldBroadcast);
		Assert.False(fanout.IncludeSourcePlayer);
		Assert.Null(fanout.PacketType);
		Assert.Null(fanout.PacketOpCode);
		Assert.Empty(fanout.Steps);
		Assert.Contains("already BLINKING", fanout.JavaSource);
	}

	[Fact]
	public void Create_StopSpawnedBroadcastsAfterBlinkingClear()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: true,
			isSpawned: true);

		var fanout = PlayerProtectionActiveTaskFanoutPlanService.Create(
			sourcePlan,
			PlayerProtectionActiveTaskFanoutAction.Stop);

		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned, fanout.Status);
		Assert.True(fanout.ShouldBroadcast);
		Assert.Equal(PlayerProtectionActiveTaskPlanStep.UnsetBlinkingVisualState, fanout.VisualMutationStep);
		Assert.True(fanout.VisualMutationStepIndex < fanout.BroadcastStepIndex);
		Assert.Equal(typeof(SmPlayerState), fanout.PacketType);
		Assert.Equal(SmPlayerState.PacketOpCode, fanout.PacketOpCode);
		Assert.Contains("other.getKnownList().sees(source)", fanout.RecipientSelection);
		Assert.False(fanout.IsLive);
	}

	[Fact]
	public void Create_StopUnspawnedSkipsFanout()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: true,
			isSpawned: false);

		var fanout = PlayerProtectionActiveTaskFanoutPlanService.Create(
			sourcePlan,
			PlayerProtectionActiveTaskFanoutAction.Stop);

		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.SkippedUnspawnedStop, fanout.Status);
		Assert.False(fanout.ShouldBroadcast);
		Assert.False(fanout.RequiresLiveKnownList);
		Assert.Null(fanout.PacketTypeName);
		Assert.Null(fanout.BroadcastStepIndex);
		Assert.Contains("!player.isSpawned()", fanout.JavaSource);
	}

	private const int PlayerObjectId = 1001;
}
