using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PlayerZoneStateService
{
	public static PlayerZoneRevalidationResult RevalidateFlightZones(
		Player player,
		IReadOnlyList<WorldMapSummary> worldMaps,
		FlightZoneTable? flightZones = null,
		WorldMapRuntimeStateTable? worldMapStates = null)
	{
		// Java parity: WorldMapInstance.addObject plus ZoneService FlyZoneInstance/NoFlyZoneInstance membership checks.
		var wasInsideFlyZone = player.IsInsideFlyZone;
		var wasInsideNoFlyZone = player.IsInsideNoFlyZone;
		var worldMapState = worldMapStates?.GetMap(player.Position.WorldId);
		var worldMap = worldMapState?.Summary ?? worldMaps.FirstOrDefault(map => map.MapId == player.Position.WorldId);
		var foundWorldMap = worldMapState != null || worldMap.MapId != 0;
		var zones = (flightZones ?? FlightZoneTable.Empty).GetZonesByMapId(player.Position.WorldId);
		var insideFlyZone = foundWorldMap && (worldMapState?.IsFlightAllowed ?? worldMap.IsFlightAllowed(worldMap.Flags));
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

	public static PlayerFlightZoneTransitionResult ApplyFlightZoneTransitionIntent(
		Player player,
		PlayerZoneRevalidationResult result,
		int freeFlightAccessLevel = 1)
	{
		// Java parity: FlyZoneInstance/NoFlyZoneInstance call PlayerController.onEnterFlyArea/onLeaveFlyArea.
		if (result.LeftValidFlyArea)
		{
			var status = player.LeaveFlyArea(freeFlightAccessLevel);
			return new PlayerFlightZoneTransitionResult(
				EnteredValidFlyArea: false,
				LeftValidFlyArea: true,
				FpReduceTriggered: status is PlayerLeaveFlyAreaStatus.ContinueGliding or PlayerLeaveFlyAreaStatus.GlidingOutsideFlyArea,
				LeaveStatus: status);
		}

		if (result.EnteredValidFlyArea)
		{
			var fpReduceTriggered = player.EnterFlyArea();
			return new PlayerFlightZoneTransitionResult(
				EnteredValidFlyArea: true,
				LeftValidFlyArea: false,
				FpReduceTriggered: fpReduceTriggered,
				LeaveStatus: PlayerLeaveFlyAreaStatus.NoChange);
		}

		return PlayerFlightZoneTransitionResult.None;
	}
}

public sealed record PlayerZoneRevalidationResult(
	bool FoundWorldMap,
	bool WasInsideFlyZone,
	bool IsInsideFlyZone,
	bool WasInsideNoFlyZone,
	bool IsInsideNoFlyZone)
{
	public bool WasInsideValidFlyArea => WasInsideFlyZone && !WasInsideNoFlyZone;

	public bool IsInsideValidFlyArea => IsInsideFlyZone && !IsInsideNoFlyZone;

	public bool EnteredFlyZone => !WasInsideFlyZone && IsInsideFlyZone;

	public bool LeftFlyZone => WasInsideFlyZone && !IsInsideFlyZone;

	public bool EnteredNoFlyZone => !WasInsideNoFlyZone && IsInsideNoFlyZone;

	public bool LeftNoFlyZone => WasInsideNoFlyZone && !IsInsideNoFlyZone;

	public bool EnteredValidFlyArea => !WasInsideValidFlyArea && IsInsideValidFlyArea;

	public bool LeftValidFlyArea => WasInsideValidFlyArea && !IsInsideValidFlyArea;
}

public sealed record PlayerFlightZoneTransitionResult(
	bool EnteredValidFlyArea,
	bool LeftValidFlyArea,
	bool FpReduceTriggered,
	PlayerLeaveFlyAreaStatus LeaveStatus)
{
	public static PlayerFlightZoneTransitionResult None { get; } = new(
		EnteredValidFlyArea: false,
		LeftValidFlyArea: false,
		FpReduceTriggered: false,
		LeaveStatus: PlayerLeaveFlyAreaStatus.NoChange);
}
