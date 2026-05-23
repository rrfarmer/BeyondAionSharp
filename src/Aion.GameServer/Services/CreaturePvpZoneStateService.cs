namespace Aion.GameServer.Services;

public static class CreaturePvpZoneStateService
{
	public static bool IsInsidePvpZone(int siegeZoneCount, int pvpZoneCount)
	{
		// Java parity: model/gameobjects/Creature.isInsidePvPZone.
		if (siegeZoneCount > 0)
			return true;

		return pvpZoneCount == 0 || pvpZoneCount == 2;
	}
}
