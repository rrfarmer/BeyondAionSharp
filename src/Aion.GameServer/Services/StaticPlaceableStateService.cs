using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public interface IStaticPlaceableStateService
{
	void SpawnPlaceableObject(int worldId, int staticId);

	void DespawnPlaceableObject(int worldId, int staticId);

	int GetSpawnCount(int worldId, int staticId);
}

public sealed class StaticPlaceableStateService : IStaticPlaceableStateService
{
	private readonly ConcurrentDictionary<StaticPlaceableKey, int> _spawnCounts = new();

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

	private readonly record struct StaticPlaceableKey(int WorldId, int StaticId);
}
