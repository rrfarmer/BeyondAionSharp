using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskUpdateFanoutServiceTests
{
	[Fact]
	public void CreatePlanMatchesJavaKiskBroadcastMemberAndRaceFanout()
	{
		var kiskPosition = new WorldPosition(210010000, 0, 0, 0, 0);
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			ownerRace: "ELYOS");
		Assert.True(kisk.AddMember(1001));
		Assert.True(kisk.AddMember(1002));
		Assert.True(kisk.AddMember(1003));

		var players = new[]
		{
			CreatePlayer(1001, "ELYOS", kiskPosition),
			CreatePlayer(1002, "ELYOS", new WorldPosition(210010000, 250, 0, 0, 0)),
			CreatePlayer(1003, "ASMODIANS", kiskPosition),
			CreatePlayer(1004, "ELYOS", new WorldPosition(210010000, 10, 0, 0, 0)),
			CreatePlayer(1005, "ASMODIANS", new WorldPosition(210010000, 10, 0, 0, 0)),
			CreatePlayer(1006, "ELYOS", new WorldPosition(220010000, 10, 0, 0, 0)),
		};

		var plan = PlayerKiskUpdateFanoutService.CreatePlan(
			kisk,
			kiskPosition,
			players,
			isKnownNpc: (player, _) => player.ObjectId is not (1002 or 1003),
			excludedPlayerObjectId: 1001);

		Assert.Equal(new[] { 1002, 1003 }, plan.DirectMemberObjectIds);
		Assert.Equal(new[] { 1004 }, plan.VisibleSameRaceObjectIds);
	}

	[Fact]
	public void CreatePlanKeepsDirectMembersOutOfVisibleBroadcastSet()
	{
		var kiskPosition = new WorldPosition(210010000, 0, 0, 0, 0);
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9002,
			ownerObjectId: 2001,
			npcId: 700273,
			ownerRace: "ELYOS");
		Assert.True(kisk.AddMember(2001));
		var players = new[]
		{
			CreatePlayer(2001, "ELYOS", kiskPosition),
			CreatePlayer(2002, "ELYOS", new WorldPosition(210010000, 5, 0, 0, 0)),
		};

		var plan = PlayerKiskUpdateFanoutService.CreatePlan(
			kisk,
			kiskPosition,
			players,
			isKnownNpc: (_, _) => false);

		Assert.Equal(new[] { 2001 }, plan.DirectMemberObjectIds);
		Assert.Equal(new[] { 2002 }, plan.VisibleSameRaceObjectIds);
	}

	[Fact]
	public void CreatePlanFallsBackToVisibleSameRaceFanoutWhenKnownListCallbackIsUnavailable()
	{
		var kiskPosition = new WorldPosition(210010000, 0, 0, 0, 0);
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9003,
			ownerObjectId: 3001,
			npcId: 700273,
			ownerRace: "ELYOS");
		Assert.True(kisk.AddMember(3001));
		var players = new[]
		{
			CreatePlayer(3001, "ELYOS", kiskPosition),
			CreatePlayer(3002, "ASMODIANS", kiskPosition),
		};

		var plan = PlayerKiskUpdateFanoutService.CreatePlan(kisk, kiskPosition, players);

		Assert.Empty(plan.DirectMemberObjectIds);
		Assert.Equal(new[] { 3001 }, plan.VisibleSameRaceObjectIds);
	}

	private static Player CreatePlayer(int objectId, string race, WorldPosition position)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"player-{objectId}",
			Race = race,
			Position = position,
		};
	}
}
