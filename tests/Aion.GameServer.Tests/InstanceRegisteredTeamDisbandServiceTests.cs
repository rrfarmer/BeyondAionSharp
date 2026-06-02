using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class InstanceRegisteredTeamDisbandServiceTests
{
	[Fact]
	public void IsRegisteredTeamDisbanded_MatchesJavaRegisteredTeamIsDisbandedCheck()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var activeGroupId = 77001;
		var activeAllianceId = 88001;
		groups.CreateOrUpdateGroup(activeGroupId, [CreatePlayer(1001), CreatePlayer(1002)]);
		alliances.CreateAlliance(activeAllianceId, CreatePlayer(2001));

		var noRegisteredTeam = new WorldMapInstanceRuntimeState(instanceId: 2, maxPlayers: 6);
		var activeGroupInstance = new WorldMapInstanceRuntimeState(instanceId: 3, maxPlayers: 6);
		var activeAllianceInstance = new WorldMapInstanceRuntimeState(instanceId: 4, maxPlayers: 6);
		var removedTeamInstance = new WorldMapInstanceRuntimeState(instanceId: 5, maxPlayers: 6);
		activeGroupInstance.RegisterTeamId(activeGroupId);
		activeAllianceInstance.RegisterTeamId(activeAllianceId);
		removedTeamInstance.RegisterTeamId(99001);

		Assert.False(InstanceRegisteredTeamDisbandService.IsRegisteredTeamDisbanded(noRegisteredTeam, groups, alliances));
		Assert.False(InstanceRegisteredTeamDisbandService.IsRegisteredTeamDisbanded(activeGroupInstance, groups, alliances));
		Assert.False(InstanceRegisteredTeamDisbandService.IsRegisteredTeamDisbanded(activeAllianceInstance, groups, alliances));
		Assert.True(InstanceRegisteredTeamDisbandService.IsRegisteredTeamDisbanded(removedTeamInstance, groups, alliances));
	}

	private static Player CreatePlayer(int objectId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"Player{objectId}",
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
	}
}
