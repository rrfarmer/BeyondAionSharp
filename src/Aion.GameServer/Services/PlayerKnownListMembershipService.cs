using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public enum PlayerKnownListMembershipUpdateReason
{
	Manual,
	WorldVisibilityRefresh,
	RegionSnapshotRefresh,
	TwoWayOperationPlan,
	VisibilityChanged,
	Removed,
	Cleared,
}

public sealed record PlayerKnownListMembershipCandidate(
	int PlayerObjectId,
	bool IsVisibleToOwner,
	string? JavaSource = null);

public sealed record PlayerKnownListMembershipEntry(
	int OwnerPlayerObjectId,
	int KnownPlayerObjectId,
	bool IsVisibleToOwner,
	PlayerKnownListMembershipUpdateReason UpdateReason,
	string JavaSource,
	bool IsLive);

public sealed record PlayerKnownListMembershipSnapshot(
	int OwnerPlayerObjectId,
	IReadOnlyList<PlayerKnownListMembershipEntry> Entries,
	bool ExcludesOwnerByNormalAddPath,
	bool DeduplicatesByObjectId,
	BindPointTeleportKnownListFanoutKnownListOrdering Ordering,
	string JavaSource,
	bool IsLive)
{
	public IReadOnlyList<int> KnownPlayerObjectIds { get; } =
		Entries.Select(entry => entry.KnownPlayerObjectId).ToArray();
}

public sealed class PlayerKnownListMembershipService
{
	private const string DefaultJavaSource =
		"com.aionemu.gameserver.world.knownlist.KnownList.knownObjects / KnownList.forEachPlayer";

	private readonly ConcurrentDictionary<int, Dictionary<int, PlayerKnownListMembershipEntry>> _knownPlayersByOwner = new();

	public PlayerKnownListMembershipSnapshot UpsertKnownPlayers(
		int ownerPlayerObjectId,
		IEnumerable<PlayerKnownListMembershipCandidate>? candidates,
		PlayerKnownListMembershipUpdateReason updateReason = PlayerKnownListMembershipUpdateReason.Manual)
	{
		var entries = _knownPlayersByOwner.GetOrAdd(ownerPlayerObjectId, _ => new Dictionary<int, PlayerKnownListMembershipEntry>());

		lock (entries)
		{
			foreach (var candidate in candidates ?? Array.Empty<PlayerKnownListMembershipCandidate>())
			{
				// Java parity: KnownList.isAwareOf rejects the owner through the normal add path.
				if (candidate.PlayerObjectId == ownerPlayerObjectId)
					continue;

				entries[candidate.PlayerObjectId] = new PlayerKnownListMembershipEntry(
					ownerPlayerObjectId,
					candidate.PlayerObjectId,
					candidate.IsVisibleToOwner,
					updateReason,
					candidate.JavaSource ?? DefaultJavaSource,
					IsLive: false);
			}

			return CreateSnapshot(ownerPlayerObjectId, entries.Values);
		}
	}

	public bool TrySetKnownPlayerVisibility(
		int ownerPlayerObjectId,
		int knownPlayerObjectId,
		bool isVisibleToOwner,
		out PlayerKnownListMembershipSnapshot snapshot)
	{
		if (!_knownPlayersByOwner.TryGetValue(ownerPlayerObjectId, out var entries))
		{
			snapshot = CreateSnapshot(ownerPlayerObjectId, Array.Empty<PlayerKnownListMembershipEntry>());
			return false;
		}

		lock (entries)
		{
			if (!entries.TryGetValue(knownPlayerObjectId, out var existing))
			{
				snapshot = CreateSnapshot(ownerPlayerObjectId, entries.Values);
				return false;
			}

			// Java parity: KnownList.updateVisibility changes KnownObject.visible without removing membership.
			entries[knownPlayerObjectId] = existing with
			{
				IsVisibleToOwner = isVisibleToOwner,
				UpdateReason = PlayerKnownListMembershipUpdateReason.VisibilityChanged,
				JavaSource = "com.aionemu.gameserver.world.knownlist.KnownList.updateVisibility",
			};

			snapshot = CreateSnapshot(ownerPlayerObjectId, entries.Values);
			return true;
		}
	}

	public bool RemoveKnownPlayer(
		int ownerPlayerObjectId,
		int knownPlayerObjectId,
		out PlayerKnownListMembershipSnapshot snapshot)
	{
		if (!_knownPlayersByOwner.TryGetValue(ownerPlayerObjectId, out var entries))
		{
			snapshot = CreateSnapshot(ownerPlayerObjectId, Array.Empty<PlayerKnownListMembershipEntry>());
			return false;
		}

		lock (entries)
		{
			var removed = entries.Remove(knownPlayerObjectId);
			if (entries.Count == 0)
				_knownPlayersByOwner.TryRemove(ownerPlayerObjectId, out _);

			snapshot = CreateSnapshot(ownerPlayerObjectId, entries.Values);
			return removed;
		}
	}

	public PlayerKnownListMembershipSnapshot ClearKnownPlayers(int ownerPlayerObjectId)
	{
		_knownPlayersByOwner.TryRemove(ownerPlayerObjectId, out _);
		return CreateSnapshot(ownerPlayerObjectId, Array.Empty<PlayerKnownListMembershipEntry>());
	}

	public PlayerKnownListMembershipSnapshot GetSnapshot(int ownerPlayerObjectId)
	{
		if (!_knownPlayersByOwner.TryGetValue(ownerPlayerObjectId, out var entries))
			return CreateSnapshot(ownerPlayerObjectId, Array.Empty<PlayerKnownListMembershipEntry>());

		lock (entries)
		{
			return CreateSnapshot(ownerPlayerObjectId, entries.Values);
		}
	}

	public IReadOnlyList<int> GetKnownPlayerObjectIds(int ownerPlayerObjectId, bool includeInvisible = true)
	{
		var snapshot = GetSnapshot(ownerPlayerObjectId);
		return snapshot.Entries
			.Where(entry => includeInvisible || entry.IsVisibleToOwner)
			.Select(entry => entry.KnownPlayerObjectId)
			.ToArray();
	}

	private static PlayerKnownListMembershipSnapshot CreateSnapshot(
		int ownerPlayerObjectId,
		IEnumerable<PlayerKnownListMembershipEntry> entries) =>
		new(
			ownerPlayerObjectId,
			entries.ToArray(),
			ExcludesOwnerByNormalAddPath: true,
			DeduplicatesByObjectId: true,
			BindPointTeleportKnownListFanoutKnownListOrdering.ConcurrentHashMapUnspecified,
			DefaultJavaSource,
			IsLive: false);
}
