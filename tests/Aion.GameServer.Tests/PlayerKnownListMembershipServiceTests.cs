using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListMembershipServiceTests
{
	[Fact]
	public void UpsertKnownPlayers_ExcludesOwnerAndDeduplicatesKnownPlayerObjectIds()
	{
		var service = new PlayerKnownListMembershipService();

		var snapshot = service.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(OwnerPlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(KnownPlayerObjectId, IsVisibleToOwner: true, "KnownList.putIfAbsent"),
				new PlayerKnownListMembershipCandidate(KnownPlayerObjectId, IsVisibleToOwner: false, "KnownList.updateKnownEntry"),
			]);

		Assert.True(snapshot.ExcludesOwnerByNormalAddPath);
		Assert.True(snapshot.DeduplicatesByObjectId);
		Assert.False(snapshot.IsLive);
		var entry = Assert.Single(snapshot.Entries);
		Assert.Equal(OwnerPlayerObjectId, entry.OwnerPlayerObjectId);
		Assert.Equal(KnownPlayerObjectId, entry.KnownPlayerObjectId);
		Assert.False(entry.IsVisibleToOwner);
		Assert.Equal("KnownList.updateKnownEntry", entry.JavaSource);
	}

	[Fact]
	public void GetKnownPlayerObjectIds_IncludesInvisibleByDefaultAndCanFilterVisibleOnly()
	{
		var service = new PlayerKnownListMembershipService();

		service.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(KnownPlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(KnownInvisiblePlayerObjectId, IsVisibleToOwner: false),
			]);

		Assert.Equal(
			[KnownPlayerObjectId, KnownInvisiblePlayerObjectId],
			service.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Equal(
			[KnownPlayerObjectId],
			service.GetKnownPlayerObjectIds(OwnerPlayerObjectId, includeInvisible: false));
	}

	[Fact]
	public void TrySetKnownPlayerVisibility_UpdatesExistingMembershipWithoutDroppingInvisibleEntry()
	{
		var service = new PlayerKnownListMembershipService();
		service.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[new PlayerKnownListMembershipCandidate(KnownPlayerObjectId, IsVisibleToOwner: true)]);

		var updated = service.TrySetKnownPlayerVisibility(
			OwnerPlayerObjectId,
			KnownPlayerObjectId,
			isVisibleToOwner: false,
			out var snapshot);

		Assert.True(updated);
		var entry = Assert.Single(snapshot.Entries);
		Assert.False(entry.IsVisibleToOwner);
		Assert.Equal(PlayerKnownListMembershipUpdateReason.VisibilityChanged, entry.UpdateReason);
		Assert.Equal([KnownPlayerObjectId], service.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Empty(service.GetKnownPlayerObjectIds(OwnerPlayerObjectId, includeInvisible: false));
	}

	[Fact]
	public void RemoveAndClearKnownPlayers_RemoveMembershipEntries()
	{
		var service = new PlayerKnownListMembershipService();
		service.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(KnownPlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(KnownInvisiblePlayerObjectId, IsVisibleToOwner: false),
			]);

		var removed = service.RemoveKnownPlayer(OwnerPlayerObjectId, KnownPlayerObjectId, out var afterRemove);
		var afterClear = service.ClearKnownPlayers(OwnerPlayerObjectId);

		Assert.True(removed);
		Assert.Equal([KnownInvisiblePlayerObjectId], afterRemove.KnownPlayerObjectIds);
		Assert.Empty(afterClear.Entries);
		Assert.Empty(service.GetSnapshot(OwnerPlayerObjectId).Entries);
	}

	private const int OwnerPlayerObjectId = 8501;
	private const int KnownPlayerObjectId = 8502;
	private const int KnownInvisiblePlayerObjectId = 8503;
}
