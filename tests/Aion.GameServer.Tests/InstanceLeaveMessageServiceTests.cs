using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class InstanceLeaveMessageServiceTests
{
	[Fact]
	public void CreateLeaveMessagePlan_MatchesJavaOnLeaveInstanceBranchOrder()
	{
		var noRegistration = new WorldMapInstanceRuntimeState(instanceId: 2, maxPlayers: 6);
		var solo = new WorldMapInstanceRuntimeState(instanceId: 3, ownerId: 1001, maxPlayers: 1);
		solo.Register(1001);
		var emptyTeam = new WorldMapInstanceRuntimeState(instanceId: 4, maxPlayers: 6);
		emptyTeam.RegisterTeamId(88001);
		var lastPlayer = new WorldMapInstanceRuntimeState(instanceId: 5, maxPlayers: 6);
		lastPlayer.Register(1001);
		lastPlayer.AddPlayer(1001);
		var playersRemain = new WorldMapInstanceRuntimeState(instanceId: 6, maxPlayers: 6);
		playersRemain.Register(1001);
		playersRemain.AddPlayer(1001);
		playersRemain.AddPlayer(1002);

		var noRegistrationPlan = InstanceLeaveMessageService.CreateLeaveMessagePlan(noRegistration, 300, 900);
		var soloPlan = InstanceLeaveMessageService.CreateLeaveMessagePlan(solo, 300, 900);
		var emptyTeamPlan = InstanceLeaveMessageService.CreateLeaveMessagePlan(emptyTeam, 300, 900, registeredTeamHasNoMembers: true);
		var lastPlayerPlan = InstanceLeaveMessageService.CreateLeaveMessagePlan(lastPlayer, 300, 900);
		var playersRemainPlan = InstanceLeaveMessageService.CreateLeaveMessagePlan(playersRemain, 300, 900);

		Assert.Equal(InstanceLeaveMessageStatus.NoRegisteredObjects, noRegistrationPlan.Status);
		Assert.Null(noRegistrationPlan.Packet);
		Assert.Equal(InstanceLeaveMessageStatus.SoloInstance, soloPlan.Status);
		Assert.Equal(5, soloPlan.Minutes);
		Assert.Equal(1400044, soloPlan.Packet!.MessageId);
		Assert.Equal(["5"], soloPlan.Packet.Parameters);
		Assert.Equal(InstanceLeaveMessageStatus.RegisteredTeamEmpty, emptyTeamPlan.Status);
		Assert.Equal(0, emptyTeamPlan.Minutes);
		Assert.Equal(1400045, emptyTeamPlan.Packet!.MessageId);
		Assert.Equal(["0"], emptyTeamPlan.Packet.Parameters);
		Assert.Equal(InstanceLeaveMessageStatus.LastOrOnlyPlayerInside, lastPlayerPlan.Status);
		Assert.Equal(15, lastPlayerPlan.Minutes);
		Assert.Equal(1400045, lastPlayerPlan.Packet!.MessageId);
		Assert.Equal(["15"], lastPlayerPlan.Packet.Parameters);
		Assert.Equal(InstanceLeaveMessageStatus.PlayersRemainInside, playersRemainPlan.Status);
		Assert.Null(playersRemainPlan.Packet);
	}
}
