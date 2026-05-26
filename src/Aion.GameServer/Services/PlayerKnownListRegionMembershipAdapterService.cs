namespace Aion.GameServer.Services;

public sealed record PlayerKnownListRegionMembershipAdapterRequest(
	PlayerKnownListRegionSnapshot RegionSnapshot,
	bool RemoveMissingSnapshotCandidates = false,
	bool CandidateVisibleState = true);

public sealed record PlayerKnownListRegionMembershipAdapterResult(
	int OwnerPlayerObjectId,
	PlayerKnownListMembershipSnapshot MembershipSnapshot,
	int RegionCandidateCount,
	int UpsertedCandidateCount,
	int RemovedStalePlayerCount,
	bool RemoveMissingSnapshotCandidates,
	bool CandidateVisibleState,
	bool UsesRegionSnapshotPrerequisite,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed class PlayerKnownListRegionMembershipAdapterService
{
	private readonly PlayerKnownListMembershipService _membershipService;

	public PlayerKnownListRegionMembershipAdapterService(PlayerKnownListMembershipService membershipService)
	{
		_membershipService = membershipService;
	}

	public PlayerKnownListRegionMembershipAdapterResult ApplySnapshot(PlayerKnownListRegionMembershipAdapterRequest request)
	{
		var snapshot = request.RegionSnapshot;
		var candidateIds = snapshot.CandidatePlayerObjectIds.ToHashSet();
		var removed = 0;

		if (request.RemoveMissingSnapshotCandidates)
		{
			foreach (var existingKnownPlayerObjectId in _membershipService.GetSnapshot(snapshot.OwnerPlayerObjectId).KnownPlayerObjectIds)
			{
				if (candidateIds.Contains(existingKnownPlayerObjectId))
					continue;

				if (_membershipService.RemoveKnownPlayer(snapshot.OwnerPlayerObjectId, existingKnownPlayerObjectId, out _))
					removed++;
			}
		}

		var membershipSnapshot = _membershipService.UpsertKnownPlayers(
			snapshot.OwnerPlayerObjectId,
			snapshot.CandidatePlayerObjectIds.Select(candidateId => new PlayerKnownListMembershipCandidate(
				candidateId,
				request.CandidateVisibleState,
				"Prerequisite projection of KnownList.findVisibleObjects from PlayerKnownListRegionSnapshotService")),
			PlayerKnownListMembershipUpdateReason.RegionSnapshotRefresh);

		return new PlayerKnownListRegionMembershipAdapterResult(
			snapshot.OwnerPlayerObjectId,
			membershipSnapshot,
			snapshot.CandidatePlayerObjectIds.Count,
			snapshot.CandidatePlayerObjectIds.Count,
			removed,
			request.RemoveMissingSnapshotCandidates,
			request.CandidateVisibleState,
			UsesRegionSnapshotPrerequisite: true,
			IsJavaRegionKnownListParity: false,
			"KnownList.findVisibleObjects region candidate snapshot adapted into player-player membership metadata; missing range/canSee/two-way world-object mutation",
			IsLive: false);
	}
}
