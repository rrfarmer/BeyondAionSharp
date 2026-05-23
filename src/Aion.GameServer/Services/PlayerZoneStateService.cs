using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerZoneStateService
{
	public static bool RevalidateFlightZonesFromWorldMap(Player player, IReadOnlyList<WorldMapSummary> worldMaps)
	{
		// Java parity: WorldMapInstance.addObject sets ZoneType.FLY from WorldMap.isFlightAllowed; polygon ZoneType.NO_FLY is still deferred.
		var worldMap = worldMaps.FirstOrDefault(map => map.MapId == player.Position.WorldId);
		var foundWorldMap = worldMap.MapId != 0;
		player.IsInsideFlyZone = foundWorldMap && worldMap.AllowsFlight;
		player.IsInsideNoFlyZone = false;
		return foundWorldMap;
	}
}
