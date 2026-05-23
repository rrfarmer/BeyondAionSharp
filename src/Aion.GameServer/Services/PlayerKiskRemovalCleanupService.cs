using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerKiskRemovalCleanupService
{
	public static PlayerKiskRemovalCleanupPlan CreatePlan(PlayerKiskDespawnResult despawn, IEnumerable<Player> onlinePlayers)
	{
		// Java parity: services/KiskService.removeKisk online-member cleanup after KiskController.delete.
		if (!despawn.RemovedRegistry || despawn.RemovedKisk == null)
			return PlayerKiskRemovalCleanupPlan.Empty;

		var memberObjectIds = despawn.MemberObjectIds.ToHashSet();
		var clearBoundObjectIds = new List<int>();
		var clearPendingRequestObjectIds = new List<int>();
		var bindPointResetObjectIds = new List<int>();
		var resurrectionOptionRefreshObjectIds = new List<int>();
		int? creatorUpdateObjectId = null;

		foreach (var player in onlinePlayers)
		{
			if (player.ObjectId == despawn.RemovedKisk.OwnerObjectId)
				creatorUpdateObjectId = player.ObjectId;

			if (player.PendingKiskBindRequest?.KiskObjectId == despawn.KiskObjectId)
				clearPendingRequestObjectIds.Add(player.ObjectId);

			var wasBoundToRemovedKisk = player.BoundKiskObjectId == despawn.KiskObjectId;
			if (wasBoundToRemovedKisk)
				clearBoundObjectIds.Add(player.ObjectId);

			if (memberObjectIds.Contains(player.ObjectId) || wasBoundToRemovedKisk)
			{
				bindPointResetObjectIds.Add(player.ObjectId);
				if (IsDead(player))
					resurrectionOptionRefreshObjectIds.Add(player.ObjectId);
			}
		}

		return new PlayerKiskRemovalCleanupPlan(
			creatorUpdateObjectId,
			clearBoundObjectIds,
			clearPendingRequestObjectIds,
			bindPointResetObjectIds,
			resurrectionOptionRefreshObjectIds);
	}

	private static bool IsDead(Player player)
	{
		return player.LifeStats?.CurrentHp <= 0
			|| player.CreatureState == PlayerCreatureState.Dead;
	}
}

public sealed record PlayerKiskRemovalCleanupPlan(
	int? CreatorUpdateObjectId,
	IReadOnlyList<int> ClearBoundObjectIds,
	IReadOnlyList<int> ClearPendingRequestObjectIds,
	IReadOnlyList<int> BindPointResetObjectIds,
	IReadOnlyList<int> ResurrectionOptionRefreshObjectIds)
{
	public static PlayerKiskRemovalCleanupPlan Empty { get; } = new(null, [], [], [], []);
}
