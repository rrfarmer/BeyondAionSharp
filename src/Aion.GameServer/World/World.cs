using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.World;

public sealed class World
{
	private readonly ILogger<World> _logger;
	private readonly ConcurrentDictionary<int, object> _objects = new();
	private readonly ConcurrentDictionary<int, WorldHouse> _housesByAddress = new();
	private int _initialized;

	public World(ILogger<World> logger)
	{
		_logger = logger;
	}

	public bool IsInitialized => Volatile.Read(ref _initialized) != 0;

	public int ObjectCount => _objects.Count;

	public int HouseCount => _housesByAddress.Count;

	public void Initialize()
	{
		// Java parity: world/World singleton initialization shell.
		if (Interlocked.Exchange(ref _initialized, 1) == 0)
			_logger.LogInformation("Initialized world container");
	}

	public bool TryAddObject(int objectId, object gameObject)
	{
		// Java parity: world/World.storeObject.
		var added = _objects.TryAdd(objectId, gameObject);
		if (added && gameObject is WorldHouse house)
			_housesByAddress[house.AddressId] = house;
		return added;
	}

	public void AddOrUpdateHouse(WorldHouse house)
	{
		// Java parity: services/HousingService.spawnHouses keeps spawned House objects available to KnownList scans.
		_objects[house.ObjectId] = house;
		_housesByAddress[house.AddressId] = house;
	}

	public IReadOnlyList<WorldHouse> GetHouses()
	{
		return _housesByAddress.Values.ToArray();
	}

	public IReadOnlyList<IWorldNpcObject> GetNpcs()
	{
		// Java parity: World.forEachObject over spawned Npc visible objects for KnownList scans.
		return _objects.Values.OfType<IWorldNpcObject>().ToArray();
	}

	public IReadOnlyList<IWorldNpcObject> GetNpcs(int worldId)
	{
		// Java parity: WorldMapInstance visible-object scans are scoped to the player's world map.
		return _objects.Values
			.OfType<IWorldNpcObject>()
			.Where(npc => npc.Position.WorldId == worldId)
			.ToArray();
	}

	public IReadOnlyList<Player> GetPlayers()
	{
		// Java parity: WorldMapInstance stores visible Player objects for KnownList scans.
		return _objects.Values.OfType<Player>().ToArray();
	}

	public IReadOnlyList<Player> GetPlayers(int worldId)
	{
		// Java parity: WorldMapInstance visible-object scans are scoped to the player's world map.
		return _objects.Values
			.OfType<Player>()
			.Where(player => player.Position.WorldId == worldId)
			.ToArray();
	}

	public bool TryRemoveObject(int objectId, out object? gameObject)
	{
		var removed = _objects.TryRemove(objectId, out gameObject);
		if (removed && gameObject is WorldHouse house)
			_housesByAddress.TryRemove(house.AddressId, out _);
		return removed;
	}

	public bool TryUpdateObject(int objectId, object gameObject)
	{
		if (!_objects.TryGetValue(objectId, out var existing) || !_objects.TryUpdate(objectId, gameObject, existing))
			return false;

		if (existing is WorldHouse oldHouse && gameObject is not WorldHouse)
			_housesByAddress.TryRemove(oldHouse.AddressId, out _);
		if (gameObject is WorldHouse newHouse)
		{
			if (existing is WorldHouse previousHouse && previousHouse.AddressId != newHouse.AddressId)
				_housesByAddress.TryRemove(previousHouse.AddressId, out _);
			_housesByAddress[newHouse.AddressId] = newHouse;
		}

		return true;
	}

	public bool TryGetObject(int objectId, out object? gameObject)
	{
		return _objects.TryGetValue(objectId, out gameObject);
	}

	public void Clear()
	{
		_objects.Clear();
		_housesByAddress.Clear();
	}
}
