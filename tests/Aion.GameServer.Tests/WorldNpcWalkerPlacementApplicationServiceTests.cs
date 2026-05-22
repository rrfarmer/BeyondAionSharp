using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerPlacementApplicationServiceTests
{
	[Fact]
	public void ApplyActivePlacements_UpdatesLiveWorldNpcPositions()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = new WorldNpcWalkerPlacementApplicationService();
		var originalSpawn = new WorldPosition(210010000, 0, 0, 3, 44);
		world.TryAddObject(1, Npc(1, originalSpawn));
		var placementPlan = new WorldNpcWalkerPlacementPlan(
			[
				new WorldNpcWalkerPlacement(1, 203000, "route-a", IsFormationMember: true, X: 10, Y: 20, Z: 30, Heading: 60),
			],
			[]);

		var result = service.ApplyActivePlacements(world, placementPlan);

		Assert.Equal([1], result.UpdatedObjectIds);
		Assert.Empty(result.MissingObjectIds);
		Assert.True(world.TryGetObject(1, out var gameObject));
		var npc = Assert.IsType<WorldNpc>(gameObject);
		Assert.Equal(new WorldPosition(210010000, 10, 20, 30, 60), npc.Position);
		Assert.Equal(originalSpawn, npc.SpawnLocation);
	}

	[Fact]
	public void ApplyActivePlacements_ReportsMissingObjects()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = new WorldNpcWalkerPlacementApplicationService();
		var placementPlan = new WorldNpcWalkerPlacementPlan(
			[
				new WorldNpcWalkerPlacement(99, 203000, "route-a", IsFormationMember: true, X: 10, Y: 20, Z: 30, Heading: 60),
			],
			[]);

		var result = service.ApplyActivePlacements(world, placementPlan);

		Assert.Empty(result.UpdatedObjectIds);
		Assert.Equal([99], result.MissingObjectIds);
	}

	private static WorldNpc Npc(int objectId, WorldPosition spawnPosition)
	{
		return new WorldNpc(
			ObjectId: objectId,
			TemplateId: 203000,
			Template: new NpcTemplateSummary(
				203000,
				$"walker-{objectId}",
				NameId: 203000,
				Level: 1,
				Rank: "NORMAL",
				Rating: "NORMAL",
				Race: "ELYOS",
				Tribe: "GENERAL",
				Type: "GENERAL"),
			Position: spawnPosition,
			SpawnPosition: spawnPosition);
	}
}
