using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PlayerRideRestrictionService
{
	public static PlayerRideRestrictionResult ValidateStartRide(
		Player player,
		bool enableRideRestriction,
		IReadOnlyList<WorldMapSummary> worldMaps,
		WorldMapRuntimeStateTable? worldMapStates = null)
	{
		// Java parity: model/templates/item/actions/RideAction.canAct iterates player.findZones()
		// and rejects the mount if any ZoneInstance.canRide() is false.
		// Current C# zone data only ports fly/no-fly polygons, so use the mutable WorldMap.canRide slice
		// until general ZoneInstance membership is available.
		if (!enableRideRestriction)
			return PlayerRideRestrictionResult.Allowed(foundWorldMap: false, usedRuntimeWorldMap: false);

		var worldMapState = worldMapStates?.GetMap(player.GetPosition().WorldId);
		if (worldMapState != null)
		{
			return worldMapState.CanRide
				? PlayerRideRestrictionResult.Allowed(foundWorldMap: true, usedRuntimeWorldMap: true)
				: PlayerRideRestrictionResult.InvalidLocation(foundWorldMap: true, usedRuntimeWorldMap: true);
		}

		var worldMap = worldMaps.FirstOrDefault(map => map.MapId == player.GetPosition().WorldId);
		if (worldMap.MapId == 0)
			return PlayerRideRestrictionResult.Allowed(foundWorldMap: false, usedRuntimeWorldMap: false);

		return worldMap.CanRide(worldMap.Flags)
			? PlayerRideRestrictionResult.Allowed(foundWorldMap: true, usedRuntimeWorldMap: false)
			: PlayerRideRestrictionResult.InvalidLocation(foundWorldMap: true, usedRuntimeWorldMap: false);
	}
}

public sealed record PlayerRideRestrictionResult(
	PlayerRideRestrictionStatus Status,
	bool FoundWorldMap,
	bool UsedRuntimeWorldMap)
{
	public bool CanRide => Status == PlayerRideRestrictionStatus.Allowed;

	public static PlayerRideRestrictionResult Allowed(bool foundWorldMap, bool usedRuntimeWorldMap)
	{
		return new PlayerRideRestrictionResult(PlayerRideRestrictionStatus.Allowed, foundWorldMap, usedRuntimeWorldMap);
	}

	public static PlayerRideRestrictionResult InvalidLocation(bool foundWorldMap, bool usedRuntimeWorldMap)
	{
		return new PlayerRideRestrictionResult(PlayerRideRestrictionStatus.InvalidLocation, foundWorldMap, usedRuntimeWorldMap);
	}
}

public enum PlayerRideRestrictionStatus
{
	Allowed,
	InvalidLocation,
}
