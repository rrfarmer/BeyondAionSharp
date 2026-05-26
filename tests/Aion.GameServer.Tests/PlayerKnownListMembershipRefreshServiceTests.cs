using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListMembershipRefreshServiceTests
{
	[Fact]
	public void RefreshOwnerFromOnlinePlayers_UsesWorldVisibilityApproximationAndExcludesOwner()
	{
		var membership = new PlayerKnownListMembershipService();
		var refresh = new PlayerKnownListMembershipRefreshService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, 0, 0, 0);
		var near = CreatePlayer(NearPlayerObjectId, 94, 0, 0);
		var far = CreatePlayer(FarPlayerObjectId, 96, 0, 0);
		var otherWorld = CreatePlayer(OtherWorldPlayerObjectId, 1, 0, 0, worldId: 220010000);

		var result = refresh.RefreshOwnerFromOnlinePlayers(owner, [owner, near, far, otherWorld]);

		Assert.True(result.UsesWorldVisibilityApproximation);
		Assert.False(result.IsJavaRegionKnownListParity);
		Assert.False(result.IsLive);
		Assert.Equal(4, result.CandidateCount);
		Assert.Equal(1, result.UpsertedVisiblePlayerCount);
		Assert.Equal([NearPlayerObjectId], result.Snapshot.KnownPlayerObjectIds);
		var entry = Assert.Single(result.Snapshot.Entries);
		Assert.True(entry.IsVisibleToOwner);
		Assert.Equal(PlayerKnownListMembershipUpdateReason.WorldVisibilityRefresh, entry.UpdateReason);
	}

	[Fact]
	public void RefreshOwnerFromOnlinePlayers_RemovesStaleOutOfRangeMembership()
	{
		var membership = new PlayerKnownListMembershipService();
		var refresh = new PlayerKnownListMembershipRefreshService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, 0, 0, 0);
		membership.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(NearPlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(FarPlayerObjectId, IsVisibleToOwner: true),
			]);

		var result = refresh.RefreshOwnerFromOnlinePlayers(
			owner,
			[owner, CreatePlayer(NearPlayerObjectId, 94, 0, 0), CreatePlayer(FarPlayerObjectId, 96, 0, 0)]);

		Assert.Equal(1, result.RemovedStalePlayerCount);
		Assert.Equal([NearPlayerObjectId], result.Snapshot.KnownPlayerObjectIds);
	}

	[Fact]
	public void RefreshAllFromOnlinePlayers_ProducesBidirectionalDistanceApproximation()
	{
		var membership = new PlayerKnownListMembershipService();
		var refresh = new PlayerKnownListMembershipRefreshService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, 0, 0, 0);
		var near = CreatePlayer(NearPlayerObjectId, 94, 0, 0);
		var far = CreatePlayer(FarPlayerObjectId, 200, 0, 0);

		var results = refresh.RefreshAllFromOnlinePlayers([owner, near, far]);

		Assert.Equal(3, results.Count);
		Assert.Equal([NearPlayerObjectId], membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Equal([OwnerPlayerObjectId], membership.GetKnownPlayerObjectIds(NearPlayerObjectId));
		Assert.Empty(membership.GetKnownPlayerObjectIds(FarPlayerObjectId));
	}

	[Fact]
	public void ClearOwnerForLogoutAndRemoveDepartingPlayerFromKnownLists_RemoveMembershipMetadata()
	{
		var membership = new PlayerKnownListMembershipService();
		var refresh = new PlayerKnownListMembershipRefreshService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, 0, 0, 0);
		var near = CreatePlayer(NearPlayerObjectId, 94, 0, 0);
		refresh.RefreshAllFromOnlinePlayers([owner, near]);

		var ownerSnapshot = refresh.ClearOwnerForLogout(OwnerPlayerObjectId);
		var removedFromOthers = refresh.RemoveDepartingPlayerFromKnownLists(OwnerPlayerObjectId, [near]);

		Assert.Empty(ownerSnapshot.Entries);
		Assert.Equal(1, removedFromOthers);
		Assert.Empty(membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Empty(membership.GetKnownPlayerObjectIds(NearPlayerObjectId));
	}

	private static Player CreatePlayer(
		int objectId,
		float x,
		float y,
		float z,
		int worldId = 210010000) =>
		new()
		{
			ObjectId = objectId,
			Position = new WorldPosition(worldId, x, y, z, Heading: 0),
		};

	private const int OwnerPlayerObjectId = 9001;
	private const int NearPlayerObjectId = 9002;
	private const int FarPlayerObjectId = 9003;
	private const int OtherWorldPlayerObjectId = 9004;
}
