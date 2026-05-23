using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerReviveTargetCleanupService
{
	public static PlayerReviveTargetCleanupResult ClearKnownPlayerTargets(
		Player revivedPlayer,
		IEnumerable<Player> players,
		Func<Player, Player, bool>? isKnownPlayer = null)
	{
		// Java parity: services/player/PlayerReviveService.revive clears players in the revived player's known list who target them.
		var clearedObjectIds = new List<int>();
		foreach (var player in players)
		{
			if (player.ObjectId == revivedPlayer.ObjectId)
				continue;
			if (player.TargetObjectId != revivedPlayer.ObjectId)
				continue;
			if (isKnownPlayer != null && !isKnownPlayer(player, revivedPlayer))
				continue;

			player.TargetObjectId = 0;
			clearedObjectIds.Add(player.ObjectId);
		}

		return new PlayerReviveTargetCleanupResult(clearedObjectIds);
	}
}

public sealed record PlayerReviveTargetCleanupResult(IReadOnlyList<int> ClearedTargetObjectIds)
{
	public bool ClearedAny => ClearedTargetObjectIds.Count > 0;
}
