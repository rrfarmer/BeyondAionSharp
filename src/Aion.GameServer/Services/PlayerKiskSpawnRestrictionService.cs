using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskSpawnRestrictionService
{
	public static PlayerKiskSpawnRestrictionResult ValidateSpawn(
		Player player,
		bool enableKiskRestriction,
		bool hasKisk,
		IReadOnlyList<WorldMapSummary> worldMaps,
		WorldMapRuntimeStateTable? worldMapStates = null)
	{
		// Java parity: model/templates/item/actions/ToyPetSpawnAction.canAct.
		if (player.IsFlying())
			return PlayerKiskSpawnRestrictionResult.Flying(foundWorldMap: false, usedRuntimeWorldMap: false);

		var lookup = LookupWorldMap(player, worldMaps, worldMapStates);
		if (lookup.IsInstance)
			return PlayerKiskSpawnRestrictionResult.Instance(lookup.FoundWorldMap, lookup.UsedRuntimeWorldMap);

		if (hasKisk && enableKiskRestriction)
			return PlayerKiskSpawnRestrictionResult.AlreadyInstalled(lookup.FoundWorldMap, lookup.UsedRuntimeWorldMap);

		if (lookup.RuntimeState != null)
		{
			return lookup.RuntimeState.CanPutKisk
				? PlayerKiskSpawnRestrictionResult.Allowed(foundWorldMap: true, usedRuntimeWorldMap: true)
				: PlayerKiskSpawnRestrictionResult.InvalidLocation(foundWorldMap: true, usedRuntimeWorldMap: true);
		}

		if (lookup.StaticMap == null)
			return PlayerKiskSpawnRestrictionResult.Allowed(foundWorldMap: false, usedRuntimeWorldMap: false);

		return lookup.StaticMap.Value.CanPutKisk(lookup.StaticMap.Value.Flags)
			? PlayerKiskSpawnRestrictionResult.Allowed(foundWorldMap: true, usedRuntimeWorldMap: false)
			: PlayerKiskSpawnRestrictionResult.InvalidLocation(foundWorldMap: true, usedRuntimeWorldMap: false);
	}

	private static PlayerKiskWorldMapLookup LookupWorldMap(
		Player player,
		IReadOnlyList<WorldMapSummary> worldMaps,
		WorldMapRuntimeStateTable? worldMapStates)
	{
		var runtimeState = worldMapStates?.GetMap(player.GetPosition().WorldId);
		if (runtimeState != null)
			return new PlayerKiskWorldMapLookup(runtimeState.Summary.IsInstance, true, true, runtimeState, null);

		var staticMap = worldMaps.FirstOrDefault(map => map.MapId == player.GetPosition().WorldId);
		if (staticMap.MapId == 0)
			return new PlayerKiskWorldMapLookup(false, false, false, null, null);

		// Java parity: VisibleObject.isInInstance delegates to WorldPosition.isInstanceMap,
		// which reads the containing WorldMap instance type instead of the numeric instance id.
		return new PlayerKiskWorldMapLookup(staticMap.IsInstance, true, false, null, staticMap);
	}

	private sealed record PlayerKiskWorldMapLookup(
		bool IsInstance,
		bool FoundWorldMap,
		bool UsedRuntimeWorldMap,
		WorldMapRuntimeState? RuntimeState,
		WorldMapSummary? StaticMap);
}

public sealed record PlayerKiskSpawnRestrictionResult(
	PlayerKiskSpawnRestrictionStatus Status,
	bool FoundWorldMap,
	bool UsedRuntimeWorldMap)
{
	public bool CanSpawn => Status == PlayerKiskSpawnRestrictionStatus.Allowed;

	public static PlayerKiskSpawnRestrictionResult Allowed(bool foundWorldMap, bool usedRuntimeWorldMap)
	{
		return new PlayerKiskSpawnRestrictionResult(PlayerKiskSpawnRestrictionStatus.Allowed, foundWorldMap, usedRuntimeWorldMap);
	}

	public static PlayerKiskSpawnRestrictionResult Flying(bool foundWorldMap, bool usedRuntimeWorldMap)
	{
		return new PlayerKiskSpawnRestrictionResult(PlayerKiskSpawnRestrictionStatus.Flying, foundWorldMap, usedRuntimeWorldMap);
	}

	public static PlayerKiskSpawnRestrictionResult Instance(bool foundWorldMap, bool usedRuntimeWorldMap)
	{
		return new PlayerKiskSpawnRestrictionResult(PlayerKiskSpawnRestrictionStatus.Instance, foundWorldMap, usedRuntimeWorldMap);
	}

	public static PlayerKiskSpawnRestrictionResult AlreadyInstalled(bool foundWorldMap, bool usedRuntimeWorldMap)
	{
		return new PlayerKiskSpawnRestrictionResult(PlayerKiskSpawnRestrictionStatus.AlreadyInstalled, foundWorldMap, usedRuntimeWorldMap);
	}

	public static PlayerKiskSpawnRestrictionResult InvalidLocation(bool foundWorldMap, bool usedRuntimeWorldMap)
	{
		return new PlayerKiskSpawnRestrictionResult(PlayerKiskSpawnRestrictionStatus.InvalidLocation, foundWorldMap, usedRuntimeWorldMap);
	}
}

public enum PlayerKiskSpawnRestrictionStatus
{
	Allowed,
	Flying,
	Instance,
	AlreadyInstalled,
	InvalidLocation,
}
