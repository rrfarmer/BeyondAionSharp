using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskUpdateFanoutService
{
	public static PlayerKiskUpdateFanoutPlan CreatePlan(
		PlayerKiskRuntimeState kisk,
		WorldPosition kiskPosition,
		IEnumerable<Player> onlinePlayers,
		Func<Player, int, bool>? isKnownNpc = null,
		int? excludedPlayerObjectId = null)
	{
		// Java parity: model/gameobjects/Kisk.broadcastKiskUpdate.
		var memberIds = kisk.CurrentMemberIds.ToHashSet();
		var directMemberObjectIds = new List<int>();
		var visibleSameRaceObjectIds = new List<int>();

		foreach (var player in onlinePlayers)
		{
			if (excludedPlayerObjectId.HasValue && player.ObjectId == excludedPlayerObjectId.Value)
				continue;

			var sendDirectMemberUpdate = memberIds.Contains(player.ObjectId)
				&& isKnownNpc?.Invoke(player, kisk.ObjectId) == false;
			if (sendDirectMemberUpdate)
			{
				directMemberObjectIds.Add(player.ObjectId);
				continue;
			}

			if (SameRace(player.Race, kisk.OwnerRace) && WorldVisibility.IsVisibleTo(player, kiskPosition))
				visibleSameRaceObjectIds.Add(player.ObjectId);
		}

		return new PlayerKiskUpdateFanoutPlan(directMemberObjectIds, visibleSameRaceObjectIds);
	}

	private static bool SameRace(string left, string right)
	{
		return !string.IsNullOrWhiteSpace(left)
			&& string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}
}

public sealed record PlayerKiskUpdateFanoutPlan(
	IReadOnlyList<int> DirectMemberObjectIds,
	IReadOnlyList<int> VisibleSameRaceObjectIds);
