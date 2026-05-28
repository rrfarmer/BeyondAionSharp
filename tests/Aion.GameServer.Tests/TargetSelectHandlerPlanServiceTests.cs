using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TargetSelectHandlerPlanServiceTests
{
	[Fact]
	public void CreatePlan_UsesPlayerStateAndKnownTargetInputWithoutMutatingPlayer()
	{
		var player = CreatePlayer(currentTargetObjectId: 0);

		var plan = TargetSelectHandlerPlanService.CreatePlan(player, new TargetSelectHandlerInput(
			RequestedTargetObjectId: 7002,
			SelectTargetOfTarget: false,
			KnownTargetObjectId: 7002,
			KnownTargetSeenByPlayer: true));

		Assert.Equal(TargetSelectHandlerPlanStatus.Created, plan.Status);
		Assert.Equal(1001, plan.PlayerObjectId);
		Assert.Equal(0, plan.CurrentTargetObjectId);
		Assert.Equal(7002, plan.PlannedTargetObjectId);
		Assert.True(plan.WouldMutatePlayerTargetObjectId);
		Assert.True(plan.WouldSendOwnerPacket);
		Assert.True(plan.WouldBroadcastToSightedPlayers);
		Assert.Equal(TargetSelectResolutionStatus.SelectedKnownObject, plan.ExecutionPlan.ResolutionPlan.Status);
		Assert.Equal(0, player.TargetObjectId);
		Assert.Contains("GameServerConnection.HandleTargetSelect", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_ModelsCurrentTargetClearWithoutLiveMutation()
	{
		var player = CreatePlayer(currentTargetObjectId: 7002);

		var plan = TargetSelectHandlerPlanService.CreatePlan(player, new TargetSelectHandlerInput(
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: false));

		Assert.Equal(TargetSelectResolutionStatus.ClearedTarget, plan.ExecutionPlan.ResolutionPlan.Status);
		Assert.Equal(TargetSelectExecutionPlanStatus.TargetChangePacketsCreated, plan.ExecutionPlan.Status);
		Assert.Equal(7002, plan.CurrentTargetObjectId);
		Assert.Equal(0, plan.PlannedTargetObjectId);
		Assert.True(plan.WouldMutatePlayerTargetObjectId);
		Assert.Equal(7002, player.TargetObjectId);
	}

	[Fact]
	public void CreatePlan_ModelsAssistEarlyReturnWithoutPacketPlanOrMutation()
	{
		var player = CreatePlayer(currentTargetObjectId: 7002);

		var plan = TargetSelectHandlerPlanService.CreatePlan(player, new TargetSelectHandlerInput(
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: true,
			CurrentTargetTargetObjectId: 7003,
			CurrentTargetTargetKnownByPlayer: true,
			CurrentTargetTargetSeenByPlayer: false));

		Assert.Equal(TargetSelectResolutionStatus.AssistTargetNotVisible, plan.ExecutionPlan.ResolutionPlan.Status);
		Assert.Equal(TargetSelectExecutionPlanStatus.ReturnedEarlyWithSystemMessage, plan.ExecutionPlan.Status);
		Assert.Equal(TargetSelectSystemMessage.AssistNoUser, plan.SystemMessage);
		Assert.Equal(7002, plan.PlannedTargetObjectId);
		Assert.False(plan.WouldMutatePlayerTargetObjectId);
		Assert.False(plan.WouldSendOwnerPacket);
		Assert.False(plan.WouldBroadcastToSightedPlayers);
		Assert.Equal(7002, player.TargetObjectId);
	}

	[Fact]
	public void CreatePlan_ModelsTeamMemberFallbackFromPacketContext()
	{
		var player = CreatePlayer(currentTargetObjectId: 0);

		var plan = TargetSelectHandlerPlanService.CreatePlan(player, new TargetSelectHandlerInput(
			RequestedTargetObjectId: 2002,
			SelectTargetOfTarget: false,
			TeamMemberObjectId: 2002));

		Assert.Equal(TargetSelectResolutionStatus.SelectedTeamMember, plan.ExecutionPlan.ResolutionPlan.Status);
		Assert.Equal(TargetSelectExecutionPlanStatus.TargetChangePacketsCreated, plan.ExecutionPlan.Status);
		Assert.Equal(2002, plan.PlannedTargetObjectId);
		Assert.True(plan.WouldSendOwnerPacket);
		Assert.True(plan.WouldBroadcastToSightedPlayers);
	}

	private static Player CreatePlayer(int currentTargetObjectId)
	{
		return new Player
		{
			ObjectId = 1001,
			TargetObjectId = currentTargetObjectId,
		};
	}
}
