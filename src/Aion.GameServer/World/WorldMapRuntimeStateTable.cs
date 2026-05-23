using Aion.GameServer.Dataholders;

namespace Aion.GameServer.World;

public sealed class WorldMapRuntimeStateTable
{
	public static readonly WorldMapRuntimeStateTable Empty = new(Array.Empty<WorldMapSummary>());

	private readonly IReadOnlyDictionary<int, WorldMapRuntimeState> _statesByMapId;

	public WorldMapRuntimeStateTable(IReadOnlyList<WorldMapSummary> worldMaps)
	{
		// Java parity: world/World constructor creates one WorldMap per WorldMapTemplate and stores it by map id.
		var states = worldMaps
			.GroupBy(map => map.MapId)
			.Select(group => new WorldMapRuntimeState(group.Last()))
			.ToArray();
		States = states;
		_statesByMapId = states.ToDictionary(state => state.Summary.MapId);
	}

	public IReadOnlyList<WorldMapRuntimeState> States { get; }

	public int Count => States.Count;

	public bool TryGetMap(int mapId, out WorldMapRuntimeState? state)
	{
		// Java parity: world/World.getWorldMap(int) lookup by map id.
		return _statesByMapId.TryGetValue(mapId, out state);
	}

	public WorldMapRuntimeState? GetMap(int mapId)
	{
		// Java parity: world/World.getWorldMap(int) returns null for unknown map ids.
		return _statesByMapId.GetValueOrDefault(mapId);
	}

	public bool SetWorldOption(int mapId, WorldZoneAttributes option)
	{
		if (!_statesByMapId.TryGetValue(mapId, out var state))
			return false;

		state.SetWorldOption(option);
		return true;
	}

	public bool RemoveWorldOption(int mapId, WorldZoneAttributes option)
	{
		if (!_statesByMapId.TryGetValue(mapId, out var state))
			return false;

		state.RemoveWorldOption(option);
		return true;
	}

	public bool InstanceExists(int mapId, int instanceId)
	{
		// Java parity: services/instance/InstanceService.instanceExists delegates to WorldMap.getWorldMapInstance(instanceId) != null.
		return _statesByMapId.TryGetValue(mapId, out var state) && state.InstanceExists(instanceId);
	}

	public bool RemoveWorldMapInstance(int mapId, int instanceId)
	{
		if (!_statesByMapId.TryGetValue(mapId, out var state))
			return false;

		state.RemoveWorldMapInstance(instanceId);
		return true;
	}

	public WorldMapInstanceRuntimeState? AddWorldMapInstance(int mapId, int instanceId, int ownerId = 0, int maxPlayers = 0)
	{
		if (!_statesByMapId.TryGetValue(mapId, out var state))
			return null;

		return state.AddWorldMapInstance(instanceId, ownerId, maxPlayers);
	}

	public WorldMapInstanceRuntimeState? CreateNextWorldMapInstance(int mapId, int ownerId = 0, int maxPlayers = 0)
	{
		if (!_statesByMapId.TryGetValue(mapId, out var state))
			return null;

		return state.CreateNextWorldMapInstance(ownerId, maxPlayers);
	}

	public bool TryGetWorldMapInstance(int mapId, int instanceId, out WorldMapInstanceRuntimeState? instance)
	{
		if (!_statesByMapId.TryGetValue(mapId, out var state))
		{
			instance = null;
			return false;
		}

		return state.TryGetWorldMapInstance(instanceId, out instance);
	}

	public WorldMapInstanceRuntimeState? GetRegisteredInstance(int mapId, int objectId)
	{
		// Java parity: InstanceService.getRegisteredInstance(worldId, objectId).
		return _statesByMapId.TryGetValue(mapId, out var state) ? state.GetRegisteredInstance(objectId) : null;
	}
}
