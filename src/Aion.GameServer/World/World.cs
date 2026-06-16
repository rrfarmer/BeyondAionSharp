using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World.Container;
using Aion.GameServer.World.Exceptions;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.World;

public sealed class World
{
	private readonly ILogger<World>? _logger;
	private int _initialized;

	// Java parity: world/World — faithful singleton backing stores (allPlayers, allObjects, localSiegeNpcs, worldMaps).
	private readonly PlayerContainer _allPlayers = new();
	private readonly ConcurrentDictionary<int, VisibleObject> _allObjects = new();
	private readonly ConcurrentDictionary<int, List<SiegeNpc>> _localSiegeNpcs = new();
	private readonly Dictionary<int, WorldMap> _worldMaps = new();

	// Java parity: World is a singleton (World.getInstance()). The faithful gameplay layer accesses it statically;
	// the DI lifecycle constructs the instance and binds it here (RegisterInstance), mirroring the DataManager bridge.
	private static World? _instance;

	public static void RegisterInstance(World instance) => _instance = instance;

	public static World GetInstance() => _instance ?? throw new InvalidOperationException(
		"World singleton bridge not initialized; call World.RegisterInstance(...) at startup.");

	public World(ILogger<World> logger)
	{
		_logger = logger;
	}

	// Java parity: World() ctor map-load loop — DataManager.WORLD_MAPS_DATA.forEachParalllel(t -> worldMaps.put(t.getMapId(), new WorldMap(t))).
	public void LoadWorldMaps(WorldMapsData worldMapsData)
	{
		foreach (var template in worldMapsData)
		{
			var worldMap = new WorldMap(template);
			lock (_worldMaps)
				_worldMaps[template.GetMapId()] = worldMap;
		}
		_logger?.LogInformation("World: {Count} world maps created.", _worldMaps.Count);
	}

	public bool IsInitialized => Volatile.Read(ref _initialized) != 0;

	// Java parity: world/World — `allObjects` is the single store of every VisibleObject (incl. faithful
	// House : VisibleObject spawned via SpawnEngine -> HousingService.SpawnHouses).
	public int ObjectCount => _allObjects.Count;

	public void Initialize()
	{
		// Java parity: world/World singleton initialization shell.
		if (Interlocked.Exchange(ref _initialized, 1) == 0)
			_logger.LogInformation("Initialized world container");
	}

	// ---------------------------------------------------------------------------------------------
	// Java parity: world/World faithful gameplay API (storeObject/removeObject/spawn/despawn/...).
	// ---------------------------------------------------------------------------------------------

	// Java parity: storeObject(VisibleObject).
	public void StoreObject(VisibleObject obj)
	{
		if (obj.GetPosition() == null)
		{
			_logger?.LogError("Tried to add {Object} with null position in world", obj);
			return;
		}
		if (!_allObjects.TryAdd(obj.GetObjectId(), obj))
			throw new DuplicateAionObjectException(obj, _allObjects[obj.GetObjectId()]);

		if (obj is SiegeNpc siegeNpc)
			_localSiegeNpcs.GetOrAdd(siegeNpc.GetSiegeId(), _ => new List<SiegeNpc>()).Add(siegeNpc);
		else if (obj is Player player)
			_allPlayers.Add(player);
	}

	// Java parity: removeObject(VisibleObject).
	public bool RemoveObject(VisibleObject obj)
	{
		bool removed = false;
		lock (obj)
		{
			if (_allObjects.TryGetValue(obj.GetObjectId(), out var worldObject) && ReferenceEquals(worldObject, obj))
			{
				try
				{
					if (obj.IsSpawned())
						Despawn(obj);
					obj.GetController().OnDelete();
				}
				catch (Exception e)
				{
					_logger?.LogError(e, "{Object} did not leave world cleanly", obj);
				}
				removed = ((ICollection<KeyValuePair<int, VisibleObject>>)_allObjects).Remove(new KeyValuePair<int, VisibleObject>(obj.GetObjectId(), obj));
			}
			else if (worldObject != null)
			{
				_logger?.LogWarning("Attempt to remove {Object} from world but ID already belongs to {Existing}", obj, worldObject);
			}
		}
		if (removed)
		{
			if (obj is SiegeNpc siegeNpc)
				_localSiegeNpcs[siegeNpc.GetSiegeId()].Remove(siegeNpc);
			else if (obj is Player player)
				_allPlayers.Remove(player);
		}
		return removed;
	}

