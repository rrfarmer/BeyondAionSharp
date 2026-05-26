namespace Aion.GameServer.Services;

public sealed record PlayerKnownListRegionKey(
	int WorldId,
	int InstanceId,
	int RegionId);

public sealed record PlayerKnownListRegionPlayer(
	int PlayerObjectId,
	PlayerKnownListRegionKey Region,
	bool IsSpawned = true,
	string JavaSource = "com.aionemu.gameserver.world.MapRegion.getObjects");

public sealed record PlayerKnownListRegionSnapshotRequest(
	int OwnerPlayerObjectId,
	PlayerKnownListRegionKey OwnerRegion,
	IEnumerable<int>? NeighbourRegionIds,
	IEnumerable<PlayerKnownListRegionPlayer>? Players);

public sealed record PlayerKnownListRegionSnapshot(
	int OwnerPlayerObjectId,
	PlayerKnownListRegionKey OwnerRegion,
	IReadOnlyList<int> ScannedRegionIds,
	IReadOnlyList<int> CandidatePlayerObjectIds,
	int SourcePlayerCount,
	int ExcludedOwnerCount,
	int ExcludedDifferentWorldOrInstanceCount,
	int ExcludedOutsideNeighbourRegionsCount,
	int ExcludedUnspawnedCount,
	bool ExcludesOwnerByNormalAddPath,
	bool DeduplicatesByObjectId,
	bool PreservesSuppliedRegionOrdering,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed class PlayerKnownListRegionSnapshotService
{
	public PlayerKnownListRegionSnapshot BuildSnapshot(PlayerKnownListRegionSnapshotRequest request)
	{
		// Java parity breadcrumb: KnownList.findVisibleObjects scans the owner's current
		// MapRegion neighbours, then KnownList.isAwareOf rejects the owner.
		var scannedRegionIds = NormalizeRegionIds(request.OwnerRegion.RegionId, request.NeighbourRegionIds);
		var players = (request.Players ?? Array.Empty<PlayerKnownListRegionPlayer>()).ToArray();
		var candidateIds = new List<int>();
		var seenCandidateIds = new HashSet<int>();
		var excludedOwner = 0;
		var excludedDifferentWorldOrInstance = 0;
		var excludedOutsideNeighbourRegions = 0;
		var excludedUnspawned = 0;

		foreach (var regionId in scannedRegionIds)
		{
			foreach (var player in players.Where(player => player.Region.RegionId == regionId))
			{
				if (!player.IsSpawned)
				{
					excludedUnspawned++;
					continue;
				}

				if (player.Region.WorldId != request.OwnerRegion.WorldId || player.Region.InstanceId != request.OwnerRegion.InstanceId)
				{
					excludedDifferentWorldOrInstance++;
					continue;
				}

				if (player.PlayerObjectId == request.OwnerPlayerObjectId)
				{
					excludedOwner++;
					continue;
				}

				if (seenCandidateIds.Add(player.PlayerObjectId))
					candidateIds.Add(player.PlayerObjectId);
			}
		}

		foreach (var player in players)
		{
			if (!scannedRegionIds.Contains(player.Region.RegionId))
				excludedOutsideNeighbourRegions++;
		}

		return new PlayerKnownListRegionSnapshot(
			request.OwnerPlayerObjectId,
			request.OwnerRegion,
			scannedRegionIds,
			candidateIds,
			players.Length,
			excludedOwner,
			excludedDifferentWorldOrInstance,
			excludedOutsideNeighbourRegions,
			excludedUnspawned,
			ExcludesOwnerByNormalAddPath: true,
			DeduplicatesByObjectId: true,
			PreservesSuppliedRegionOrdering: true,
			IsJavaRegionKnownListParity: false,
			"Prerequisite model for KnownList.findVisibleObjects MapRegion neighbour scans; does not execute Java range/canSee/two-way add",
			IsLive: false);
	}

	private static IReadOnlyList<int> NormalizeRegionIds(int ownerRegionId, IEnumerable<int>? neighbourRegionIds)
	{
		var regionIds = new List<int> { ownerRegionId };
		var seen = new HashSet<int> { ownerRegionId };

		foreach (var regionId in neighbourRegionIds ?? Array.Empty<int>())
		{
			if (seen.Add(regionId))
				regionIds.Add(regionId);
		}

		return regionIds;
	}
}
