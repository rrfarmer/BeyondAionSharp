using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListRegionSnapshotServiceTests
{
	[Fact]
	public void BuildSnapshot_IncludesOwnerRegionAndNeighbourPlayersWhileExcludingOwner()
	{
		var service = new PlayerKnownListRegionSnapshotService();
		var ownerRegion = new PlayerKnownListRegionKey(WorldId: 210010000, InstanceId: 1, RegionId: 10);

		var snapshot = service.BuildSnapshot(new PlayerKnownListRegionSnapshotRequest(
			OwnerPlayerObjectId,
			ownerRegion,
			NeighbourRegionIds: [11, 12],
			Players:
			[
				new PlayerKnownListRegionPlayer(OwnerPlayerObjectId, ownerRegion),
				new PlayerKnownListRegionPlayer(SameRegionPlayerObjectId, ownerRegion),
				new PlayerKnownListRegionPlayer(NeighbourPlayerObjectId, ownerRegion with { RegionId = 11 }),
				new PlayerKnownListRegionPlayer(OtherRegionPlayerObjectId, ownerRegion with { RegionId = 99 }),
			]));

		Assert.False(snapshot.IsLive);
		Assert.False(snapshot.IsJavaRegionKnownListParity);
		Assert.True(snapshot.ExcludesOwnerByNormalAddPath);
		Assert.True(snapshot.DeduplicatesByObjectId);
		Assert.Equal([10, 11, 12], snapshot.ScannedRegionIds);
		Assert.Equal([SameRegionPlayerObjectId, NeighbourPlayerObjectId], snapshot.CandidatePlayerObjectIds);
		Assert.Equal(1, snapshot.ExcludedOwnerCount);
		Assert.Equal(1, snapshot.ExcludedOutsideNeighbourRegionsCount);
	}

	[Fact]
	public void BuildSnapshot_PreservesRegionScanOrderAndDeduplicatesCandidatePlayers()
	{
		var service = new PlayerKnownListRegionSnapshotService();
		var ownerRegion = new PlayerKnownListRegionKey(WorldId: 210010000, InstanceId: 1, RegionId: 10);

		var snapshot = service.BuildSnapshot(new PlayerKnownListRegionSnapshotRequest(
			OwnerPlayerObjectId,
			ownerRegion,
			NeighbourRegionIds: [12, 11, 12, 10],
			Players:
			[
				new PlayerKnownListRegionPlayer(NeighbourPlayerObjectId, ownerRegion with { RegionId = 11 }),
				new PlayerKnownListRegionPlayer(FarNeighbourPlayerObjectId, ownerRegion with { RegionId = 12 }),
				new PlayerKnownListRegionPlayer(SameRegionPlayerObjectId, ownerRegion),
				new PlayerKnownListRegionPlayer(NeighbourPlayerObjectId, ownerRegion with { RegionId = 12 }),
			]));

		Assert.True(snapshot.PreservesSuppliedRegionOrdering);
		Assert.Equal([10, 12, 11], snapshot.ScannedRegionIds);
		Assert.Equal([SameRegionPlayerObjectId, FarNeighbourPlayerObjectId, NeighbourPlayerObjectId], snapshot.CandidatePlayerObjectIds);
	}

	[Fact]
	public void BuildSnapshot_ExcludesDifferentWorldInstanceAndUnspawnedPlayers()
	{
		var service = new PlayerKnownListRegionSnapshotService();
		var ownerRegion = new PlayerKnownListRegionKey(WorldId: 210010000, InstanceId: 7, RegionId: 10);

		var snapshot = service.BuildSnapshot(new PlayerKnownListRegionSnapshotRequest(
			OwnerPlayerObjectId,
			ownerRegion,
			NeighbourRegionIds: [11],
			Players:
			[
				new PlayerKnownListRegionPlayer(SameRegionPlayerObjectId, ownerRegion with { WorldId = 220010000 }),
				new PlayerKnownListRegionPlayer(NeighbourPlayerObjectId, ownerRegion with { InstanceId = 8 }),
				new PlayerKnownListRegionPlayer(FarNeighbourPlayerObjectId, ownerRegion with { RegionId = 11 }, IsSpawned: false),
			]));

		Assert.Empty(snapshot.CandidatePlayerObjectIds);
		Assert.Equal(2, snapshot.ExcludedDifferentWorldOrInstanceCount);
		Assert.Equal(1, snapshot.ExcludedUnspawnedCount);
	}

	private const int OwnerPlayerObjectId = 9001;
	private const int SameRegionPlayerObjectId = 9002;
	private const int NeighbourPlayerObjectId = 9003;
	private const int FarNeighbourPlayerObjectId = 9004;
	private const int OtherRegionPlayerObjectId = 9005;
}
