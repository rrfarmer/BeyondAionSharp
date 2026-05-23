using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Aion.GameServer.Dataholders;

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

	[Fact]
	public void CreatePlanUsesNpcVisibilityKnownListForDirectMemberFallback()
	{
		var npcVisibility = new NpcVisibilityService();
		var kiskPosition = new WorldPosition(210010000, 0, 0, 0, 0);
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9004,
			ownerObjectId: 4001,
			npcId: 700273,
			ownerRace: "ELYOS");
		Assert.True(kisk.AddMember(4001));
		Assert.True(kisk.AddMember(4002));
		Assert.True(kisk.AddMember(4003));
		var kiskNpc = CreateKiskNpc(kisk.ObjectId, kiskPosition);
		var players = new[]
		{
			CreatePlayer(4001, "ELYOS", kiskPosition),
			CreatePlayer(4002, "ELYOS", kiskPosition),
			CreatePlayer(4003, "ASMODIANS", new WorldPosition(210010000, 250, 0, 0, 0)),
			CreatePlayer(4004, "ELYOS", new WorldPosition(210010000, 5, 0, 0, 0)),
		};
		npcVisibility.UpdateKnownNpcs(players[0], [kiskNpc]);
		npcVisibility.UpdateKnownNpcs(players[1], [kiskNpc]);

		var plan = PlayerKiskUpdateFanoutService.CreatePlan(
			kisk,
			kiskPosition,
			players,
			npcVisibility.IsKnownNpc);

		Assert.Equal(new[] { 4003 }, plan.DirectMemberObjectIds);
		Assert.Equal(new[] { 4001, 4002, 4004 }, plan.VisibleSameRaceObjectIds);
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

	private static WorldNpc CreateKiskNpc(int objectId, WorldPosition position)
	{
		var template = new NpcTemplateSummary(
			700273,
			"kisk",
			NameId: 1,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL",
			KiskStats: new KiskStatsSummary());
		return new WorldNpc(objectId, template.TemplateId, template, position);
	}
}
