using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListRegionMembershipAdapterServiceTests
{
	[Fact]
	public void ApplySnapshot_UpsertsRegionCandidatesAsNonLiveMembershipMetadata()
	{
		var membership = new PlayerKnownListMembershipService();
		var adapter = new PlayerKnownListRegionMembershipAdapterService(membership);
		var regionSnapshot = CreateRegionSnapshot([NearPlayerObjectId, NeighbourPlayerObjectId]);

		var result = adapter.ApplySnapshot(new PlayerKnownListRegionMembershipAdapterRequest(regionSnapshot));

		Assert.True(result.UsesRegionSnapshotPrerequisite);
		Assert.False(result.IsJavaRegionKnownListParity);
		Assert.False(result.IsLive);
		Assert.Equal(2, result.RegionCandidateCount);
		Assert.Equal(2, result.UpsertedCandidateCount);
		Assert.Equal([NearPlayerObjectId, NeighbourPlayerObjectId], result.MembershipSnapshot.KnownPlayerObjectIds);
		Assert.All(result.MembershipSnapshot.Entries, entry =>
		{
			Assert.True(entry.IsVisibleToOwner);
			Assert.False(entry.IsLive);
			Assert.Equal(PlayerKnownListMembershipUpdateReason.RegionSnapshotRefresh, entry.UpdateReason);
		});
	}

	[Fact]
	public void ApplySnapshot_CanPreserveExistingMembershipByDefaultBecauseJavaCanKeepInvisibleKnownObjects()
	{
		var membership = new PlayerKnownListMembershipService();
		var adapter = new PlayerKnownListRegionMembershipAdapterService(membership);
		membership.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[new PlayerKnownListMembershipCandidate(StalePlayerObjectId, IsVisibleToOwner: false)]);
		var regionSnapshot = CreateRegionSnapshot([NearPlayerObjectId]);

		var result = adapter.ApplySnapshot(new PlayerKnownListRegionMembershipAdapterRequest(regionSnapshot));

		Assert.Equal(0, result.RemovedStalePlayerCount);
		Assert.False(result.RemoveMissingSnapshotCandidates);
		Assert.Equal([StalePlayerObjectId, NearPlayerObjectId], result.MembershipSnapshot.KnownPlayerObjectIds);
		Assert.False(result.MembershipSnapshot.Entries.Single(entry => entry.KnownPlayerObjectId == StalePlayerObjectId).IsVisibleToOwner);
	}

	[Fact]
	public void ApplySnapshot_CanRemoveMissingSnapshotCandidatesWhenRequested()
	{
		var membership = new PlayerKnownListMembershipService();
		var adapter = new PlayerKnownListRegionMembershipAdapterService(membership);
		membership.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(StalePlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(NearPlayerObjectId, IsVisibleToOwner: true),
			]);
		var regionSnapshot = CreateRegionSnapshot([NearPlayerObjectId]);

		var result = adapter.ApplySnapshot(new PlayerKnownListRegionMembershipAdapterRequest(
			regionSnapshot,
			RemoveMissingSnapshotCandidates: true));

		Assert.True(result.RemoveMissingSnapshotCandidates);
		Assert.Equal(1, result.RemovedStalePlayerCount);
		Assert.Equal([NearPlayerObjectId], result.MembershipSnapshot.KnownPlayerObjectIds);
	}

	[Fact]
	public void ApplySnapshot_CanRecordInvisibleCandidateStateWhenCallerDoesNotHaveCanSeeParity()
	{
		var membership = new PlayerKnownListMembershipService();
		var adapter = new PlayerKnownListRegionMembershipAdapterService(membership);
		var regionSnapshot = CreateRegionSnapshot([NearPlayerObjectId]);

		var result = adapter.ApplySnapshot(new PlayerKnownListRegionMembershipAdapterRequest(
			regionSnapshot,
			CandidateVisibleState: false));

		var entry = Assert.Single(result.MembershipSnapshot.Entries);
		Assert.False(result.CandidateVisibleState);
		Assert.False(entry.IsVisibleToOwner);
		Assert.Equal(PlayerKnownListMembershipUpdateReason.RegionSnapshotRefresh, entry.UpdateReason);
	}

	private static PlayerKnownListRegionSnapshot CreateRegionSnapshot(IReadOnlyList<int> candidateIds)
	{
		var ownerRegion = new PlayerKnownListRegionKey(WorldId: 210010000, InstanceId: 1, RegionId: 10);
		return new PlayerKnownListRegionSnapshot(
			OwnerPlayerObjectId,
			ownerRegion,
			ScannedRegionIds: [10, 11],
			CandidatePlayerObjectIds: candidateIds,
			SourcePlayerCount: candidateIds.Count + 1,
			ExcludedOwnerCount: 1,
			ExcludedDifferentWorldOrInstanceCount: 0,
			ExcludedOutsideNeighbourRegionsCount: 0,
			ExcludedUnspawnedCount: 0,
			ExcludesOwnerByNormalAddPath: true,
			DeduplicatesByObjectId: true,
			PreservesSuppliedRegionOrdering: true,
			IsJavaRegionKnownListParity: false,
			"test region snapshot",
			IsLive: false);
	}

	private const int OwnerPlayerObjectId = 9001;
	private const int NearPlayerObjectId = 9002;
	private const int NeighbourPlayerObjectId = 9003;
	private const int StalePlayerObjectId = 9004;
}
