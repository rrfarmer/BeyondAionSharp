using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public interface IStaticPlaceableStateService
{
	void SpawnPlaceableObject(int worldId, int staticId);

	void DespawnPlaceableObject(int worldId, int staticId);

	int GetSpawnCount(int worldId, int staticId);

	void SetDoorState(int worldId, int instanceId, int staticId, bool open);

	bool? GetDoorState(int worldId, int instanceId, int staticId);
}

public sealed class StaticPlaceableStateService : IStaticPlaceableStateService
{
	private readonly ConcurrentDictionary<StaticPlaceableKey, int> _spawnCounts = new();
	private readonly ConcurrentDictionary<StaticDoorKey, bool> _doorStates = new();

	public void SpawnPlaceableObject(int worldId, int staticId)
	{
		if (staticId <= 0)
			return;

		// Java parity: world/geo/GeoService.spawnPlaceableObject activates collision/placeable state for spawn static IDs.
		_spawnCounts.AddOrUpdate(new StaticPlaceableKey(worldId, staticId), 1, (_, count) => count + 1);
	}

	public void DespawnPlaceableObject(int worldId, int staticId)
	{
		if (staticId <= 0)
			return;

		// Java parity: world/geo/GeoService.despawnPlaceableObject clears collision/placeable state when the spawn despawns.
		var key = new StaticPlaceableKey(worldId, staticId);
		_spawnCounts.AddOrUpdate(key, 0, (_, count) => Math.Max(0, count - 1));
		_spawnCounts.TryRemove(new KeyValuePair<StaticPlaceableKey, int>(key, 0));
	}

	public int GetSpawnCount(int worldId, int staticId)
	{
		return _spawnCounts.GetValueOrDefault(new StaticPlaceableKey(worldId, staticId));
	}

	public void SetDoorState(int worldId, int instanceId, int staticId, bool open)
	{
		if (worldId <= 0 || instanceId <= 0 || staticId <= 0)
			return;

		// Java parity: world/geo/GeoService.setDoorState stores static door collision state by map, instance, and door id.
		_doorStates[new StaticDoorKey(worldId, instanceId, staticId)] = open;
	}

	public bool? GetDoorState(int worldId, int instanceId, int staticId)
	{
		return _doorStates.TryGetValue(new StaticDoorKey(worldId, instanceId, staticId), out var open)
			? open
			: null;
	}

	private readonly record struct StaticPlaceableKey(int WorldId, int StaticId);

	private readonly record struct StaticDoorKey(int WorldId, int InstanceId, int StaticId);
}
