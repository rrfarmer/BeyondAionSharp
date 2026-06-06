using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerTeleportToNpcRequestServiceTests
{
	[Fact]
	public void GetFirstSpawnByNpcId_SearchesPlayerWorldBeforeOtherWorlds()
	{
		var table = new NpcSpawnTable(
		[
			CreateSpawn(220010000, 203001, x: 1),
			CreateSpawn(210010000, 203001, x: 2),
			CreateSpawn(210010000, 203002, x: 3),
		]);

		var sameWorld = table.GetFirstSpawnByNpcId(210010000, 203001);
		var fallbackWorld = table.GetFirstSpawnByNpcId(300010000, 203001);
		var missing = table.GetFirstSpawnByNpcId(210010000, 203999);

		Assert.NotNull(sameWorld);
		Assert.Equal(210010000, sameWorld.MapId);
		Assert.Equal(2, sameWorld.X);
		Assert.NotNull(fallbackWorld);
		Assert.Equal(220010000, fallbackWorld.MapId);
		Assert.Equal(1, fallbackWorld.X);
		Assert.Null(missing);
	}

	[Fact]
	public void GetNearestSpawnByNpcId_SearchesNearestSpawnOnPlayerWorld()
	{
		var table = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203001, x: 500, y: 500, z: 0),
			CreateSpawn(210010000, 203001, x: 11, y: 20, z: 30),
			CreateSpawn(210010000, 203001, x: 100, y: 100, z: 0),
		]);
		var playerPosition = new WorldPosition(210010000, 10, 20, 30, 0);

		var spawn = table.GetNearestSpawnByNpcId(
			playerPosition,
			"ELYOS",
			CreateWorldMaps(),
			203001);

		Assert.NotNull(spawn);
		Assert.Equal(210010000, spawn.MapId);
		Assert.Equal(11, spawn.X);
		Assert.Equal(20, spawn.Y);
		Assert.Equal(30, spawn.Z);
	}

	[Fact]
	public void GetNearestSpawnByNpcId_SearchesSameRaceWorldsBeforeOtherWorlds()
	{
		var table = new NpcSpawnTable(
		[
			CreateSpawn(220010000, 203001, x: 1),
			CreateSpawn(210030000, 203001, x: 2),
		]);
		var playerPosition = new WorldPosition(210010000, 10, 20, 30, 0);

		var spawn = table.GetNearestSpawnByNpcId(
			playerPosition,
			"ELYOS",
			CreateWorldMaps(),
			203001);

		Assert.NotNull(spawn);
		Assert.Equal(210030000, spawn.MapId);
		Assert.Equal(2, spawn.X);
	}

	[Fact]
	public void GetNearestSpawnByNpcId_WhenOffWorldUsesFirstSpawnWithoutDistanceSort()
	{
		var table = new NpcSpawnTable(
		[
			CreateSpawn(220010000, 203001, x: 500, y: 500),
			CreateSpawn(220010000, 203001, x: 10, y: 20),
		]);
		var playerPosition = new WorldPosition(210010000, 10, 20, 30, 0);

		var spawn = table.GetNearestSpawnByNpcId(
			playerPosition,
			"ELYOS",
			CreateWorldMaps(),
			203001);

		Assert.NotNull(spawn);
		Assert.Equal(220010000, spawn.MapId);
		Assert.Equal(500, spawn.X);
	}

	[Fact]
	public void SendTeleportRequest_RegistersQuestionWindowAndAcceptComputesJavaDestination()
	{
		var service = new PlayerTeleportToNpcRequestService();
		var player = CreatePlayer();
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203001, x: 100, y: 200, z: 30, heading: 30)]);
		var templates = CreateTemplates(new NpcTemplateSummary(
			203001,
			"teleport_npc",
			0,
			1,
			"NORMAL",
			"NORMAL",
			"NONE",
			"NONE",
			"NPC",
			BoundRadius: 2.5f));
		var result = service.SendTeleportRequest(
			player,
			203001,
			templates);

		Assert.Equal(TeleportToNpcRequestStatus.Requested, result.Status);
		Assert.Equal(SmQuestionWindow.TeleportToNpcConfirm, result.QuestionWindow?.Code);
		Assert.Equal(1, player.ResponseRequester.Count);
		Assert.NotNull(result.Request);
		Assert.Equal("teleport_npc", result.Request.NpcName);
		var accepted = service.HandleResponse(player, SmQuestionWindow.TeleportToNpcConfirm, response: 1, spawns, templates);
		Assert.Equal(TeleportToNpcResponseStatus.Accepted, accepted.Status);
		Assert.NotNull(accepted.ResolvedDestination);
		Assert.Equal(new WorldPosition(210010000, 100, 200, 30, 30), accepted.ResolvedDestination.SpawnPosition);
		Assert.Equal(2.5f, accepted.ResolvedDestination.NpcRadius);
		Assert.Equal(210010000, accepted.ResolvedDestination.Destination.WorldId);
		Assert.Equal(2, accepted.ResolvedDestination.Destination.InstanceId);
		Assert.Equal(100, accepted.ResolvedDestination.Destination.X, precision: 5);
		Assert.Equal(203.5f, accepted.ResolvedDestination.Destination.Y, precision: 5);
		Assert.Equal(30.5f, accepted.ResolvedDestination.Destination.Z);
		Assert.Equal(90, accepted.ResolvedDestination.Destination.Heading);
	}

	[Fact]
	public void SendTeleportRequest_DuplicateQuestionLeavesOriginalRequest()
	{
		var service = new PlayerTeleportToNpcRequestService();
		var player = CreatePlayer();
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203001), CreateSpawn(210010000, 203002)]);
		var templates = CreateTemplates(
			new NpcTemplateSummary(203001, "first_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
			new NpcTemplateSummary(203002, "second_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"));

		var first = service.SendTeleportRequest(player, 203001, templates);
		var duplicate = service.SendTeleportRequest(player, 203002, templates);

		Assert.Equal(TeleportToNpcRequestStatus.Requested, first.Status);
		Assert.Equal(TeleportToNpcRequestStatus.DuplicateRequest, duplicate.Status);
		Assert.Equal(1, player.ResponseRequester.Count);
		var denied = service.HandleResponse(player, SmQuestionWindow.TeleportToNpcConfirm, response: 0, spawns, templates);
		Assert.Equal(203001, denied.Request?.NpcId);
	}

	[Fact]
	public void HandleResponse_DenyConsumesRequestAndDoesNotMovePlayer()
	{
		var service = new PlayerTeleportToNpcRequestService();
		var player = CreatePlayer();
		var originalPosition = player.Position;
		service.SendTeleportRequest(
			player,
			203001,
			CreateTemplates(new NpcTemplateSummary(203001, "teleport_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC")));

		var response = service.HandleResponse(
			player,
			SmQuestionWindow.TeleportToNpcConfirm,
			response: 0,
			new NpcSpawnTable([CreateSpawn(210010000, 203001, x: 100)]),
			CreateTemplates(new NpcTemplateSummary(203001, "teleport_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC")));

		Assert.Equal(TeleportToNpcResponseStatus.Denied, response.Status);
		Assert.Equal(203001, response.Request?.NpcId);
		Assert.Null(response.Teleport);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Equal(originalPosition, player.Position);
	}

	[Fact]
	public void HandleResponse_AcceptConsumesRequestAndTeleportsWithNoneArrival()
	{
		var service = new PlayerTeleportToNpcRequestService();
		var player = CreatePlayer();
		var originalPosition = player.Position;
		service.SendTeleportRequest(
			player,
			203001,
			CreateTemplates(new NpcTemplateSummary(
				203001,
				"teleport_npc",
				0,
				1,
				"NORMAL",
				"NORMAL",
				"NONE",
				"NONE",
				"NPC",
				BoundRadius: 1)));

		var response = service.HandleResponse(
			player,
			SmQuestionWindow.TeleportToNpcConfirm,
			response: 1,
			new NpcSpawnTable([CreateSpawn(210010000, 203001, x: 100, y: 200, z: 30, heading: 80)]),
			CreateTemplates(new NpcTemplateSummary(
				203001,
				"teleport_npc",
				0,
				1,
				"NORMAL",
				"NORMAL",
				"NONE",
				"NONE",
				"NPC",
				BoundRadius: 1)));

		Assert.Equal(TeleportToNpcResponseStatus.Accepted, response.Status);
		Assert.NotNull(response.Teleport);
		Assert.Equal(originalPosition, response.Teleport.PreviousPosition);
		Assert.Equal(response.Teleport.Destination, player.Position);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Equal(20, player.Position.Heading);
		Assert.Equal(ArrivalAnimation.Landing, player.PortAnimation);
		Assert.Equal(player.Position.X, player.Movement.TargetX);
		Assert.Equal(player.Position.Y, player.Movement.TargetY);
		Assert.Equal(player.Position.Z, player.Movement.TargetZ);
	}

	[Fact]
	public void HandleResponse_WrongQuestionLeavesRegisteredRequest()
	{
		var service = new PlayerTeleportToNpcRequestService();
		var player = CreatePlayer();
		service.SendTeleportRequest(
			player,
			203001,
			CreateTemplates(new NpcTemplateSummary(203001, "teleport_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC")));

		var response = service.HandleResponse(
			player,
			SmQuestionWindow.BuddyListAddBuddyRequest,
			response: 1,
			new NpcSpawnTable([CreateSpawn(210010000, 203001)]),
			CreateTemplates(new NpcTemplateSummary(203001, "teleport_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC")));

		Assert.Equal(TeleportToNpcResponseStatus.Ignored, response.Status);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	[Fact]
	public void HandleResponse_NoSpawnConsumesRequestAndDoesNotMovePlayer()
	{
		var service = new PlayerTeleportToNpcRequestService();
		var player = CreatePlayer();
		var originalPosition = player.Position;
		var templates = CreateTemplates(new NpcTemplateSummary(203001, "teleport_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"));
		service.SendTeleportRequest(player, 203001, templates);

		var response = service.HandleResponse(
			player,
			SmQuestionWindow.TeleportToNpcConfirm,
			response: 1,
			new NpcSpawnTable([]),
			templates);

		Assert.Equal(TeleportToNpcResponseStatus.NoSpawnFound, response.Status);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Equal(originalPosition, player.Position);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Player",
			Position = new WorldPosition(210010000, 10, 20, 30, 0, InstanceId: 2),
		};
	}

	private static NpcTemplateTable CreateTemplates(params NpcTemplateSummary[] templates)
	{
		return new NpcTemplateTable(templates);
	}

	private static IReadOnlyList<WorldMapSummary> CreateWorldMaps()
	{
		return
		[
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 0, WorldType: "ELYSEA"),
			new WorldMapSummary(210030000, IsInstance: false, TwinCount: 0, WorldType: "ELYSEA"),
			new WorldMapSummary(220010000, IsInstance: false, TwinCount: 0, WorldType: "ASMODAE"),
		];
	}

	private static NpcSpawnSummary CreateSpawn(
		int mapId,
		int npcId,
		float x = 10,
		float y = 20,
		float z = 30,
		byte heading = 0)
	{
		return new NpcSpawnSummary(
			mapId,
			npcId,
			x,
			y,
			z,
			heading,
			RespawnSeconds: 0,
			PoolSize: 0,
			DifficultId: 0,
			Handler: string.Empty,
			StaticId: 0,
			RandomWalkRange: 0,
			WalkerId: string.Empty,
			WalkerIndex: 0,
			Anchor: string.Empty,
			State: 0,
			AiName: string.Empty,
			Custom: false,
			GroupTemporarySchedule: null,
			SpotTemporarySchedule: null);
	}
}
