using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed record NearbyQuestRegionKey(
	int WorldId,
	int InstanceId,
	int RegionId);

public sealed record NearbyQuestRegionPlayer(
	Player Player,
	NearbyQuestRegionKey Region,
	WorldMapInstanceRuntimeState? ParentWorldInstance,
	bool IsSpawned = true,
	string JavaSource = "com.aionemu.gameserver.world.MapRegion.getObjects");

public sealed record NearbyQuestRegionSnapshotRequest(
	int WorldId,
	int InstanceId,
	IEnumerable<NearbyQuestRegionPlayer>? Players);

public sealed record NearbyQuestRegionSnapshot(
	int WorldId,
	int InstanceId,
	IReadOnlyList<NearbyQuestDelayedRefreshPlayerInput> PlayerInputs,
	int SourcePlayerCount,
	int ExcludedDifferentWorldOrInstanceCount,
	int ExcludedUnspawnedCount,
	bool PreservesSuppliedPlayerOrdering,
	bool IsLive,
	string JavaSource);

public sealed class NearbyQuestRegionSnapshotService
{
	public NearbyQuestRegionSnapshot BuildSnapshot(NearbyQuestRegionSnapshotRequest request)
	{
		// Java parity breadcrumb: WorldMapInstance.updateNearbyQuestsTask iterates the
		// instance's worldMapPlayers values and each PlayerController.updateNearbyQuests
		// resolves that player's current position.mapRegion.parent quest ids.
		var players = (request.Players ?? Array.Empty<NearbyQuestRegionPlayer>()).ToArray();
		var inputs = new List<NearbyQuestDelayedRefreshPlayerInput>();
		var excludedDifferentWorldOrInstance = 0;
		var excludedUnspawned = 0;

		foreach (var player in players)
		{
			if (!player.IsSpawned)
			{
				excludedUnspawned++;
				continue;
			}

			if (player.Region.WorldId != request.WorldId || player.Region.InstanceId != request.InstanceId)
			{
				excludedDifferentWorldOrInstance++;
				continue;
			}

			inputs.Add(new NearbyQuestDelayedRefreshPlayerInput(
				player.Player,
				new NearbyQuestMapRegionSnapshot(
					player.Player.Position,
					player.ParentWorldInstance,
					player.JavaSource)));
		}

		return new NearbyQuestRegionSnapshot(
			request.WorldId,
			request.InstanceId,
			inputs,
			players.Length,
			excludedDifferentWorldOrInstance,
			excludedUnspawned,
			PreservesSuppliedPlayerOrdering: true,
			IsLive: false,
			"Non-live prerequisite model for WorldMapInstance.forEachPlayer -> PlayerController.updateNearbyQuests; does not execute Java MapRegion storage or PacketSendUtility");
	}
}
