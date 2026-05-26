using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed record PlayerKnownListMembershipRefreshResult(
	int OwnerPlayerObjectId,
	PlayerKnownListMembershipSnapshot Snapshot,
	int CandidateCount,
	int UpsertedVisiblePlayerCount,
	int RemovedStalePlayerCount,
	bool UsesWorldVisibilityApproximation,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed class PlayerKnownListMembershipRefreshService
{
	private readonly PlayerKnownListMembershipService _membershipService;

	public PlayerKnownListMembershipRefreshService(PlayerKnownListMembershipService membershipService)
	{
		_membershipService = membershipService;
	}

	public PlayerKnownListMembershipRefreshResult RefreshOwnerFromOnlinePlayers(
		Player owner,
		IEnumerable<Player>? onlinePlayers)
	{
		// Java parity breadcrumb: KnownList.update() performs region-neighbor scans and
		// two-way add/remove. This C# seam is only a current online-player/distance
		// approximation until region-backed known-list population exists.
		var candidates = (onlinePlayers ?? Array.Empty<Player>()).ToArray();
		var visibleCandidates = candidates
			.Where(player => player.ObjectId != owner.ObjectId)
			.Where(player => WorldVisibility.IsVisibleTo(player, owner.Position))
			.Select(player => new PlayerKnownListMembershipCandidate(
				player.ObjectId,
				IsVisibleToOwner: true,
				"Approximation of KnownList.findVisibleObjects using supplied online players + WorldVisibility"))
			.ToArray();
		var visibleCandidateIds = visibleCandidates.Select(candidate => candidate.PlayerObjectId).ToHashSet();
		var removed = 0;

		foreach (var existingKnownPlayerObjectId in _membershipService.GetSnapshot(owner.ObjectId).KnownPlayerObjectIds)
		{
			if (visibleCandidateIds.Contains(existingKnownPlayerObjectId))
				continue;

			if (_membershipService.RemoveKnownPlayer(owner.ObjectId, existingKnownPlayerObjectId, out _))
				removed++;
		}

		var snapshot = _membershipService.UpsertKnownPlayers(
			owner.ObjectId,
			visibleCandidates,
			PlayerKnownListMembershipUpdateReason.WorldVisibilityRefresh);

		return new PlayerKnownListMembershipRefreshResult(
			owner.ObjectId,
			snapshot,
			candidates.Length,
			visibleCandidates.Length,
			removed,
			UsesWorldVisibilityApproximation: true,
			IsJavaRegionKnownListParity: false,
			"KnownList.update -> findVisibleObjects/forgetObjectsOrUpdateVisibility approximated with supplied online players and WorldVisibility",
			IsLive: false);
	}

	public IReadOnlyList<PlayerKnownListMembershipRefreshResult> RefreshAllFromOnlinePlayers(IEnumerable<Player>? onlinePlayers)
	{
		var players = (onlinePlayers ?? Array.Empty<Player>()).ToArray();
		return players
			.Select(owner => RefreshOwnerFromOnlinePlayers(owner, players))
			.ToArray();
	}

	public PlayerKnownListMembershipSnapshot ClearOwnerForLogout(int ownerPlayerObjectId)
	{
		return _membershipService.ClearKnownPlayers(ownerPlayerObjectId);
	}

	public int RemoveDepartingPlayerFromKnownLists(int departingPlayerObjectId, IEnumerable<Player>? remainingOwners)
	{
		var removed = 0;
		foreach (var owner in remainingOwners ?? Array.Empty<Player>())
		{
			if (_membershipService.RemoveKnownPlayer(owner.ObjectId, departingPlayerObjectId, out _))
				removed++;
		}

		return removed;
	}
}