	// Java parity: getLocalSiegeNpcs(int).
	public ICollection<SiegeNpc> GetLocalSiegeNpcs(int locationId) =>
		_localSiegeNpcs.TryGetValue(locationId, out var result) ? result : Array.Empty<SiegeNpc>();

	// Java parity: getPlayer(String).
	public Player GetPlayer(string name) => _allPlayers.Get(name);

	// Java parity: getPlayer(int).
	public Player GetPlayer(int objectId) => _allPlayers.Get(objectId);

	// Java parity: findVisibleObject(int).
	public VisibleObject? FindVisibleObject(int objectId) =>
		_allObjects.TryGetValue(objectId, out var obj) ? obj : null;

	// Java parity: isInWorld(int).
	public bool IsInWorld(int objectId) => _allObjects.ContainsKey(objectId);

	// Java parity: getWorldMap(int).
	public WorldMap GetWorldMap(int id) => _worldMaps.TryGetValue(id, out var map) ? map : null!;

	// Java parity: updatePosition(VisibleObject, float, float, float, byte).
	public void UpdatePosition(VisibleObject obj, float newX, float newY, float newZ, byte newHeading) =>
		UpdatePosition(obj, newX, newY, newZ, newHeading, true);

	// Java parity: updatePosition(VisibleObject, float, float, float, byte, boolean).
	public void UpdatePosition(VisibleObject obj, float newX, float newY, float newZ, byte newHeading, bool updateKnownList)
	{
		if (!obj.IsSpawned())
		{
			_logger?.LogWarning("Can't update position of despawned object: {Object}", obj);
			return;
		}

		WorldPosition position = obj.GetPosition();
		MapRegion? oldRegion = position.GetMapRegion();
		if (oldRegion == null)
		{
			if (obj is Player player)
			{
				if (!player.IsStaff())
				{
					AuditLogger.Log(player, "is outside valid regions: " + position);
					player.GetClientConnection().Close(SmSystemMessage.STR_KICK_CHARACTER());
				}
			}
			else
			{
				_logger?.LogWarning("Old MapRegion was null when trying to update position of {Object}", obj);
			}
			return;
		}

		MapRegion? newRegion = oldRegion.GetParent().GetRegion(newX, newY, newZ);
		if (newRegion == null)
		{
			_logger?.LogWarning("New MapRegion for {Object} doesn't exist at coordinates: Map {Map}, X {X}, Y {Y}, Z {Z}",
				obj, obj.GetWorldId(), newX, newY, newZ);
			if (obj is Creature creature)
				creature.GetMoveController().AbortMove();
			if (obj is Player player)
			{
				float x, y, z;
				int worldId;
				byte h = 0;

				if (player.GetBindPoint() != null)
				{
					var bplist = player.GetBindPoint();
					worldId = bplist.GetMapId();
					x = bplist.GetX();
					y = bplist.GetY();
					z = bplist.GetZ();
					h = bplist.GetHeading();
				}
				else
				{
					var locationData = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(player.GetCommonData().GetRace());
					worldId = locationData!.GetMapId();
					x = locationData.GetX();
					y = locationData.GetY();
					z = locationData.GetZ();
				}
				SetPosition(obj, worldId, x, y, z, h);
			}
			return;
		}

		position.SetXYZH(newX, newY, newZ, newHeading);

		if (newRegion != oldRegion)
		{
			if (obj is Creature creature)
			{
				oldRegion.RevalidateZones(creature);
				newRegion.RevalidateZones(creature);
			}
			oldRegion.Remove(obj);
			newRegion.Add(obj);
			position.SetMapRegion(newRegion);
		}

		if (updateKnownList)
			obj.UpdateKnownlist();
	}

