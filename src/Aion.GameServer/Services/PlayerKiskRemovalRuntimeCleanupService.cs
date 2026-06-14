using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskRemovalRuntimeCleanupService
{
	public static ValueTask<PlayerKiskRemovalRuntimeCleanupResult> ApplyAsync(
		PlayerKiskDespawnResult result,
		GameServerRuntimeContext? runtimeContext,
		GameWorld? world = null,
		CancellationToken cancellationToken = default,
		CreaturePvpZoneCounterService? pvpZoneCounterService = null)
	{
		if (result.RemovedKisk == null || cancellationToken.IsCancellationRequested)
			return ValueTask.FromResult(PlayerKiskRemovalRuntimeCleanupResult.NotApplied);

		var clearedZoneCounters = pvpZoneCounterService?.ClearCounters(result.RemovedKisk.ObjectId) == true ? 1 : 0;
		if (world == null)
			return ValueTask.FromResult(PlayerKiskRemovalRuntimeCleanupResult.NotApplied with { ClearedZoneCounters = clearedZoneCounters });

		// Java parity: services/KiskService.removeKisk sends the creator a final SM_KISK_UPDATE,
		// clears online member kisk references, restores their obelisk bind point, and refreshes revive options.
		var onlinePlayers = new List<Player>();
		world.ForEachPlayer(player =>
		{
			if (player.IsOnline())
				onlinePlayers.Add(player);
		});
		var plan = PlayerKiskRemovalCleanupService.CreatePlan(result, onlinePlayers);
		var playersByObjectId = onlinePlayers.ToDictionary(player => player.ObjectId);
		var clearBoundObjectIds = plan.ClearBoundObjectIds.ToHashSet();
		var clearPendingRequestObjectIds = plan.ClearPendingRequestObjectIds.ToHashSet();
		var resurrectionOptionRefreshObjectIds = plan.ResurrectionOptionRefreshObjectIds.ToHashSet();

		foreach (var player in onlinePlayers)
		{
			// Java parity: services/KiskService.removeKisk -> member.setKisk(null) clears the bound kisk.
			if (clearBoundObjectIds.Contains(player.ObjectId))
				player.SetKisk(null);
			// Java parity: the in-flight STR_ASK_REGISTER_BINDSTONE request lives inside the player's
			// ResponseRequester (the SM_QUESTION_WINDOW registry); cancel it there.
			if (clearPendingRequestObjectIds.Contains(player.ObjectId))
				player.GetResponseRequester().Remove(SmQuestionWindow.STR_ASK_REGISTER_BINDSTONE);
		}

		var creatorUpdatesSent = 0;
		if (plan.CreatorUpdateObjectId.HasValue)
		{
			// Java parity: services/KiskService.removeKisk -> PacketSendUtility.sendPacket(creator, new SM_KISK_UPDATE(kisk)).
			var creator = world.GetPlayer(plan.CreatorUpdateObjectId.Value);
			if (creator != null && creator.IsOnline())
			{
				PacketSendUtility.SendPacket(creator, new SmKiskUpdate(result.RemovedKisk));
				creatorUpdatesSent++;
			}
		}

		var bindPointResetsSent = 0;
		var deathOptionRefreshesSent = 0;
		var staticData = runtimeContext?.DataManager?.StaticData;
		foreach (var playerObjectId in plan.BindPointResetObjectIds)
		{
			if (cancellationToken.IsCancellationRequested)
				break;

			if (playersByObjectId.TryGetValue(playerObjectId, out var player) && player.IsOnline())
			{
				// Java parity: services/teleport/TeleportService.sendKiskBindPoint -> obelisk bind point packet.
				PacketSendUtility.SendPacket(player, CreateBindPointPacket(player, staticData));
				bindPointResetsSent++;

				if (resurrectionOptionRefreshObjectIds.Contains(playerObjectId))
				{
					// Java parity: PlayerController.showResurrectionOptions sends SM_DIE to dead members.
					PacketSendUtility.SendPacket(player, new SmDie());
					deathOptionRefreshesSent++;
				}
			}
		}

		return ValueTask.FromResult(new PlayerKiskRemovalRuntimeCleanupResult(
			Applied: true,
			CreatorUpdatesSent: creatorUpdatesSent,
			BindPointResetsSent: bindPointResetsSent,
			DeathOptionRefreshesSent: deathOptionRefreshesSent,
			ClearedBoundMembers: clearBoundObjectIds.Count,
			ClearedPendingRequests: clearPendingRequestObjectIds.Count,
			// Java parity: KiskService.removeKisk does no NPC-visibility refresh; the reworked composite had no Java basis.
			NpcVisibilityRefreshes: 0,
			ClearedZoneCounters: clearedZoneCounters));
	}

	private static SmBindPointInfo CreateBindPointPacket(Player player, StaticData? staticData)
	{
		// Java parity: services/teleport/TeleportService.sendObeliskBindPoint.
		if (player.BindPoint != null)
			return new SmBindPointInfo(player.BindPoint.MapId, player.BindPoint.X, player.BindPoint.Y, player.BindPoint.Z);

		var spawn = staticData?.PlayerInitialData.GetSpawnLocation(player.Race.ToString());
		return spawn == null
			? new SmBindPointInfo(player.GetPosition().WorldId, player.GetPosition().X, player.GetPosition().Y, player.GetPosition().Z)
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
