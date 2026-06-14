using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: services/KiskService.removeKisk decides the per-player side effects of a kisk removal —
/// the creator receives SM_KISK_UPDATE, every current online member has their bind point reset and their
/// kisk reference cleared, and dead members get their resurrection options refreshed. This pure planner
/// computes those object-id sets so PlayerKiskRemovalRuntimeCleanupService can apply them against the world.
/// </summary>
public static class PlayerKiskRemovalCleanupService
{
	public static PlayerKiskRemovalCleanupPlan CreatePlan(
		PlayerKiskDespawnResult result,
		IReadOnlyCollection<Player> onlinePlayers)
	{
		if (result.RemovedKisk == null)
			return PlayerKiskRemovalCleanupPlan.Empty;

		// Java parity: World.getInstance().getPlayer(kisk.getCreatorId()) -> new SM_KISK_UPDATE(kisk).
		var creatorObjectId = result.RemovedKisk.OwnerObjectId;
		int? creatorUpdateObjectId = onlinePlayers.Any(player => player.ObjectId == creatorObjectId)
			? creatorObjectId
			: null;

		var memberIds = result.MemberObjectIds.ToHashSet();
		var clearBound = new List<int>();
		var clearPending = new List<int>();
		var bindPointReset = new List<int>();
		var resurrectionRefresh = new List<int>();

		// Java parity: for (Player member : kisk.getCurrentMemberList()) { sendKiskBindPoint; setKisk(null);
		// if (member.isDead()) showResurrectionOptions(); }.
		foreach (var player in onlinePlayers)
		{
			if (!memberIds.Contains(player.ObjectId))
				continue;

			bindPointReset.Add(player.ObjectId);
			clearBound.Add(player.ObjectId);
			if (player.PendingKiskBindRequest != null)
				clearPending.Add(player.ObjectId);
			if (player.IsDead())
				resurrectionRefresh.Add(player.ObjectId);
		}

		return new PlayerKiskRemovalCleanupPlan(
			creatorUpdateObjectId,
			bindPointReset,
			clearBound,
			clearPending,
			resurrectionRefresh);
	}
}

public sealed record PlayerKiskRemovalCleanupPlan(
	int? CreatorUpdateObjectId,
	IReadOnlyList<int> BindPointResetObjectIds,
	IReadOnlyList<int> ClearBoundObjectIds,
	IReadOnlyList<int> ClearPendingRequestObjectIds,
	IReadOnlyList<int> ResurrectionOptionRefreshObjectIds)
{
	public static PlayerKiskRemovalCleanupPlan Empty { get; } = new(null, [], [], [], []);
}