	// Java parity: setPosition(VisibleObject, int, float, float, float, byte).
	public bool SetPosition(VisibleObject obj, int mapId, float x, float y, float z, byte heading)
	{
		int instanceId = 1;
		if (obj.GetPosition() != null && obj.GetPosition().GetMapId() == mapId)
			instanceId = obj.GetInstanceId();
		return SetPosition(obj, mapId, instanceId, x, y, z, heading);
	}

	// Java parity: setPosition(VisibleObject, int, int, float, float, float, byte).
	public bool SetPosition(VisibleObject obj, int mapId, int instance, float x, float y, float z, byte heading)
	{
		if (obj == null)
			return false;
		WorldPosition pos = CreatePosition(mapId, x, y, z, heading, instance);
		if (pos == null)
			return false;
		if (obj.IsSpawned())
			Despawn(obj);
		obj.SetPosition(pos);
		return true;
	}

	// Java parity: createPosition(int, float, float, float, byte, int).
	public WorldPosition CreatePosition(int mapId, float x, float y, float z, byte heading, int instanceId)
	{
		WorldMap map = GetWorldMap(mapId);
		if (map == null)
			throw new NullReferenceException("Failed to create position (invalid mapId: " + mapId + ")");
		if (map.GetWorldMapInstance(instanceId) == null)
			throw new NullReferenceException("Failed to create position (invalid instanceId " + instanceId + " for mapId " + mapId + ")");
		MapRegion mr = map.GetWorldMapInstance(instanceId).GetRegion(x, y, z);
		if (mr == null)
			throw new NullReferenceException(
				"Failed to create position (invalid coords: x=" + x + ", y=" + y + ", z=" + z + " for mapId " + mapId + " in instanceId " + instanceId + ")");
		return new WorldPosition(mapId, x, y, z, heading, mr);
	}

	// Java parity: spawn(VisibleObject).
	public void Spawn(VisibleObject obj)
	{
		if (obj == null)
			return;
		WorldPosition position = obj.GetPosition();
		if (position.IsSpawned())
			throw new AlreadySpawnedException(obj);

		obj.GetController().OnBeforeSpawn();
		position.SetIsSpawned(true);

		position.GetMapRegion()!.GetParent().AddObject(obj);
		position.GetMapRegion()!.Add(obj);
		obj.GetController().OnAfterSpawn();

		obj.UpdateKnownlist();
	}

	// Java parity: despawn(VisibleObject).
	public void Despawn(VisibleObject obj) => Despawn(obj, ObjectDeleteAnimation.FadeOut);

	// Java parity: despawn(VisibleObject, ObjectDeleteAnimation).
	public void Despawn(VisibleObject obj, ObjectDeleteAnimation animation)
	{
		WorldPosition position = obj.GetPosition();
		try
		{
			obj.GetController().OnDespawn();
		}
		finally
		{
			MapRegion? oldMapRegion = position.GetMapRegion();
			position.SetIsSpawned(false);
			if (oldMapRegion != null)
			{
				oldMapRegion.GetParent().RemoveObject(obj);
				oldMapRegion.Remove(obj);
				if (obj is Creature creature)
					oldMapRegion.RevalidateZones(creature);
			}
			obj.ClearKnownlist(animation);
		}
	}

	// Java parity: updateCachedPlayerName(String, Player).
	public void UpdateCachedPlayerName(string oldName, Player player) =>
		_allPlayers.UpdateCachedPlayerName(oldName, player);

	// Java parity: getAllPlayers().
	public ICollection<Player> GetAllPlayers() => _allPlayers.GetAllPlayers();

	// Java parity: forEachPlayer(Consumer<Player>).
	public void ForEachPlayer(Action<Player> consumer)
	{
		foreach (var player in _allPlayers)
			consumer(player);
	}

	// Java parity: forEachObject(Consumer<VisibleObject>).
	public void ForEachObject(Action<VisibleObject> consumer)
	{
		foreach (var obj in _allObjects.Values)
			consumer(obj);
	}
}
