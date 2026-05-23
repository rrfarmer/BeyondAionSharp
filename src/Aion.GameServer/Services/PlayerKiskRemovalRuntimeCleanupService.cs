using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskRemovalRuntimeCleanupService
{
	public static async ValueTask<PlayerKiskRemovalRuntimeCleanupResult> ApplyAsync(
		PlayerKiskDespawnResult result,
		IGameClientConnectionRegistry? connectionRegistry,
		GameServerRuntimeContext? runtimeContext,
		GameWorld? world = null,
		CancellationToken cancellationToken = default,
		CreaturePvpZoneCounterService? pvpZoneCounterService = null)
	{
		if (result.RemovedKisk == null || cancellationToken.IsCancellationRequested)
			return PlayerKiskRemovalRuntimeCleanupResult.NotApplied;

		var clearedZoneCounters = pvpZoneCounterService?.ClearCounters(result.RemovedKisk.ObjectId) == true ? 1 : 0;
		if (connectionRegistry == null)
			return PlayerKiskRemovalRuntimeCleanupResult.NotApplied with { ClearedZoneCounters = clearedZoneCounters };

		// Java parity: services/KiskService.removeKisk sends the creator a final SM_KISK_UPDATE,
		// clears online member kisk references, restores their obelisk bind point, and refreshes revive options.
		var onlinePlayers = new List<Player>();
		connectionRegistry.ForEachOnlinePlayer(onlinePlayers.Add);
		var plan = PlayerKiskRemovalCleanupService.CreatePlan(result, onlinePlayers);
		var playersByObjectId = onlinePlayers.ToDictionary(player => player.ObjectId);
		var clearBoundObjectIds = plan.ClearBoundObjectIds.ToHashSet();
		var clearPendingRequestObjectIds = plan.ClearPendingRequestObjectIds.ToHashSet();
		var resurrectionOptionRefreshObjectIds = plan.ResurrectionOptionRefreshObjectIds.ToHashSet();

		foreach (var player in onlinePlayers)
		{
			if (clearBoundObjectIds.Contains(player.ObjectId))
				player.BoundKiskObjectId = 0;
			if (clearPendingRequestObjectIds.Contains(player.ObjectId))
				player.PendingKiskBindRequest = null;
		}

		var creatorUpdatesSent = 0;
		if (plan.CreatorUpdateObjectId.HasValue
			&& await connectionRegistry.SendPacketToPlayerAsync(
				plan.CreatorUpdateObjectId.Value,
				new SmKiskUpdate(result.RemovedKisk)))
		{
			creatorUpdatesSent++;
		}

		var bindPointResetsSent = 0;
		var deathOptionRefreshesSent = 0;
		var staticData = runtimeContext?.DataManager?.StaticData;
		foreach (var playerObjectId in plan.BindPointResetObjectIds)
		{
			if (cancellationToken.IsCancellationRequested)
				break;

			if (playersByObjectId.TryGetValue(playerObjectId, out var player)
				&& await connectionRegistry.SendPacketToPlayerAsync(playerObjectId, CreateBindPointPacket(player, staticData)))
			{
				bindPointResetsSent++;
			}

			if (resurrectionOptionRefreshObjectIds.Contains(playerObjectId)
				&& await connectionRegistry.SendPacketToPlayerAsync(playerObjectId, new SmDie()))
			{
				deathOptionRefreshesSent++;
			}
		}

		var npcVisibilityRefreshes = 0;
		if (world != null && result.WorldId.HasValue)
			npcVisibilityRefreshes = await connectionRegistry.RefreshNpcVisibilityAsync(world.GetNpcs(result.WorldId.Value));

		return new PlayerKiskRemovalRuntimeCleanupResult(
			Applied: true,
			CreatorUpdatesSent: creatorUpdatesSent,
			BindPointResetsSent: bindPointResetsSent,
			DeathOptionRefreshesSent: deathOptionRefreshesSent,
			ClearedBoundMembers: clearBoundObjectIds.Count,
			ClearedPendingRequests: clearPendingRequestObjectIds.Count,
			NpcVisibilityRefreshes: npcVisibilityRefreshes,
			ClearedZoneCounters: clearedZoneCounters);
	}

	private static SmBindPointInfo CreateBindPointPacket(Player player, StaticData? staticData)
	{
		// Java parity: services/teleport/TeleportService.sendObeliskBindPoint.
		if (player.BindPoint != null)
			return new SmBindPointInfo(player.BindPoint.MapId, player.BindPoint.X, player.BindPoint.Y, player.BindPoint.Z);

		var spawn = staticData?.PlayerInitialData.GetSpawnLocation(player.Race);
		return spawn == null
			? new SmBindPointInfo(player.Position.WorldId, player.Position.X, player.Position.Y, player.Position.Z)
			: new SmBindPointInfo(spawn.MapId, spawn.X, spawn.Y, spawn.Z);
	}
}

public sealed record PlayerKiskRemovalRuntimeCleanupResult(
	bool Applied,
	int CreatorUpdatesSent,
	int BindPointResetsSent,
	int DeathOptionRefreshesSent,
	int ClearedBoundMembers,
	int ClearedPendingRequests,
	int NpcVisibilityRefreshes,
	int ClearedZoneCounters = 0)
{
	public static PlayerKiskRemovalRuntimeCleanupResult NotApplied { get; } = new(
		Applied: false,
		CreatorUpdatesSent: 0,
		BindPointResetsSent: 0,
		DeathOptionRefreshesSent: 0,
		ClearedBoundMembers: 0,
		ClearedPendingRequests: 0,
		NpcVisibilityRefreshes: 0);
}
