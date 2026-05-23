using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerKiskAttackabilityService
{
	public static bool IsEnemyFrom(
		Player player,
		PlayerKiskRuntimeState kisk,
		bool kiskInsidePvpZone,
		bool playerInsidePvpZone)
	{
		// Java parity: model/gameobjects/Kisk.isEnemyFrom(Player).
		return !SameRace(player.Race, kisk.OwnerRace)
			&& kiskInsidePvpZone
			&& playerInsidePvpZone;
	}

	public static PlayerKiskCreatureType GetCreatureType(
		Player player,
		PlayerKiskRuntimeState kisk,
		bool kiskInsidePvpZone,
		bool playerInsidePvpZone)
	{
		// Java parity: model/gameobjects/Kisk.getType(Player) returns ATTACKABLE for enemies and SUPPORT otherwise.
		return IsEnemyFrom(player, kisk, kiskInsidePvpZone, playerInsidePvpZone)
			? PlayerKiskCreatureType.Attackable
			: PlayerKiskCreatureType.Support;
	}

	public static PlayerKiskCreatureType GetCreatureType(
		Player player,
		PlayerKiskRuntimeState kisk,
		int kiskSiegeZoneCount,
		int kiskPvpZoneCount,
		int playerSiegeZoneCount,
		int playerPvpZoneCount)
	{
		// Java parity: Kisk.getType(Player) consumes Creature.isInsidePvPZone for both kisk and player.
		return GetCreatureType(
			player,
			kisk,
			CreaturePvpZoneStateService.IsInsidePvpZone(kiskSiegeZoneCount, kiskPvpZoneCount),
			CreaturePvpZoneStateService.IsInsidePvpZone(playerSiegeZoneCount, playerPvpZoneCount));
	}

	private static bool SameRace(string left, string right)
	{
		return !string.IsNullOrWhiteSpace(left)
			&& string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}
}

public enum PlayerKiskCreatureType
{
	// Java parity: model/CreatureType.ATTACKABLE(0).
	Attackable = 0,

	// Java parity: model/CreatureType.SUPPORT(54).
	Support = 54,
}
