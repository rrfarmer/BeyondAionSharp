using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestRegionSnapshotServiceTests
{
	[Fact]
	public void BuildSnapshot_AssemblesSpawnedSameInstancePlayersForDelayedRefresh()
	{
		var service = new NearbyQuestRegionSnapshotService();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7);
		var first = CreatePlayer(1001, new WorldPosition(WorldId, 10, 20, 30, 40, InstanceId));
		var second = CreatePlayer(1002, new WorldPosition(WorldId, 11, 21, 31, 41, InstanceId));

		var snapshot = service.BuildSnapshot(new NearbyQuestRegionSnapshotRequest(
			WorldId,
			InstanceId,
			[
				new NearbyQuestRegionPlayer(first, new NearbyQuestRegionKey(WorldId, InstanceId, RegionId: 10), instance),
				new NearbyQuestRegionPlayer(second, new NearbyQuestRegionKey(WorldId, InstanceId, RegionId: 11), instance),
			]));

		Assert.False(snapshot.IsLive);
		Assert.True(snapshot.PreservesSuppliedPlayerOrdering);
		Assert.Equal(2, snapshot.SourcePlayerCount);
		Assert.Equal(0, snapshot.ExcludedDifferentWorldOrInstanceCount);
		Assert.Equal(0, snapshot.ExcludedUnspawnedCount);
		Assert.Collection(
			snapshot.PlayerInputs,
			input =>
			{
				Assert.Same(first, input.Player);
				Assert.Equal(first.Position, input.MapRegion?.Position);
				Assert.Same(instance, input.MapRegion?.ParentWorldInstance);
			},
			input =>
			{
				Assert.Same(second, input.Player);
				Assert.Equal(second.Position, input.MapRegion?.Position);
				Assert.Same(instance, input.MapRegion?.ParentWorldInstance);
			});
	}

	[Fact]
	public void BuildSnapshot_ExcludesUnspawnedAndDifferentWorldOrInstancePlayers()
	{
		var service = new NearbyQuestRegionSnapshotService();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7);
		var included = CreatePlayer(1001, new WorldPosition(WorldId, 10, 20, 30, 40, InstanceId));
		var otherWorld = CreatePlayer(1002, new WorldPosition(220010000, 11, 21, 31, 41, InstanceId));
		var otherInstance = CreatePlayer(1003, new WorldPosition(WorldId, 12, 22, 32, 42, InstanceId: 8));
		var unspawned = CreatePlayer(1004, new WorldPosition(WorldId, 13, 23, 33, 43, InstanceId));

		var snapshot = service.BuildSnapshot(new NearbyQuestRegionSnapshotRequest(
			WorldId,
			InstanceId,
			[
				new NearbyQuestRegionPlayer(included, new NearbyQuestRegionKey(WorldId, InstanceId, RegionId: 10), instance),
				new NearbyQuestRegionPlayer(otherWorld, new NearbyQuestRegionKey(220010000, InstanceId, RegionId: 10), instance),
				new NearbyQuestRegionPlayer(otherInstance, new NearbyQuestRegionKey(WorldId, 8, RegionId: 10), instance),
				new NearbyQuestRegionPlayer(unspawned, new NearbyQuestRegionKey(WorldId, InstanceId, RegionId: 10), instance, IsSpawned: false),
			]));

		var input = Assert.Single(snapshot.PlayerInputs);
		Assert.Same(included, input.Player);
		Assert.Equal(4, snapshot.SourcePlayerCount);
		Assert.Equal(2, snapshot.ExcludedDifferentWorldOrInstanceCount);
		Assert.Equal(1, snapshot.ExcludedUnspawnedCount);
	}

	[Fact]
	public void BuildSnapshot_DoesNotApplyKnownListOwnerOrNeighbourFiltering()
	{
		var service = new NearbyQuestRegionSnapshotService();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7);
		var ownerLikePlayer = CreatePlayer(1001, new WorldPosition(WorldId, 10, 20, 30, 40, InstanceId));
		var distantRegionPlayer = CreatePlayer(1002, new WorldPosition(WorldId, 900, 900, 30, 40, InstanceId));

		var snapshot = service.BuildSnapshot(new NearbyQuestRegionSnapshotRequest(
			WorldId,
			InstanceId,
			[
				new NearbyQuestRegionPlayer(ownerLikePlayer, new NearbyQuestRegionKey(WorldId, InstanceId, RegionId: 10), instance),
				new NearbyQuestRegionPlayer(distantRegionPlayer, new NearbyQuestRegionKey(WorldId, InstanceId, RegionId: 99), instance),
			]));

		Assert.Equal([1001, 1002], snapshot.PlayerInputs.Select(input => input.Player.ObjectId));
	}

	private const int WorldId = 210010000;
	private const int InstanceId = 7;

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
