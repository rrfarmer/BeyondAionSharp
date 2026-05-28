using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldRegionKeyProjectionServiceTests
{
	[Fact]
	public void CreateNearby2DRegionKey_DerivesJava2DRegionIdFromWorldPosition()
	{
		var position = new WorldPosition(WorldId: 210010000, X: 256.9f, Y: 384.1f, Z: 999.9f, Heading: 40, InstanceId: 7);

		var key = WorldRegionKeyProjectionService.CreateNearby2DRegionKey(position);

		Assert.Equal(210010000, key.WorldId);
		Assert.Equal(7, key.InstanceId);
		Assert.Equal(2003, key.RegionId);
	}

	[Fact]
	public void CreateNearby3DRegionKey_DerivesJava3DRegionIdFromWorldPosition()
	{
		var position = new WorldPosition(WorldId: 210010000, X: 256.9f, Y: 384.1f, Z: 512.5f, Heading: 40, InstanceId: 7);

		var key = WorldRegionKeyProjectionService.CreateNearby3DRegionKey(position);

		Assert.Equal(210010000, key.WorldId);
		Assert.Equal(7, key.InstanceId);
		Assert.Equal(2003004, key.RegionId);
	}

	[Fact]
	public void CreateKnownListRegionKeys_UseSameJavaRegionMathAsNearbyKeys()
	{
		var position = new WorldPosition(WorldId: 220010000, X: 128f, Y: 256f, Z: 384f, Heading: 90, InstanceId: 8);

		var key2D = WorldRegionKeyProjectionService.CreateKnownList2DRegionKey(position);
		var key3D = WorldRegionKeyProjectionService.CreateKnownList3DRegionKey(position);

		Assert.Equal(new PlayerKnownListRegionKey(220010000, 8, 1002), key2D);
		Assert.Equal(new PlayerKnownListRegionKey(220010000, 8, 1002003), key3D);
	}

	[Fact]
	public void BuildSnapshot_UsesDerivedNearbyRegionKeysForSameInstanceFiltering()
	{
		var service = new NearbyQuestRegionSnapshotService();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7);
		var included = CreatePlayer(1001, new WorldPosition(210010000, 256f, 384f, 512f, 40, InstanceId: 7));
		var otherInstance = CreatePlayer(1002, new WorldPosition(210010000, 256f, 384f, 512f, 40, InstanceId: 8));

		var snapshot = service.BuildSnapshot(new NearbyQuestRegionSnapshotRequest(
			210010000,
			7,
			[
				new NearbyQuestRegionPlayer(included, WorldRegionKeyProjectionService.CreateNearby3DRegionKey(included.Position), instance),
				new NearbyQuestRegionPlayer(otherInstance, WorldRegionKeyProjectionService.CreateNearby3DRegionKey(otherInstance.Position), instance),
			]));

		var input = Assert.Single(snapshot.PlayerInputs);
		Assert.Same(included, input.Player);
		Assert.Equal(2003004, input.MapRegion?.Position is { } position
			? WorldRegionIdService.Get3DRegionId(position.X, position.Y, position.Z)
			: -1);
		Assert.Equal(1, snapshot.ExcludedDifferentWorldOrInstanceCount);
	}

	[Fact]
	public void BuildSnapshot_UsesDerivedKnownListRegionKeysForNeighbourScan()
	{
		var service = new PlayerKnownListRegionSnapshotService();
		var ownerRegion = WorldRegionKeyProjectionService.CreateKnownList2DRegionKey(
			new WorldPosition(210010000, 256f, 384f, 0f, 40, InstanceId: 7));
		var neighbourRegion = WorldRegionKeyProjectionService.CreateKnownList2DRegionKey(
			new WorldPosition(210010000, 384f, 384f, 0f, 40, InstanceId: 7));

		var snapshot = service.BuildSnapshot(new PlayerKnownListRegionSnapshotRequest(
			OwnerPlayerObjectId,
			ownerRegion,
			NeighbourRegionIds: [neighbourRegion.RegionId],
			Players:
			[
				new PlayerKnownListRegionPlayer(OwnerPlayerObjectId, ownerRegion),
				new PlayerKnownListRegionPlayer(NeighbourPlayerObjectId, neighbourRegion),
			]));

		Assert.Equal([2003, 3003], snapshot.ScannedRegionIds);
		Assert.Equal([NeighbourPlayerObjectId], snapshot.CandidatePlayerObjectIds);
		Assert.Equal(1, snapshot.ExcludedOwnerCount);
	}

	private const int OwnerPlayerObjectId = 9001;
	private const int NeighbourPlayerObjectId = 9002;

	private static Player CreatePlayer(int objectId, WorldPosition position)
	{
		return new Player
		{
			ObjectId = objectId,
			Level = 20,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			Position = position,
		};
	}
}
