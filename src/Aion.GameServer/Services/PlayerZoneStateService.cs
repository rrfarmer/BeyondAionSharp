using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerZoneStateService
{
	public static bool RevalidateFlightZones(
		Player player,
		IReadOnlyList<WorldMapSummary> worldMaps,
		FlightZoneTable? flightZones = null)
	{
		// Java parity: WorldMapInstance.addObject plus ZoneService FlyZoneInstance/NoFlyZoneInstance membership checks.
		var worldMap = worldMaps.FirstOrDefault(map => map.MapId == player.Position.WorldId);
		var foundWorldMap = worldMap.MapId != 0;
		var zones = (flightZones ?? FlightZoneTable.Empty).GetZonesByMapId(player.Position.WorldId);
		var insideFlyZone = foundWorldMap && worldMap.AllowsFlight;
		var insideNoFlyZone = false;
		foreach (var zone in zones)
		{
			if (!zone.Contains(player.Position))
				continue;

			if (zone.ZoneType == FlightZoneType.Fly)
				insideFlyZone = true;
			else if (zone.ZoneType == FlightZoneType.NoFly)
				insideNoFlyZone = true;
		}

		player.IsInsideFlyZone = insideFlyZone;
		player.IsInsideNoFlyZone = insideNoFlyZone;
		return foundWorldMap;
	}
}
