using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerZoneStateService
{
	public static PlayerZoneRevalidationResult RevalidateFlightZones(
		Player player,
		IReadOnlyList<WorldMapSummary> worldMaps,
		FlightZoneTable? flightZones = null)
	{
		// Java parity: WorldMapInstance.addObject plus ZoneService FlyZoneInstance/NoFlyZoneInstance membership checks.
		var wasInsideFlyZone = player.IsInsideFlyZone;
		var wasInsideNoFlyZone = player.IsInsideNoFlyZone;
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
		return new PlayerZoneRevalidationResult(
			foundWorldMap,
			wasInsideFlyZone,
			player.IsInsideFlyZone,
			wasInsideNoFlyZone,
			player.IsInsideNoFlyZone);
	}
}

public sealed record PlayerZoneRevalidationResult(
	bool FoundWorldMap,
	bool WasInsideFlyZone,
	bool IsInsideFlyZone,
	bool WasInsideNoFlyZone,
	bool IsInsideNoFlyZone)
{
	public bool EnteredFlyZone => !WasInsideFlyZone && IsInsideFlyZone;

	public bool LeftFlyZone => WasInsideFlyZone && !IsInsideFlyZone;

	public bool EnteredNoFlyZone => !WasInsideNoFlyZone && IsInsideNoFlyZone;

	public bool LeftNoFlyZone => WasInsideNoFlyZone && !IsInsideNoFlyZone;
}
